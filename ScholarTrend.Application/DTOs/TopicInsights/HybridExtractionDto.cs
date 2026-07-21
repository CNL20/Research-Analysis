using System.Text.Json.Serialization;

namespace ScholarTrend.Application.DTOs.TopicInsights;

public class HybridExtractionResultDto
{
    [JsonPropertyName("primary_extraction")]
    public AiPaperExtractionDto PrimaryExtraction { get; set; } = new();

    [JsonPropertyName("section_extractions")]
    public SectionExtractionsDto SectionExtractions { get; set; } = new();

    [JsonPropertyName("merged_extraction")]
    public AiPaperExtractionDto MergedExtraction { get; set; } = new();

    [JsonPropertyName("extraction_metadata")]
    public ExtractionMetadataDto Metadata { get; set; } = new();
}

public class SectionExtractionsDto
{
    [JsonPropertyName("discussion")]
    public AiPaperExtractionDto? Discussion { get; set; }

    [JsonPropertyName("conclusion")]
    public AiPaperExtractionDto? Conclusion { get; set; }

    [JsonPropertyName("introduction")]
    public AiPaperExtractionDto? Introduction { get; set; }

    [JsonPropertyName("methodology")]
    public AiPaperExtractionDto? Methodology { get; set; }
}

public class ExtractionMetadataDto
{
    [JsonPropertyName("used_abstract")]
    public bool UsedAbstract { get; set; }

    [JsonPropertyName("used_discussion")]
    public bool UsedDiscussion { get; set; }

    [JsonPropertyName("used_conclusion")]
    public bool UsedConclusion { get; set; }

    [JsonPropertyName("used_introduction")]
    public bool UsedIntroduction { get; set; }

    [JsonPropertyName("used_methodology")]
    public bool UsedMethodology { get; set; }

    [JsonPropertyName("total_tokens_estimate")]
    public int TotalTokensEstimate { get; set; }

    [JsonPropertyName("confidence_breakdown")]
    public ConfidenceBreakdownDto ConfidenceBreakdown { get; set; } = new();

    [JsonPropertyName("missing_fields")]
    public List<string> MissingFields { get; set; } = new();

    [JsonPropertyName("extraction_timestamp")]
    public DateTime ExtractionTimestamp { get; set; } = DateTime.UtcNow;
}

public class ConfidenceBreakdownDto
{
    [JsonPropertyName("abstract_confidence")]
    public int AbstractConfidence { get; set; }

    [JsonPropertyName("discussion_confidence")]
    public int DiscussionConfidence { get; set; }

    [JsonPropertyName("conclusion_confidence")]
    public int ConclusionConfidence { get; set; }

    [JsonPropertyName("overall_confidence")]
    public int OverallConfidence { get; set; }

    [JsonPropertyName("field_confidence")]
    public FieldConfidenceDto FieldConfidence { get; set; } = new();
}

public class FieldConfidenceDto
{
    [JsonPropertyName("methods")]
    public int MethodsConfidence { get; set; }

    [JsonPropertyName("datasets")]
    public int DatasetsConfidence { get; set; }

    [JsonPropertyName("limitations")]
    public int LimitationsConfidence { get; set; }

    [JsonPropertyName("future_work")]
    public int FutureWorkConfidence { get; set; }
}

public class ExtractedSectionsDto
{
    public string Introduction { get; set; } = string.Empty;
    public string Methodology { get; set; } = string.Empty;
    public string Experiments { get; set; } = string.Empty;
    public string Discussion { get; set; } = string.Empty;
    public string Conclusion { get; set; } = string.Empty;

    public bool HasAnySection => !string.IsNullOrWhiteSpace(Introduction)
        || !string.IsNullOrWhiteSpace(Methodology)
        || !string.IsNullOrWhiteSpace(Experiments)
        || !string.IsNullOrWhiteSpace(Discussion)
        || !string.IsNullOrWhiteSpace(Conclusion);

    public int EstimatedTokens
    {
        get
        {
            var total = (Introduction?.Length ?? 0)
                + (Methodology?.Length ?? 0)
                + (Experiments?.Length ?? 0)
                + (Discussion?.Length ?? 0)
                + (Conclusion?.Length ?? 0);
            return total / 4;
        }
    }
}
