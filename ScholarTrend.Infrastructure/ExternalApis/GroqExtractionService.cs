using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
3. 'future_works': Limitations, future works, or open challenges mentioned.

Text:
{abstractText}

Return ONLY a valid JSON object matching this structure:
{{
  ""methods"": [""method 1"", ""method 2""],
  ""datasets"": [""dataset 1""],
  ""future_works"": [""future direction 1""]
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
