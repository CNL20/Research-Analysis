using ScholarTrend.Application.DTOs.Payment;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Interfaces.Services;
using ScholarTrend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ScholarTrend.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentProvider _paymentProvider;
    private readonly ISubscriptionService _subscriptionService;

    public PaymentService(
        IUnitOfWork unitOfWork,
        IPaymentProvider paymentProvider,
        ISubscriptionService subscriptionService)
    {
        _unitOfWork = unitOfWork;
        _paymentProvider = paymentProvider;
        _subscriptionService = subscriptionService;
    }

    public async Task<PaymentLinkDto> CreateCheckoutUrlAsync(string userId, int planId, string cancelUrl, string returnUrl)
    {
        var plan = await _unitOfWork.Context.Set<SubscriptionPlan>().FindAsync(planId);
        if (plan == null) throw new ArgumentException("Plan not found");

        var transaction = new PaymentTransaction
        {
            UserId = userId,
            PlanId = planId,
            Provider = "PayOS",
            Currency = "VND",
            Amount = plan.PriceVND,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Context.Set<PaymentTransaction>().AddAsync(transaction);
        await _unitOfWork.SaveChangesAsync(); // Save to generate Id

        // Load plan reference for provider
        transaction.Plan = plan;

        var result = await _paymentProvider.CreatePaymentLinkAsync(transaction, cancelUrl, returnUrl);
        
        transaction.ProviderTransactionId = result.PaymentLinkId;
        await _unitOfWork.SaveChangesAsync();

        return result;
    }

    public async Task<bool> HandleWebhookAsync(object webhookBody)
    {
        // Log webhook for tracing
        var log = new PaymentWebhookLog
        {
            Provider = "PayOS",
            RawPayload = System.Text.Json.JsonSerializer.Serialize(webhookBody),
            ReceivedAt = DateTime.UtcNow
        };
        await _unitOfWork.Context.Set<PaymentWebhookLog>().AddAsync(log);
        await _unitOfWork.SaveChangesAsync(); // save log immediately

        var verificationResult = await _paymentProvider.VerifyWebhookAsync(webhookBody);
        if (!verificationResult.IsValid)
        {
            return false;
        }

        var orderCodeStr = verificationResult.OrderCode.ToString();
        var transaction = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            _unitOfWork.Context.Set<PaymentTransaction>(),
            t => t.ProviderTransactionId == orderCodeStr);

        if (transaction == null) return false;

        // Idempotency check: only process Pending transactions
        if (transaction.Status == "Paid")
        {
            return true; // Already processed
        }

        if (transaction.Amount == verificationResult.Amount)
        {
            transaction.Status = "Paid";
            transaction.CompletedAt = DateTime.UtcNow;

            await _subscriptionService.ActivateSubscriptionAsync(transaction.UserId, transaction.PlanId);
            
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        return false;
    }

    public async Task<List<TransactionHistoryDto>> GetUserTransactionHistoryAsync(string userId)
    {
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            _unitOfWork.Context.Set<PaymentTransaction>()
                .Include(p => p.Plan)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TransactionHistoryDto
                {
                    TransactionId = t.Id,
                    PlanName = t.Plan != null ? t.Plan.Name : "Unknown",
                    Amount = t.Amount,
                    Currency = t.Currency,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt,
                    CompletedAt = t.CompletedAt
                })
        );
    }
}
