namespace ScholarTrend.Application.DTOs.Auth;

public class UserFilterRequest
{
    public string? Search { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}
