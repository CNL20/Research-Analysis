using Microsoft.Extensions.Configuration;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using ScholarTrend.Application.DTOs.Payment;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Services;

public class PayOSPaymentProvider : IPaymentProvider
{
    private readonly PayOSClient _payOS;

    public PayOSPaymentProvider(IConfiguration configuration)
    {
        var clientId = configuration["PayOS:ClientId"] ?? throw new ArgumentNullException("PayOS ClientId is missing");
        var apiKey = configuration["PayOS:ApiKey"] ?? throw new ArgumentNullException("PayOS ApiKey is missing");
        var checksumKey = configuration["PayOS:ChecksumKey"] ?? throw new ArgumentNullException("PayOS ChecksumKey is missing");

        _payOS = new PayOSClient(clientId, apiKey, checksumKey);
    }

    public async Task<PaymentLinkDto> CreatePaymentLinkAsync(ScholarTrend.Domain.Entities.PaymentTransaction transaction, string cancelUrl, string returnUrl)
    {
        var items = new List<PaymentLinkItem> 
        { 
            new PaymentLinkItem { Name = $"Plan: {transaction.Plan.Name}", Quantity = 1, Price = (int)transaction.Amount } 
        };

        // Order code must be unique integer up to 53 bits.
        // We use timestamp + transaction Id to ensure uniqueness
        long orderCode = long.Parse(DateTimeOffset.Now.ToString("yyMMddHHmmss") + transaction.Id.ToString("D2"));

        var createPaymentReq = new CreatePaymentLinkRequest 
        {
            OrderCode = orderCode,
            Amount = (int)transaction.Amount,
            Description = $"Order {orderCode}", // Maximum 25 chars required by PayOS
            Items = items,
            CancelUrl = cancelUrl,
            ReturnUrl = returnUrl
        };

        var createPaymentResult = await _payOS.PaymentRequests.CreateAsync(createPaymentReq);

        return new PaymentLinkDto
        {
            CheckoutUrl = createPaymentResult.CheckoutUrl,
            PaymentLinkId = orderCode.ToString() // We pass our local unique orderCode back
        };
    }

    public async Task<PaymentWebhookResponseDto> VerifyWebhookAsync(object webhookBody)
    {
        try
        {
            var body = webhookBody as Webhook;
            if (body == null) return new PaymentWebhookResponseDto { IsValid = false };

            var webhookData = await _payOS.Webhooks.VerifyAsync(body);
            if (webhookData == null || webhookData.Code != "00") return new PaymentWebhookResponseDto { IsValid = false };

            return new PaymentWebhookResponseDto
            {
                IsValid = true,
                OrderCode = webhookData.OrderCode,
                Amount = (int)webhookData.Amount,
                Description = webhookData.Description
            };
        }
        catch (Exception)
        {
            return new PaymentWebhookResponseDto { IsValid = false };
        }
    }
}
