// Quick test to verify PDF download
using System.Net.Http;
using ScholarTrend.Infrastructure.Storage;

var http = new HttpClient();
http.Timeout = TimeSpan.FromSeconds(30);
http.DefaultRequestHeaders.Add("User-Agent", "Test/1.0");

var downloader = new HttpDocumentDownloader(http, null!);

var urls = new[]
{
    "https://arxiv.org/pdf/2103.14030.pdf",
    "https://arxiv.org/pdf/1706.03762.pdf",
    "https://arxiv.org/pdf/2210.03629.pdf"
};

foreach (var url in urls)
{
    Console.WriteLine($"Downloading: {url}");
    var doc = await downloader.DownloadAsync(url, CancellationToken.None);
    if (doc != null)
    {
        Console.WriteLine($"  OK: {doc.Bytes.Length} bytes, ContentType: {doc.ContentType}");
    }
    else
    {
        Console.WriteLine("  FAILED: returned null");
    }
}
