namespace ScholarTrend.Domain.Entities;

public class SubscriptionPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string TargetRole { get; set; } = string.Empty;
    public decimal PriceVND { get; set; }
    public decimal PriceUSD { get; set; }
    public int DurationDays { get; set; }
    public int TrialDays { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
