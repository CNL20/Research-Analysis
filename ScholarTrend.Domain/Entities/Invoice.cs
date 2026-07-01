namespace ScholarTrend.Domain.Entities;

public class Invoice
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public int TransactionId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public string? PdfUrl { get; set; }

    public User User { get; set; } = null!;
    public PaymentTransaction Transaction { get; set; } = null!;
}
