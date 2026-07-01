namespace ScholarTrend.Domain.Entities;

public class PaymentTransaction
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int? SubscriptionId { get; set; }
    public int PlanId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ProviderTransactionId { get; set; } = string.Empty;
    public string? ProviderResponse { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public User User { get; set; } = null!;
    public SubscriptionPlan Plan { get; set; } = null!;
    public Subscription? Subscription { get; set; }
}
