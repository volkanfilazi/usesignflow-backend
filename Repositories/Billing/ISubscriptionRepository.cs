using DynamicFormBuilder.Models.Billing;

namespace DynamicFormBuilder.Repositories.Billing;

public interface ISubscriptionRepository
{
    Task<UserSubscription?> GetByUserIdAsync(string userId);
    Task<UserSubscription?> GetByLemonSubscriptionIdAsync(string lemonSubscriptionId);
    Task CreateAsync(UserSubscription subscription);
    Task UpsertByUserIdAsync(UserSubscription subscription);
}