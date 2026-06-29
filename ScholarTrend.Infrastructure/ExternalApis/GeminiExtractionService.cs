using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.TopicInsights;
using ScholarTrend.Application.Interfaces.External;

namespace ScholarTrend.Infrastructure.ExternalApis;

public class GeminiExtractionService : IAiExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiExtractionService> _logger;

    public GeminiExtractionService(HttpClient httpClient, IConfiguration config, ILogger<GeminiExtractionService> logger)
    {
        _httpClient = httpClient;
        _apiKey = (config["GeminiAI:ApiKey"] ?? string.Empty).Trim();
        _logger = logger;
    }

    public async Task<AiPaperExtractionDto?> ExtractFromAbstractAsync(string abstractText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("Gemini API Key is missing. Skipping extraction.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(abstractText))
            return null;

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-latest:generateContent?key={_apiKey}";

        var prompt = $@"
You are an expert AI academic researcher. 
Read the following paper abstract and extract the core methodologies (methods), datasets used (if any), limitations mentioned, and future work/research directions proposed.

Abstract:
""""""{abstractText}""""""

Return ONLY a valid JSON object matching the following structure exactly (without Markdown blocks like ```json). If a field has no information, return an empty array [].

{{
  ""methods"": [""method 1"", ""method 2""],
  ""datasets"": [""dataset 1""],
  ""limitations"": [""limitation 1""],
  ""future_work"": [""future direction 1""]
}}
";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                responseMimeType = "application/json"
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseData = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var textResult = responseData
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(textResult))
                return null;

            // Clean up markdown if Gemini ignores the prompt
            textResult = textResult.Trim();
            if (textResult.StartsWith("```json"))
                textResult = textResult.Substring(7);
            if (textResult.StartsWith("```"))
                textResult = textResult.Substring(3);
            if (textResult.EndsWith("```"))
                textResult = textResult.Substring(0, textResult.Length - 3);
            
            textResult = textResult.Trim();

            var result = JsonSerializer.Deserialize<AiPaperExtractionDto>(textResult, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract info using Gemini AI.");
            return null;
        }
    }

    public async Task<List<AiOpportunityDto>> SummarizeOpportunitiesAsync(string topicName, List<string> futureWorks, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || !futureWorks.Any())
            return new List<AiOpportunityDto>();

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-latest:generateContent?key={_apiKey}";

        // We pass the index so the AI can tell us which snippet it used as evidence
        var inputList = string.Join("\n", futureWorks.Select((fw, index) => $"[{index}] {fw}"));

        var prompt = $@"
You are an expert AI academic researcher. 
Review the following list of 'future work' excerpts extracted from recent research papers in the topic '{topicName}'.
Synthesize them and identify the top 2-3 most important 'Research Opportunities' (Open Challenges).
For each opportunity, provide:
- title: A concise title.
- description: A brief explanation of what needs to be solved.
- source_indices: An array of integers containing the [index] of the excerpts you used to form this opportunity.

Excerpts:
""""""{inputList}""""""

Return ONLY a valid JSON array matching this structure exactly (without Markdown blocks like ```json):
[
  {{
    ""title"": ""Opportunity Title"",
    ""description"": ""Explanation..."",
    ""source_indices"": [0, 2]
  }}
]
";

        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.2, responseMimeType = "application/json" }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseData = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var textResult = responseData.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();

            if (string.IsNullOrWhiteSpace(textResult))
                return new List<AiOpportunityDto>();

            // Clean up markdown if Gemini ignores the prompt
            textResult = textResult.Trim();
            if (textResult.StartsWith("```json"))
                textResult = textResult.Substring(7);
            if (textResult.StartsWith("```"))
                textResult = textResult.Substring(3);
            if (textResult.EndsWith("```"))
                textResult = textResult.Substring(0, textResult.Length - 3);
            
            textResult = textResult.Trim();

            var result = JsonSerializer.Deserialize<List<AiOpportunityDto>>(textResult, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result ?? new List<AiOpportunityDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to summarize opportunities using Gemini AI.");
            return new List<AiOpportunityDto>();
        }
    }
}
