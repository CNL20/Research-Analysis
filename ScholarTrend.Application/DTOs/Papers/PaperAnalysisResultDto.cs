using System.Text.Json.Serialization;

namespace ScholarTrend.Application.DTOs.Papers;

public class PaperAnalysisResultDto
{
    [JsonPropertyName("paper_id")]
    public int PaperId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("limitations")]
    public List<string> Limitations { get; set; } = [];

    [JsonPropertyName("future_work")]
    public List<string> FutureWork { get; set; } = [];

    [JsonPropertyName("was_inferred")]
    public bool WasInferred { get; set; }
}
