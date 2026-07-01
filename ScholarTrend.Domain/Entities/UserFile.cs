namespace ScholarTrend.Domain.Entities;

public class UserFile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? PaperId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    public User User { get; set; } = null!;
    public ResearchPaper? Paper { get; set; }
}
