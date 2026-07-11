using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.MergeVerification;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=== ScholarTrend Merge Verification ===");
        Console.WriteLine();

        var connectionString = args.Length > 0
            ? args[0]
            : Environment.GetEnvironmentVariable("CONNECTION_STRING")
              ?? "Host=localhost;Port=5432;Database=scholartrend;Username=postgres;Password=postgres";

        Console.WriteLine($"Connection: {MaskConnectionString(connectionString)}");
        Console.WriteLine();

        var optionsBuilder = new DbContextOptionsBuilder<ScholarTrendDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        await using var context = new ScholarTrendDbContext(optionsBuilder.Options);

        var papers = await context.ResearchPapers
            .OrderBy(p => p.Id)
            .ToListAsync();

        Console.WriteLine($"Loaded {papers.Count} papers from DB");
        Console.WriteLine();

        var report = new List<MergeReportRow>();
        int realDoiCount = 0;
        int fakeDoiCount = 0;
        int missingDoiCount = 0;

        foreach (var paper in papers)
        {
            var row = new MergeReportRow
            {
                Id = paper.Id,
                Title = Truncate(paper.Title, 60),
                Doi = paper.Doi ?? "(null)",
                CitationCount = paper.CitationCount
            };

            if (string.IsNullOrWhiteSpace(paper.Doi))
            {
                missingDoiCount++;
                row.Note = "Missing DOI";
            }
            else if (paper.Doi.StartsWith("10.1000/st.", StringComparison.OrdinalIgnoreCase))
            {
                fakeDoiCount++;
                row.Note = "Fake DOI (test data)";
            }
            else
            {
                realDoiCount++;
                row.Note = "Real DOI";
            }

            report.Add(row);
        }

        Console.WriteLine("=== SUMMARY ===");
        Console.WriteLine($"Total papers:        {papers.Count}");
        Console.WriteLine($"Real DOI:            {realDoiCount} ({(papers.Count > 0 ? realDoiCount * 100.0 / papers.Count : 0):F1}%)");
        Console.WriteLine($"Fake/test DOI:       {fakeDoiCount}");
        Console.WriteLine($"Missing DOI:         {missingDoiCount}");
        Console.WriteLine();

        Console.WriteLine("=== DETAIL TABLE ===");
        Console.WriteLine($"{"Id",-5} {"Doi",-32} {"CitedBy",-10} {"Title",-60} {"Note",-30}");
        Console.WriteLine(new string('-', 140));
        foreach (var row in report)
        {
            Console.WriteLine($"{row.Id,-5} {Truncate(row.Doi, 32),-32} {row.CitationCount,-10} {row.Title,-60} {row.Note,-30}");
        }
        Console.WriteLine();

        // Test merge logic với 5 papers có real DOI đầu tiên
        var realDoiPapers = report
            .Where(r => r.Note == "Real DOI")
            .Take(5)
            .ToList();

        if (realDoiPapers.Count > 0)
        {
            Console.WriteLine("=== SIMULATING EXTERNAL API LOOKUP ===");
            Console.WriteLine($"Testing {realDoiPapers.Count} papers with real DOI...");
            Console.WriteLine();

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ScholarTrendVerification/1.0");

            foreach (var row in realDoiPapers)
            {
                Console.WriteLine($"--- Paper {row.Id}: DOI={row.Doi} ---");
                await SimulateLookupAsync(row.Doi, http);
                Console.WriteLine();
                await Task.Delay(500);
            }
        }

        // Save report
        var jsonPath = "merge-report.json";
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(jsonPath, json);
        Console.WriteLine();
        Console.WriteLine($"Report saved to {jsonPath}");

        return 0;
    }

    private static async Task SimulateLookupAsync(string doi, HttpClient http)
    {
        var oaTask = SafeGetAsync($"https://api.openalex.org/works/doi:{doi}", http, "OpenAlex");
        var ssTask = SafeGetAsync($"https://api.semanticscholar.org/graph/v1/paper/DOI:{doi}?fields=title", http, "SemanticScholar");
        var crTask = SafeGetAsync($"https://api.crossref.org/works/{doi}", http, "Crossref");

        await Task.WhenAll(oaTask, ssTask, crTask);

        Console.WriteLine($"  OpenAlex:        {(oaTask.Result.IsFound ? $"FOUND ({oaTask.Result.LatencyMs}ms)" : $"MISS ({oaTask.Result.LatencyMs}ms)")}");
        Console.WriteLine($"  SemanticScholar: {(ssTask.Result.IsFound ? $"FOUND ({ssTask.Result.LatencyMs}ms)" : $"MISS ({ssTask.Result.LatencyMs}ms)")}");
        Console.WriteLine($"  Crossref:        {(crTask.Result.IsFound ? $"FOUND ({crTask.Result.LatencyMs}ms)" : $"MISS ({crTask.Result.LatencyMs}ms)")}");
    }

    private static async Task<LookupResult> SafeGetAsync(string url, HttpClient http, string name)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await http.GetAsync(url);
            sw.Stop();
            return new LookupResult
            {
                Name = name,
                IsFound = response.IsSuccessStatusCode,
                LatencyMs = (int)sw.ElapsedMilliseconds,
                StatusCode = (int)response.StatusCode
            };
        }
        catch
        {
            sw.Stop();
            return new LookupResult { Name = name, IsFound = false, LatencyMs = (int)sw.ElapsedMilliseconds };
        }
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }

    private static string MaskConnectionString(string cs)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            cs, @"(Password|password)=[^;]+", "$1=***", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}

public class MergeReportRow
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Doi { get; set; } = "";
    public int? CitationCount { get; set; }
    public string Note { get; set; } = "";
}

internal class LookupResult
{
    public string Name { get; set; } = "";
    public bool IsFound { get; set; }
    public int LatencyMs { get; set; }
    public int StatusCode { get; set; }
}
