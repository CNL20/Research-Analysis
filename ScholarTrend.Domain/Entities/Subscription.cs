namespace ScholarTrend.Domain.Entities;

public class Subscription
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int PlanId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? TrialEndDate { get; set; }
    public bool AutoRenew { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public SubscriptionPlan Plan { get; set; } = null!;
    public ICollection<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();
}
