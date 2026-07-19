using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.GapAnalysis;
using ScholarTrend.Application.DTOs.TopicInsights;
using ScholarTrend.Application.Interfaces.External;

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

    public async Task<List<ResearchGapDto>> GenerateResearchGapsAsync(
        string topicName,
        PatternMiningResultDto patterns,
        GapTimelineDto timeline,
        List<PaperAnalysisDto> analyses,
        CancellationToken cancellationToken = default)
    {
        var prompt = $@"
You are an expert academic researcher analyzing research gaps in the field of '{topicName}'.

Based on the following evidence from {analyses.Count} papers:

METHODS TRENDS:
{JsonSerializer.Serialize(patterns.Methods)}

DATASET TRENDS:
{JsonSerializer.Serialize(patterns.Datasets)}

LIMITATION PATTERNS:
{JsonSerializer.Serialize(patterns.Limitations)}

GAP TIMELINE:
{JsonSerializer.Serialize(timeline.Timeline)}

Identify exactly 5-7 research gaps with the following structure:
- gap_type: Dataset Gap | Method Gap | Evaluation Gap | Application Gap | Geographic Gap | Temporal Gap | Contradiction Gap
- title: Concise gap title
- description: Detailed explanation
- suggested_direction: What research should address this gap
- confidence: 0-100 (higher if supported by multiple papers)
- evidence_count: Number of papers supporting this gap

Return ONLY a valid JSON object:
{{
  ""gaps"": [
    {{
      ""title"": ""Gap Title"",
      ""description"": ""Description of the gap"",
      ""gap_type"": ""Dataset Gap"",
      ""suggested_direction"": ""Suggested research direction"",
      ""confidence"": 85,
      ""evidence_count"": 12
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
