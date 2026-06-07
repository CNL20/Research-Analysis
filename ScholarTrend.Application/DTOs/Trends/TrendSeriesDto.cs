namespace ScholarTrend.Application.DTOs.Trends;

public class TrendSeriesDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<TrendDataPointDto> DataPoints { get; set; } = [];
}
