namespace ScholarTrend.Application.DTOs.Files;

public class FileUploadResultDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? PaperId { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
