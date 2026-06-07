namespace ScholarTrend.Application.DTOs.Auth;

public class UpdateProfileRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? Institution { get; set; }
    public string? ResearchField { get; set; }
}
