namespace ScholarTrend.Application.DTOs.Payment;

public class PaymentWebhookResponseDto
{
    public bool IsValid { get; set; }
    public long OrderCode { get; set; }
    public int Amount { get; set; }
    public string Description { get; set; } = string.Empty;
}
