using ScholarTrend.Application.DTOs.Payment;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.External;

public interface IPaymentProvider
{
    Task<PaymentLinkDto> CreatePaymentLinkAsync(ScholarTrend.Domain.Entities.PaymentTransaction transaction, string cancelUrl, string returnUrl);
    Task<PaymentWebhookResponseDto> VerifyWebhookAsync(object webhookBody);
}
