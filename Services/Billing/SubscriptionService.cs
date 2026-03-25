using DynamicFormBuilder.Models.Billing;

namespace DynamicFormBuilder.Services.Billing;

public class SubscriptionService : ISubscriptionService
{
    private readonly SubscriptionRepository _subscriptionRepository;

    public SubscriptionService(SubscriptionRepository subscriptionRepository)
    {
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<UserSubscription> GetOrCreateForUserAsync(string userId)
    {
        var existing = await _subscriptionRepository.GetByUserIdAsync(userId);
        if (existing != null)
            return existing;

        var subscription = new UserSubscription
        {
            UserId = userId,
            PlanCode = PlanCode.Free,
            Status = SubscriptionStatus.Active
        };

        await _subscriptionRepository.CreateAsync(subscription);
        return subscription;
    }
}