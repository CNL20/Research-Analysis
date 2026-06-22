namespace ScholarTrend.Application.DTOs.Aggregation;

public class PaperAggregateResultDto
{
    public string Doi { get; set; } = string.Empty;
    public string MatchMethod { get; set; } = "doi";
    public int? InternalPaperId { get; set; }
    public List<string> SourcesAttempted { get; set; } = [];
    public List<string> SourcesMatched { get; set; } = [];
    public Dictionary<string, PaperSourceMetadataDto> Sources { get; set; } = new();
    public PaperSourceMetadataDto UnifiedMetadata { get; set; } = new();
    public Dictionary<string, FieldAuthorityDto> FieldAuthority { get; set; } = new();
    public Dictionary<string, FieldCoverageDto> Coverage { get; set; } = new();
    public List<DataGapDto> DataGaps { get; set; } = [];
    public List<FieldConflictDto> Conflicts { get; set; } = [];
    public int CompletenessScore { get; set; }
    public int TrustScore { get; set; }
    public string ConfidenceLevel { get; set; } = "low";
    public List<string> Recommendations { get; set; } = [];
    public DateTime AggregatedAt { get; set; } = DateTime.UtcNow;
}

public class FieldAuthorityDto
{
    public string Field { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string ChosenFrom { get; set; } = string.Empty;
    public List<string> Alternatives { get; set; } = [];
}

public class FieldCoverageDto
{
    public string Field { get; set; } = string.Empty;
    public int SourceCount { get; set; }
    public int TotalSources { get; set; }
    public double CoveragePercent { get; set; }
    public List<string> PresentIn { get; set; } = [];
    public List<string> MissingIn { get; set; } = [];
}

public class DataGapDto
{
    public string Field { get; set; } = string.Empty;
    public double CoveragePercent { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class FieldConflictDto
{
    public string Field { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, string?> Values { get; set; } = new();
    public string? Note { get; set; }
}
