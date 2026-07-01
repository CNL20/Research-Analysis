namespace ScholarTrend.Application.DTOs.Files;

public class FileListQuery
{
    public string? Category { get; set; }
    public string? UserId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
