using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.GapAnalysis;
using ScholarTrend.Application.DTOs.TopicInsights;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.ExternalApis;

public class GroqExtractionService : IAiExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GroqExtractionService> _logger;
    private readonly string? _apiKey;

    public GroqExtractionService(HttpClient httpClient, IConfiguration configuration, ILogger<GroqExtractionService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _apiKey = _configuration["GroqAI:ApiKey"];
    }

    private async Task<string?> CallGroqApiAsync(string prompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("Groq API key is missing.");
            return null;
        }

        var requestBody = new
        {
            model = "llama-3.3-70b-versatile",
            messages = new[]
            {
                new { role = "system", content = "You are an expert AI academic researcher. You must only output a valid JSON object or array as requested. Do not include markdown code blocks or any conversational text." },
                new { role = "user", content = prompt }
            },
            response_format = new { type = "json_object" },
            temperature = 0.2
        };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        requestMessage.Headers.Add("Authorization", $"Bearer {_apiKey}");
        requestMessage.Content = JsonContent.Create(requestBody);

        int maxRetries = 3;
        int currentRetry = 0;

        while (currentRetry < maxRetries)
        {
            try
            {
                var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseData = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                var textResult = responseData.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

                if (string.IsNullOrWhiteSpace(textResult)) return null;

                // Clean up any markdown that models might still output
                textResult = textResult.Trim();
                if (textResult.StartsWith("```json")) textResult = textResult.Substring(7);
                if (textResult.StartsWith("```")) textResult = textResult.Substring(3);
                if (textResult.EndsWith("```")) textResult = textResult.Substring(0, textResult.Length - 3);
                
                return textResult.Trim();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                currentRetry++;
                _logger.LogWarning($"Groq API Rate Limit (429). Retrying {currentRetry}/{maxRetries} in 5 seconds...");
                if (currentRetry >= maxRetries) return null;
                await Task.Delay(5000, cancellationToken);
                
                // Recreate request message for retry
                requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
                requestMessage.Headers.Add("Authorization", $"Bearer {_apiKey}");
                requestMessage.Content = JsonContent.Create(requestBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to communicate with Groq AI.");
                return null;
            }
        }

        return null;
    }

    public async Task<AiPaperExtractionDto?> ExtractFromAbstractAsync(string abstractText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(abstractText))
            return null;

        var prompt = $@"
Analyze the following text from an academic paper.
Identify:
1. 'methods': Key methodologies, architectures, or algorithms proposed or used.
2. 'datasets': Datasets or benchmarks used in the evaluation.
3. 'limitations': Explicit limitations or weaknesses mentioned in the paper.
4. 'future_work': Explicit future work or next steps mentioned in the paper.
5. 'discussions': Key discussion points and their implications.
6. 'conclusions': Main conclusions drawn from the research.
7. 'research_problem': The main research problem or question addressed.
8. 'metric': Evaluation metrics used to measure performance.
9. 'contribution': The main contribution of the paper.

Text:
{abstractText}

Return ONLY a valid JSON object matching this structure:
{{
  ""methods"": [""method 1"", ""method 2""],
  ""datasets"": [""dataset 1""],
  ""limitations"": [""limitation 1""],
  ""future_work"": [""future direction 1""],
  ""discussions"": [""discussion point 1""],
  ""conclusions"": [""conclusion 1""],
  ""research_problem"": ""problem statement"",
  ""metric"": ""evaluation metric"",
  ""contribution"": ""main contribution""
}}";

        var textResult = await CallGroqApiAsync(prompt, cancellationToken);
        if (string.IsNullOrWhiteSpace(textResult)) return null;

        try
        {
            return JsonSerializer.Deserialize<AiPaperExtractionDto>(textResult, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Groq extraction result.");
            return null;
        }
    }

    public async Task<AiPaperExtractionDto?> ExtractFromFullTextAsync(string fullText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullText))
            return null;

        var prompt = $@"
Analyze the following text from an academic paper (possibly from Discussion, Conclusion, or Future Work sections).
Extract structured information with high precision.

Text:
{fullText}

