using ScholarTrend.Application.DTOs.Payment;

namespace ScholarTrend.Application.Interfaces.Services;

public interface IPaymentService
{
    Task<PaymentLinkDto> CreateCheckoutUrlAsync(string userId, int planId, string cancelUrl, string returnUrl);
    Task<bool> HandleWebhookAsync(object webhookBody);
    Task<List<TransactionHistoryDto>> GetUserTransactionHistoryAsync(string userId);
}
