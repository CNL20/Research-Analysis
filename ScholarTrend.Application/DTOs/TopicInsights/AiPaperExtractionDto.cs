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

    [JsonIgnore]
    public string? ResearchProblem { get; set; }

    [JsonPropertyName("research_problem")]
    public System.Text.Json.JsonElement? ResearchProblemElement 
    { 
        get => null; 
        set 
        {
            if (value.HasValue) 
            {
                var el = value.Value;
                if (el.ValueKind == System.Text.Json.JsonValueKind.String) 
                    ResearchProblem = el.GetString();
                else if (el.ValueKind == System.Text.Json.JsonValueKind.Array) 
                    ResearchProblem = string.Join(", ", el.EnumerateArray().Select(e => e.GetString()));
                else 
                    ResearchProblem = el.GetRawText();
            }
        }
    }

    [JsonIgnore]
    public string? Metric { get; set; }

    [JsonPropertyName("metric")]
    public System.Text.Json.JsonElement? MetricElement 
    { 
        get => null; 
        set 
        {
            if (value.HasValue) 
            {
                var el = value.Value;
                if (el.ValueKind == System.Text.Json.JsonValueKind.String) 
                    Metric = el.GetString();
                else if (el.ValueKind == System.Text.Json.JsonValueKind.Array) 
                    Metric = string.Join(", ", el.EnumerateArray().Select(e => e.GetString()));
                else 
                    Metric = el.GetRawText();
            }
        }
    }

    [JsonIgnore]
    public string? Contribution { get; set; }

    [JsonPropertyName("contribution")]
    public System.Text.Json.JsonElement? ContributionElement 
    { 
        get => null; 
        set 
        {
            if (value.HasValue) 
            {
                var el = value.Value;
                if (el.ValueKind == System.Text.Json.JsonValueKind.String) 
                    Contribution = el.GetString();
                else if (el.ValueKind == System.Text.Json.JsonValueKind.Array) 
                    Contribution = string.Join(", ", el.EnumerateArray().Select(e => e.GetString()));
                else 
                    Contribution = el.GetRawText();
            }
        }
    }
}
