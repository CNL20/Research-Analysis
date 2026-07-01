using Microsoft.AspNetCore.Identity;

namespace ScholarTrend.Domain.Entities;

public class User : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? Institution { get; set; }
    public string? ResearchField { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Bookmark> Bookmarks { get; set; } = [];
    public ICollection<FollowedTopic> FollowedTopics { get; set; } = [];
    public ICollection<FollowedJournal> FollowedJournals { get; set; } = [];
    public ICollection<FollowedAuthor> FollowedAuthors { get; set; } = [];
    public ICollection<FollowedPaper> FollowedPapers { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public ICollection<SearchHistory> SearchHistories { get; set; } = [];
    public NotificationSetting? NotificationSetting { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = [];
    public ICollection<Subscription> Subscriptions { get; set; } = [];
    public ICollection<Invoice> Invoices { get; set; } = [];
}