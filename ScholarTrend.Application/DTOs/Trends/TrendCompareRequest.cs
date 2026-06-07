namespace ScholarTrend.Application.DTOs.Trends;

public class TrendCompareRequest
{
    public string Type { get; set; } = "keyword";
    public List<int> Ids { get; set; } = [];
    public TrendFilterRequest? Filter { get; set; }
}
