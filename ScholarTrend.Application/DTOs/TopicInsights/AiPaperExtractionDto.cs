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
}
