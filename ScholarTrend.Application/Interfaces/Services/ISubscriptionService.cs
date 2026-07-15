using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Services;

public interface ISubscriptionService
{
    Task<IEnumerable<SubscriptionPlan>> GetPlansAsync();
    Task ActivateSubscriptionAsync(string userId, int planId);
}
