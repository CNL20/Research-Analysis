using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Services;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IUnitOfWork _unitOfWork;

    public SubscriptionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<SubscriptionPlan>> GetPlansAsync()
    {
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(_unitOfWork.Context.Set<SubscriptionPlan>());
    }

    public async Task ActivateSubscriptionAsync(string userId, int planId)
    {
        var plan = await _unitOfWork.Context.Set<SubscriptionPlan>().FindAsync(planId);
        if (plan == null) throw new ArgumentException("Plan not found");

        var user = await _unitOfWork.Context.Set<User>().FindAsync(userId);
        if (user == null) throw new ArgumentException("User not found");

        var subscriptionRepo = _unitOfWork.Context.Set<Subscription>();
        
        // Find existing active subscription
        var existingSubscriptions = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            System.Linq.Queryable.Where(subscriptionRepo, s => s.UserId == userId && s.Status == "Active"));
        var activeSub = existingSubscriptions.OrderByDescending(s => s.EndDate).FirstOrDefault();

        var durationDays = plan.DurationDays;
        
        if (activeSub != null)
        {
            // Extend existing subscription
            activeSub.EndDate = activeSub.EndDate.AddDays(durationDays);
            subscriptionRepo.Update(activeSub);
        }
        else
        {
            // Create new subscription
            var newSub = new Subscription
            {
                UserId = userId,
                PlanId = planId,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(durationDays),
                Status = "Active"
            };
            await subscriptionRepo.AddAsync(newSub);
        }

        // Change user role/metadata if needed, depending on the system design
        // Here we just ensure the Subscription entity is correct as requested.
        await _unitOfWork.SaveChangesAsync();
    }
}
