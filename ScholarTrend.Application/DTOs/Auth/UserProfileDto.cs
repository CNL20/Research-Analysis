namespace ScholarTrend.Application.DTOs.Auth;

public class UserProfileDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Institution { get; set; }
    public string? ResearchField { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<string> Roles { get; set; } = [];
    public string? CurrentPlanName { get; set; }
    public DateTime? SubscriptionEndDate { get; set; }
}
