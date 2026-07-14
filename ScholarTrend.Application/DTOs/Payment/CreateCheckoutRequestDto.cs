namespace ScholarTrend.Application.DTOs.Payment;

public class CreateCheckoutRequestDto
{
    public int PlanId { get; set; }
    public string CancelUrl { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
}
