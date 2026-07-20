namespace ScholarTrend.Application.DTOs.GapAnalysis;

public class PatternMiningResultDto
{
    public int TopicId { get; set; }
    public string TopicName { get; set; } = string.Empty;
    public List<MethodPatternDto> Methods { get; set; } = [];
    public List<DatasetPatternDto> Datasets { get; set; } = [];
    public List<LimitationPatternDto> Limitations { get; set; } = [];
    public DateTime MinedAt { get; set; }
}

public class MethodPatternDto
{
    public string MethodName { get; set; } = string.Empty;
    public int PaperCount { get; set; }
    public int Year { get; set; }
    public double GrowthRate { get; set; }
    public string Trend { get; set; } = "stable";
}

public class DatasetPatternDto
{
    public string DatasetName { get; set; } = string.Empty;
    public int PaperCount { get; set; }
    public int Year { get; set; }
    public double GrowthRate { get; set; }
    public string Trend { get; set; } = "stable";
}

public class LimitationPatternDto
{
    public string LimitationText { get; set; } = string.Empty;
    public int PaperCount { get; set; }
    public int Year { get; set; }
    public double GrowthRate { get; set; }
    public string Trend { get; set; } = "stable";
}