Return ONLY a valid JSON object matching this structure:
{{
  ""methods"": [""method 1"", ""method 2""],
  ""datasets"": [""dataset 1""],
  ""limitations"": [""limitation 1""],
  ""future_work"": [""future direction 1""],
  ""discussions"": [""discussion point 1""],
  ""conclusions"": [""conclusion 1""],
  ""research_problem"": ""problem statement"",
  ""metric"": ""evaluation metric"",
  ""contribution"": ""main contribution""
}}";

        var textResult = await CallGroqApiAsync(prompt, cancellationToken);
        if (string.IsNullOrWhiteSpace(textResult)) return null;

        try
        {
            return JsonSerializer.Deserialize<AiPaperExtractionDto>(textResult, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Groq full text extraction result.");
            return null;
        }
    }

    public async Task<HybridExtractionResultDto?> ExtractHybridAsync(
        string abstractText,
        string? discussionSection,
        string? conclusionSection,
        string? introductionSection,
        string? methodologySection,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(abstractText))
            return null;

        var result = new HybridExtractionResultDto();

        // Stage 1: Extract from abstract (always)
        var abstractExtraction = await ExtractFromAbstractAsync(abstractText, cancellationToken);
        if (abstractExtraction == null)
        {
            _logger.LogWarning("Failed to extract from abstract");
            return null;
        }

        result.PrimaryExtraction = abstractExtraction;
        result.Metadata.UsedAbstract = true;
        result.Metadata.ExtractionTimestamp = DateTime.UtcNow;

        // Calculate abstract confidence
        result.Metadata.ConfidenceBreakdown.AbstractConfidence = CalculateFieldConfidence(abstractExtraction);

        // Estimate tokens used
        result.Metadata.TotalTokensEstimate = EstimateTokens(abstractText);
        if (!string.IsNullOrWhiteSpace(discussionSection))
            result.Metadata.TotalTokensEstimate += EstimateTokens(discussionSection);
        if (!string.IsNullOrWhiteSpace(conclusionSection))
            result.Metadata.TotalTokensEstimate += EstimateTokens(conclusionSection);
        if (!string.IsNullOrWhiteSpace(introductionSection))
            result.Metadata.TotalTokensEstimate += EstimateTokens(introductionSection);
        if (!string.IsNullOrWhiteSpace(methodologySection))
            result.Metadata.TotalTokensEstimate += EstimateTokens(methodologySection);

        // Stage 2: Identify missing fields
        var missingFields = IdentifyMissingFields(abstractExtraction);
        result.Metadata.MissingFields = missingFields;

        if (missingFields.Count == 0)
        {
            // Abstract has all fields - use abstract only
            result.MergedExtraction = abstractExtraction;
            result.Metadata.ConfidenceBreakdown.OverallConfidence = result.Metadata.ConfidenceBreakdown.AbstractConfidence;
            return result;
        }

        // Stage 3: Extract from targeted sections for missing fields
        var sectionExtraction = await ExtractMissingFieldsAsync(
            missingFields,
            discussionSection,
            conclusionSection,
            introductionSection,
            methodologySection,
            cancellationToken);

        if (sectionExtraction != null)
        {
            // Track which sections were used
            if (!string.IsNullOrWhiteSpace(discussionSection) && sectionExtraction.Limitations.Any())
            {
                result.Metadata.UsedDiscussion = true;
                result.Metadata.ConfidenceBreakdown.DiscussionConfidence = CalculateFieldConfidence(sectionExtraction);
            }
            if (!string.IsNullOrWhiteSpace(conclusionSection) && sectionExtraction.FutureWork.Any())
            {
                result.Metadata.UsedConclusion = true;
                result.Metadata.ConfidenceBreakdown.ConclusionConfidence = CalculateFieldConfidence(sectionExtraction);
            }
            if (!string.IsNullOrWhiteSpace(introductionSection))
                result.Metadata.UsedIntroduction = true;
            if (!string.IsNullOrWhiteSpace(methodologySection))
                result.Metadata.UsedMethodology = true;

            // Store section extraction
            result.SectionExtractions = new SectionExtractionsDto
            {
                Discussion = result.Metadata.UsedDiscussion ? sectionExtraction : null,
                Conclusion = result.Metadata.UsedConclusion ? sectionExtraction : null,
                Introduction = result.Metadata.UsedIntroduction ? abstractExtraction : null,
                Methodology = result.Metadata.UsedMethodology ? abstractExtraction : null
            };

            // Stage 4: Merge results
            result.MergedExtraction = MergeExtractions(abstractExtraction, sectionExtraction, missingFields);
        }
        else
        {
            // Fallback to abstract only if section extraction fails
            result.MergedExtraction = abstractExtraction;
        }

        // Calculate overall confidence
        result.Metadata.ConfidenceBreakdown.OverallConfidence = CalculateOverallConfidence(
            result.Metadata.ConfidenceBreakdown.AbstractConfidence,
            result.Metadata.ConfidenceBreakdown.DiscussionConfidence,
            result.Metadata.ConfidenceBreakdown.ConclusionConfidence,
            missingFields);

        // Set field-level confidence
        result.Metadata.ConfidenceBreakdown.FieldConfidence = new FieldConfidenceDto
        {
            MethodsConfidence = result.MergedExtraction.Methods.Any() ? 85 : 40,
            DatasetsConfidence = result.MergedExtraction.Datasets.Any() ? 85 : 40,
            LimitationsConfidence = result.MergedExtraction.Limitations.Any() ? 75 : 40,
            FutureWorkConfidence = result.MergedExtraction.FutureWork.Any() ? 75 : 40
        };

        return result;
    }

    public async Task<AiPaperExtractionDto?> ExtractMissingFieldsAsync(
        List<string> missingFields,
        string? discussionSection,
        string? conclusionSection,
        string? introductionSection,
        string? methodologySection,
        CancellationToken cancellationToken = default)
    {
        if (!missingFields.Any())
            return null;

        // Build prompt based on what fields are missing and which sections are available
        var sections = new List<string>();

        if (missingFields.Contains("limitations") && !string.IsNullOrWhiteSpace(discussionSection))
            sections.Add($"DISCUSSION SECTION:\n{discussionSection}");

        if (missingFields.Contains("future_work") && !string.IsNullOrWhiteSpace(conclusionSection))
            sections.Add($"CONCLUSION/FUTURE WORK SECTION:\n{conclusionSection}");

        if (missingFields.Contains("datasets") && !string.IsNullOrWhiteSpace(methodologySection))
            sections.Add($"METHODOLOGY SECTION:\n{methodologySection}");

        if (missingFields.Contains("methods") && !string.IsNullOrWhiteSpace(methodologySection))
            sections.Add($"METHODOLOGY SECTION:\n{methodologySection}");

        if (missingFields.Contains("research_problem") && !string.IsNullOrWhiteSpace(introductionSection))
            sections.Add($"INTRODUCTION SECTION:\n{introductionSection}");

        if (!sections.Any())
            return null;

        var fieldsStr = string.Join(", ", missingFields.Select(f => $"'{f}'"));
        var prompt = $@"
Analyze the following sections from an academic paper to extract the MISSING fields: {fieldsStr}

The abstract extraction already captured some information. Focus on extracting:
- Limitations: Look for explicit mentions of weaknesses, constraints, scalability issues in Discussion section
- Future Work: Look for authors' proposed next steps in Conclusion/Future Work section
- Datasets: Look for dataset names, benchmarks, data collection details in Methodology section
- Methods: Look for algorithm names, architecture details in Methodology section
- Research Problem: Look for problem statement in Introduction section

SECTIONS:
{string.Join("\n\n", sections)}

Return ONLY a valid JSON object with these fields (empty arrays if not found):
{{
  ""methods"": [""method 1"", ""method 2""],
  ""datasets"": [""dataset 1""],
  ""limitations"": [""limitation 1""],
  ""future_work"": [""future direction 1""],
  ""discussions"": [],
  ""conclusions"": [],
  ""research_problem"": ""problem statement"",
  ""metric"": """",
  ""contribution"": """"
}}";

        var textResult = await CallGroqApiAsync(prompt, cancellationToken);
        if (string.IsNullOrWhiteSpace(textResult)) return null;

        try
        {
            return JsonSerializer.Deserialize<AiPaperExtractionDto>(textResult,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Groq missing fields extraction result.");
            return null;
        }
    }

    private List<string> IdentifyMissingFields(AiPaperExtractionDto extraction)
    {
        var missing = new List<string>();

        if (!extraction.Methods.Any()) missing.Add("methods");
        if (!extraction.Datasets.Any()) missing.Add("datasets");
        if (!extraction.Limitations.Any()) missing.Add("limitations");
        if (!extraction.FutureWork.Any()) missing.Add("future_work");
        if (string.IsNullOrWhiteSpace(extraction.ResearchProblem)) missing.Add("research_problem");

        return missing;
    }

    private AiPaperExtractionDto MergeExtractions(
        AiPaperExtractionDto abstractResult,
        AiPaperExtractionDto sectionResult,
        List<string> missingFields)
    {
        var merged = new AiPaperExtractionDto
        {
            ResearchProblem = !string.IsNullOrWhiteSpace(abstractResult.ResearchProblem)
                ? abstractResult.ResearchProblem
                : sectionResult.ResearchProblem,
            Metric = !string.IsNullOrWhiteSpace(abstractResult.Metric)
                ? abstractResult.Metric
                : sectionResult.Metric,
            Contribution = !string.IsNullOrWhiteSpace(abstractResult.Contribution)
                ? abstractResult.Contribution
                : sectionResult.Contribution,
            Discussions = abstractResult.Discussions,
            Conclusions = abstractResult.Conclusions
        };

        // Merge methods - abstract first, then section (avoid duplicates)
        merged.Methods = abstractResult.Methods.Concat(sectionResult.Methods)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Merge datasets - abstract first, then section
        merged.Datasets = abstractResult.Datasets.Concat(sectionResult.Datasets)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // For limitations and future work - section (Discussion/Conclusion) has higher priority
        // since these are often not in abstract
        if (missingFields.Contains("limitations"))
        {
            merged.Limitations = sectionResult.Limitations.Any()
                ? sectionResult.Limitations.Concat(abstractResult.Limitations).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : abstractResult.Limitations;
        }
        else
        {
            merged.Limitations = abstractResult.Limitations;
        }

        if (missingFields.Contains("future_work"))
        {
            merged.FutureWork = sectionResult.FutureWork.Any()
                ? sectionResult.FutureWork.Concat(abstractResult.FutureWork).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : abstractResult.FutureWork;
        }
        else
        {
            merged.FutureWork = abstractResult.FutureWork;
        }

        return merged;
    }

    private int CalculateFieldConfidence(AiPaperExtractionDto extraction)
    {
        int score = 50;
        if (extraction.Methods.Any()) score += 10;
        if (extraction.Datasets.Any()) score += 10;
        if (extraction.Limitations.Any()) score += 10;
        if (extraction.FutureWork.Any()) score += 10;
        if (!string.IsNullOrWhiteSpace(extraction.ResearchProblem)) score += 5;
        if (!string.IsNullOrWhiteSpace(extraction.Metric)) score += 5;
        return Math.Min(score, 100);
    }

    private int CalculateOverallConfidence(int abstractConfidence, int discussionConfidence, int conclusionConfidence, List<string> missingFields)
    {
        if (!missingFields.Any())
            return abstractConfidence;

        double totalWeight = 0;
        double weightedSum = 0;

        // Abstract contributes to all fields
        totalWeight += 1.0;
        weightedSum += abstractConfidence;

        // Discussion helps with limitations
        if (missingFields.Contains("limitations") && discussionConfidence > 0)
        {
            totalWeight += 0.5;
            weightedSum += discussionConfidence * 0.5;
        }

        // Conclusion helps with future work
        if (missingFields.Contains("future_work") && conclusionConfidence > 0)
        {
            totalWeight += 0.5;
            weightedSum += conclusionConfidence * 0.5;
        }

        return (int)Math.Min(100, weightedSum / totalWeight * 1.1);
    }

    private int EstimateTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        return text.Length / 4;
    }

    public async Task<List<ResearchGapDto>> GenerateResearchGapsAsync(
        string topicName,
        PatternMiningResultDto patterns,
        GapTimelineDto timeline,
        List<PaperAnalysisDto> analyses,
        CancellationToken cancellationToken = default)
    {
        // Build paper context so AI can reference real paper IDs
        var paperContext = analyses.Take(50).Select((a, idx) =>
            $"[Paper {idx + 1}] ID={a.PaperId}, Title=\"{a.Title}\", Method=\"{a.Method}\", Dataset=\"{a.Dataset}\", Year={a.Year}").ToList();

        var prompt = $@"
You are an expert academic researcher analyzing research gaps in the field of '{topicName}'.

Based on the following evidence from {analyses.Count} papers:

PAPER CONTEXT (use these Paper IDs when listing supporting_paper_ids):
{string.Join("\n", paperContext)}

METHODS TRENDS:
{JsonSerializer.Serialize(patterns.Methods)}

DATASET TRENDS:
{JsonSerializer.Serialize(patterns.Datasets)}

LIMITATION PATTERNS:
{JsonSerializer.Serialize(patterns.Limitations)}

GAP TIMELINE:
{JsonSerializer.Serialize(timeline.Timeline)}

Identify exactly 5-7 DISTINCT and NON-OVERLAPPING research gaps. Each gap MUST be unique in its core theme. Avoid creating multiple gaps about the same underlying issue.

For each gap, return:
- gap_type: MUST be EXACTLY one of these values (no other text):
  * ""Dataset Gap""
  * ""Method Gap""
  * ""Evaluation Gap""
  * ""Application Gap""
  * ""Geographic Gap""
  * ""Temporal Gap""
  * ""Contradiction Gap""
- title: Concise gap title (max 200 chars)
- description: Detailed explanation citing specific papers/methods when relevant (min 100 chars)
- suggested_direction: Concrete, actionable research direction that would address this gap. Must NOT be empty. (min 80 chars)
- confidence: integer 0-100 (higher if supported by multiple papers)
- supporting_paper_ids: array of Paper IDs from the PAPER CONTEXT above that support this gap. Must reference real IDs from PAPER CONTEXT.
- evidence_count: integer equal to the number of supporting_paper_ids (or number of papers supporting this gap).

CRITICAL RULES:
1. gap_type MUST be exactly one of the 7 values listed above.
2. suggested_direction MUST be a non-empty concrete recommendation (e.g., ""Conduct user studies with diverse populations to evaluate X"").
3. Each gap must target a DIFFERENT research dimension. Do not create multiple ""dataset"" or ""evaluation"" gaps with overlapping themes.
4. Limit to 5-7 total gaps. Quality over quantity.

Return ONLY a valid JSON object:
{{
  ""gaps"": [
    {{
      ""title"": ""Gap Title"",
      ""description"": ""Description of the gap citing specific papers/methods"",
      ""gap_type"": ""Dataset Gap"",
      ""suggested_direction"": ""Concrete actionable research direction"",
      ""confidence"": 85,
      ""supporting_paper_ids"": [1, 5, 12],
      ""evidence_count"": 3
    }}
  ]
}}";

        var textResult = await CallGroqApiAsync(prompt, cancellationToken);
        if (string.IsNullOrWhiteSpace(textResult)) return new List<ResearchGapDto>();

        try
        {
            using var jsonDoc = JsonDocument.Parse(textResult);
            if (jsonDoc.RootElement.TryGetProperty("gaps", out var gapsElement))
            {
                var gaps = JsonSerializer.Deserialize<List<ResearchGapDto>>(gapsElement.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ResearchGapDto>();

                // Normalize gap_type and provide defaults for missing fields
                foreach (var gap in gaps)
                {
                    gap.GapType = NormalizeGapType(gap.GapType);
                    gap.SuggestedDirection = string.IsNullOrWhiteSpace(gap.SuggestedDirection)
                        ? $"Further research is needed to investigate {gap.Title.ToLowerInvariant()}."
                        : gap.SuggestedDirection;
                    if (gap.Confidence <= 0) gap.Confidence = 50;
                    if (gap.Confidence > 100) gap.Confidence = 100;
                }

                // Deduplicate gaps by gap_type + similar title to avoid AI generating overlapping gaps
                gaps = DeduplicateGaps(gaps);

                return gaps;
            }
            return new List<ResearchGapDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Groq research gap generation result.");
            return new List<ResearchGapDto>();
        }
    }

    private static string NormalizeGapType(string? rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType)) return GapTypes.Dataset;

        var cleaned = rawType.Trim();
        // Map common variations to canonical values
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["dataset"] = GapTypes.Dataset,
            ["data"] = GapTypes.Dataset,
            ["method"] = GapTypes.Method,
            ["methodology"] = GapTypes.Method,
            ["algorithm"] = GapTypes.Method,
            ["evaluation"] = GapTypes.Evaluation,
            ["metric"] = GapTypes.Evaluation,
            ["benchmark"] = GapTypes.Evaluation,
            ["application"] = GapTypes.Application,
            ["practical"] = GapTypes.Application,
            ["real-world"] = GapTypes.Application,
            ["geographic"] = GapTypes.Geographic,
            ["geographical"] = GapTypes.Geographic,
            ["regional"] = GapTypes.Geographic,
            ["temporal"] = GapTypes.Temporal,
            ["time"] = GapTypes.Temporal,
            ["contradiction"] = GapTypes.Contradiction,
            ["conflict"] = GapTypes.Contradiction,
            ["disagreement"] = GapTypes.Contradiction
        };

        var key = cleaned.ToLowerInvariant().Replace(" gap", "").Trim();
        return mappings.TryGetValue(key, out var mapped) ? mapped : cleaned;
    }

    private static List<ResearchGapDto> DeduplicateGaps(List<ResearchGapDto> gaps)
    {
        if (gaps.Count <= 1) return gaps;

        // Group by gap_type, keep the highest-confidence gap per type if titles are similar
        var deduped = new List<ResearchGapDto>();
        var seenSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Sort by confidence desc so we keep the strongest gap per cluster
        var ordered = gaps.OrderByDescending(g => g.Confidence).ToList();

        foreach (var gap in ordered)
        {
            // Build a signature: normalized title words (first 4 words) + gap_type
            var titleWords = (gap.Title ?? string.Empty)
                .ToLowerInvariant()
                .Split(new[] { ' ', ',', '.', ':', ';', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3)
                .Take(4)
                .OrderBy(w => w)
                .ToList();

            var signature = $"{gap.GapType}|{string.Join("_", titleWords)}";

            // If exact signature seen, skip
            if (seenSignatures.Contains(signature)) continue;

            // Check overlap with any existing deduped gap of same type
            var isDuplicate = deduped.Any(existing =>
                existing.GapType.Equals(gap.GapType, StringComparison.OrdinalIgnoreCase) &&
                TitleOverlap(existing.Title, gap.Title) > 0.6);

            if (!isDuplicate)
            {
                deduped.Add(gap);
                seenSignatures.Add(signature);
            }
        }

        return deduped;
    }

    private static double TitleOverlap(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0;
        var wordsA = a.ToLowerInvariant().Split(new[] { ' ', ',', '.', ':', ';', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3).ToHashSet();
        var wordsB = b.ToLowerInvariant().Split(new[] { ' ', ',', '.', ':', ';', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3).ToHashSet();
        if (!wordsA.Any() || !wordsB.Any()) return 0;
        var intersect = wordsA.Intersect(wordsB).Count();
        var union = wordsA.Union(wordsB).Count();
        return union == 0 ? 0 : (double)intersect / union;
    }

    public async Task<AiPaperExtractionDto> InferLimitationsAndFutureWorkAsync(
        string paperTitle,
        string abstractText,
        List<string> methods,
        List<string> datasets,
        CancellationToken cancellationToken = default)
    {
        var methodsStr = methods.Any() ? string.Join(", ", methods) : "unknown";
        var datasetsStr = datasets.Any() ? string.Join(", ", datasets) : "unknown";

        var prompt = $@"
You are analyzing an academic paper to identify its limitations and potential future research directions.
Do NOT simply repeat what's in the abstract. You must critically analyze and infer beyond what is explicitly stated.

Paper Title: {paperTitle}
Abstract: {abstractText}
Methods Used: {methodsStr}
Datasets Used: {datasetsStr}

Based on the paper's methodology, experimental setup, and contribution, infer:
1. 'limitations': Potential weaknesses, constraints, or areas the paper could improve (e.g., narrow evaluation, missing baselines, scalability issues). Each limitation should be insightful, not generic.
2. 'future_work': Concrete and actionable future research directions that could build upon this work. Must be specific to this paper's domain and contribution.

Return ONLY a valid JSON object matching this structure (add [AI Inferred] suffix to each item to mark as inferred):
{{
  ""limitations"": [""Limitation 1 [AI Inferred]"", ""Limitation 2 [AI Inferred]""],
  ""future_work"": [""Future direction 1 [AI Inferred]"", ""Future direction 2 [AI Inferred]""]
}}";

        var textResult = await CallGroqApiAsync(prompt, cancellationToken);
        if (string.IsNullOrWhiteSpace(textResult))
            return new AiPaperExtractionDto { Limitations = [], FutureWork = [] };

        try
        {
            return JsonSerializer.Deserialize<AiPaperExtractionDto>(textResult, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new AiPaperExtractionDto { Limitations = [], FutureWork = [] };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Groq inference result.");
            return new AiPaperExtractionDto { Limitations = [], FutureWork = [] };
        }
    }

    public async Task<List<AiOpportunityDto>> SummarizeOpportunitiesAsync(string topicName, List<string> futureWorks, CancellationToken cancellationToken = default)
    {
        if (!futureWorks.Any())
            return new List<AiOpportunityDto>();

        var prompt = $@"
Review the following list of 'future work' excerpts extracted from recent research papers in the topic '{topicName}'.

{JsonSerializer.Serialize(futureWorks)}

Analyze these texts and synthesize them into exactly 3 distinct research opportunities or open challenges.
Return ONLY a valid JSON object matching this exact structure:
{{
  ""opportunities"": [
    {{
      ""title"": ""Opportunity Title [🤖 AI]"",
      ""description"": ""Explanation..."",
      ""source_indices"": []
    }}
  ]
}}";

        var textResult = await CallGroqApiAsync(prompt, cancellationToken);
        if (string.IsNullOrWhiteSpace(textResult)) return new List<AiOpportunityDto>();

        try
        {
            using var jsonDoc = JsonDocument.Parse(textResult);
            if (jsonDoc.RootElement.TryGetProperty("opportunities", out var oppsElement))
            {
                var result = JsonSerializer.Deserialize<List<AiOpportunityDto>>(oppsElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return result ?? new List<AiOpportunityDto>();
            }
            return new List<AiOpportunityDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Groq summarize opportunities result.");
            return new List<AiOpportunityDto>();
        }
    }

    public async Task<AiTopicFallbackDto?> GenerateFallbackInsightsAsync(string topicName, bool needMethods, bool needDatasets, bool needOpportunities, CancellationToken cancellationToken = default)
    {
        if (!needMethods && !needDatasets && !needOpportunities)
            return null;

        var requirements = new List<string>();
        if (needMethods) requirements.Add("- methods: An array of 5 emerging or popular methodologies in this field (each with ' [AI Inferred]' suffix).");
        if (needDatasets) requirements.Add("- datasets: An array of 5 commonly used dataset types or domains in this field (each with ' [AI Inferred]' suffix).");
        if (needOpportunities) requirements.Add("- opportunities: An array of 3 top research opportunities/open challenges (each having 'title', 'description', and 'source_indices' as an empty array []).");

        var reqStr = string.Join("\n", requirements);

        var prompt = $@"
You are given the research topic: '{topicName}'.
Based on your general knowledge of this topic, provide the following missing insights:

{reqStr}

Return ONLY a valid JSON object matching this structure exactly:
{{
  ""methods"": [""method 1 [AI Inferred]"", ""method 2 [AI Inferred]""],
  ""datasets"": [""dataset 1 [AI Inferred]""],
  ""opportunities"": [
    {{
      ""title"": ""Opportunity Title [🤖 AI]"",
      ""description"": ""Explanation of the opportunity..."",
      ""source_indices"": []
    }}
  ]
}}";

        var textResult = await CallGroqApiAsync(prompt, cancellationToken);
        if (string.IsNullOrWhiteSpace(textResult)) return null;

        try
        {
            return JsonSerializer.Deserialize<AiTopicFallbackDto>(textResult, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Groq fallback insights.");
            return null;
        }
    }
}
