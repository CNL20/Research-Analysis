namespace ScholarTrend.Domain.Entities;

public class PaymentWebhookLog
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string RawPayload { get; set; } = string.Empty;
    public string? Signature { get; set; }
    public bool SignatureValid { get; set; }
    public bool Processed { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
