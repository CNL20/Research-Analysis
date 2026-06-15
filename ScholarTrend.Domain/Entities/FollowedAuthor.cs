namespace ScholarTrend.Domain.Entities;

public class FollowedAuthor
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int AuthorId { get; set; }
    public DateTime FollowedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public Author Author { get; set; } = null!;
}
