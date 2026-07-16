namespace ScholarTrend.Application.DTOs.Auth;

public class UserFilterRequest
{
    public string? Search { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
