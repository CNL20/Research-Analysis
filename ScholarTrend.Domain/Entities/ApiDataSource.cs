namespace ScholarTrend.Domain.Entities;
public class ApiDataSource
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncAt { get; set; }
}