using DynamicFormBuilder.Models.Billing;
using DynamicFormBuilder.Repositories.Billing;
using DynamicFormBuilder.Services.Common;

namespace DynamicFormBuilder.Services.Billing;

public class SubscriptionService : ISubscriptionService
{
    private readonly IClock _clock;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public SubscriptionService(IClock clock, ISubscriptionRepository subscriptionRepository)
    {
        _clock = clock;
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<UserSubscription> GetOrCreateForUserAsync(string userId)
    {
        var subscription = await _subscriptionRepository.GetByUserIdAsync(userId);

        if (subscription == null)
        {
            var now = _clock.UtcNow;

            subscription = new UserSubscription
            {
                UserId = userId,
                PlanCode = PlanCode.Free,
                Status = SubscriptionStatus.Active,
                CurrentPeriodStartUtc = now,
                CurrentPeriodEndUtc = now.AddMonths(1),
                CancelAtPeriodEnd = false
            };

            await _subscriptionRepository.CreateAsync(subscription);
            return subscription;
        }

        if (subscription.PlanCode == PlanCode.Free)
        {
            var now = _clock.UtcNow;
            var changed = false;

            while (subscription.CurrentPeriodEndUtc.HasValue &&
                   now >= subscription.CurrentPeriodEndUtc.Value)
            {
                subscription.CurrentPeriodStartUtc = subscription.CurrentPeriodEndUtc.Value;
                subscription.CurrentPeriodEndUtc = subscription.CurrentPeriodEndUtc.Value.AddMonths(1);
                changed = true;
            }

            if (changed)
            {
                await _subscriptionRepository.UpsertByUserIdAsync(subscription);
            }
        }

        return subscription;
    }

    public async Task HandlePaidSubscriptionRenewalAsync(
    string userId,
    DateTime newPeriodStartUtc,
    DateTime newPeriodEndUtc)
    {
        var subscription = await _subscriptionRepository.GetByUserIdAsync(userId);

        if (subscription == null)
            throw new InvalidOperationException("Subscription not found for renewal.");

        if (subscription.PlanCode == PlanCode.Free)
            throw new InvalidOperationException("Cannot renew a free subscription.");

        subscription.CurrentPeriodStartUtc = newPeriodStartUtc;
        subscription.CurrentPeriodEndUtc = newPeriodEndUtc;
        subscription.Status = SubscriptionStatus.Active;

        await _subscriptionRepository.UpsertByUserIdAsync(subscription);
    }
}