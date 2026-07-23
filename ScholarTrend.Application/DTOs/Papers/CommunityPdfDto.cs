namespace ScholarTrend.Application.DTOs.Papers;

public class CommunityPdfDto
{
    public int FileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string UploadedByFullName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}
