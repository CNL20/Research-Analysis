using System.Text.Json.Serialization;

namespace ScholarTrend.Application.DTOs.TopicInsights;

public class AiPaperExtractionDto
{
    [JsonPropertyName("methods")]
    public List<string> Methods { get; set; } = [];

    [JsonPropertyName("datasets")]
    public List<string> Datasets { get; set; } = [];

    [JsonPropertyName("limitations")]
    public List<string> Limitations { get; set; } = [];

    [JsonPropertyName("future_work")]
    public List<string> FutureWork { get; set; } = [];

    [JsonPropertyName("discussions")]
    public List<string> Discussions { get; set; } = [];

    [JsonPropertyName("conclusions")]
    public List<string> Conclusions { get; set; } = [];

    [JsonPropertyName("research_problem")]
    public string? ResearchProblem { get; set; }

    [JsonPropertyName("metric")]
    public string? Metric { get; set; }

    [JsonPropertyName("contribution")]
    public string? Contribution { get; set; }
}
