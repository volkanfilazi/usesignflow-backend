using DynamicFormBuilder.Models.Billing;

namespace DynamicFormBuilder.Services.Billing;

public interface ISubscriptionService
{
    Task<UserSubscription> GetOrCreateForUserAsync(string userId);
    Task HandlePaidSubscriptionRenewalAsync(string userId,
    DateTime newPeriodStartUtc,
    DateTime newPeriodEndUtc);
}