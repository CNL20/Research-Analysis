using System.Text.Json.Serialization;

namespace ScholarTrend.Application.DTOs.TopicInsights;

public class AiOpportunityDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("source_indices")]
    public List<int> SourceIndices { get; set; } = [];
}
