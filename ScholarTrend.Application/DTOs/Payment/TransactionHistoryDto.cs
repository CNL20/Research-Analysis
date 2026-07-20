namespace ScholarTrend.Application.DTOs.Payment;

public class TransactionHistoryDto
{
    public int TransactionId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
