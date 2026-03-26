using DynamicFormBuilder.Models.Billing;
using DynamicFormBuilder.Repositories.Billing;
using Microsoft.Extensions.Options;

namespace DynamicFormBuilder.Services.Billing;

public interface IBillingService
{
    string CreateCheckoutUrl(string userId, PlanCode planCode);
    Task ChangePlanAsync(UserSubscription currentSubscription, PlanCode newPlan);
    Task CancelRenewalAsync(string lemonSubscriptionId);
    Task ReactivateRenewalAsync(string lemonSubscriptionId);
}

public class BillingService : IBillingService
{
    private readonly LemonOptions _options;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public BillingService(
        IOptions<LemonOptions> options,
        ISubscriptionRepository subscriptionRepository)
    {
        _options = options.Value;
        _subscriptionRepository = subscriptionRepository;
    }

    public string CreateCheckoutUrl(string userId, PlanCode planCode)
    {
        var baseUrl = planCode switch
        {
            PlanCode.Pro => _options.ProCheckoutUrl,
            PlanCode.Business => _options.BusinessCheckoutUrl,
            _ => throw new InvalidOperationException("Free plan does not require checkout.")
        };

        var separator = baseUrl.Contains('?') ? "&" : "?";

        return $"{baseUrl}{separator}checkout[custom][user_id]={Uri.EscapeDataString(userId)}";
    }

    public async Task ChangePlanAsync(UserSubscription currentSubscription, PlanCode newPlan)
    {
        var newVariantId = newPlan switch
        {
            PlanCode.Pro => _options.ProMonthlyVariantId,
            PlanCode.Business => _options.BusinessMonthlyVariantId,
            _ => throw new InvalidOperationException("Invalid target plan.")
        };

        var isUpgrade = Rank(newPlan) > Rank(currentSubscription.PlanCode);
        var attributes = new Dictionary<string, object>
        {
            ["variant_id"] = int.Parse(newVariantId)
        };

        if (isUpgrade)
            attributes["invoice_immediately"] = true;
        else
            attributes["disable_prorations"] = true;

        var payload = new
        {
            data = new
            {
                type = "subscriptions",
                id = currentSubscription.LemonSubscriptionId,
                attributes = attributes
            }
        };

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        client.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.api+json"));

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/vnd.api+json");

        var response = await client.PatchAsync(
            $"https://api.lemonsqueezy.com/v1/subscriptions/{currentSubscription.LemonSubscriptionId}",
            content);

        if (response.IsSuccessStatusCode)
        {
            currentSubscription.PlanCode = newPlan;
            currentSubscription.LemonVariantId = newVariantId;

            if (currentSubscription.CurrentPeriodEndUtc.HasValue &&
                !currentSubscription.CurrentPeriodStartUtc.HasValue)
            {
                currentSubscription.CurrentPeriodStartUtc = DateTime.SpecifyKind(
                    currentSubscription.CurrentPeriodEndUtc.Value.AddMonths(-1).Date,
                    DateTimeKind.Utc);
            }

            currentSubscription.UpdatedAtUtc = DateTime.UtcNow;

            await _subscriptionRepository.UpsertByUserIdAsync(currentSubscription);
        }
        else
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Lemon change plan failed: {body}");
        }
    }

    private static int Rank(PlanCode plan) => plan switch
    {
        PlanCode.Free => 0,
        PlanCode.Pro => 1,
        PlanCode.Business => 2,
        _ => 0
    };

    public async Task CancelRenewalAsync(string lemonSubscriptionId)
    {
        if (string.IsNullOrWhiteSpace(lemonSubscriptionId))
            throw new InvalidOperationException("Subscription id is missing.");

        using var client = new HttpClient();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);

        client.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.api+json"));

        var payload = new
        {
            data = new
            {
                type = "subscriptions",
                id = lemonSubscriptionId,
                attributes = new
                {
                    cancelled = true
                }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);

        var content = new StringContent(
            json,
            System.Text.Encoding.UTF8,
            "application/vnd.api+json");

        var response = await client.PatchAsync(
            $"https://api.lemonsqueezy.com/v1/subscriptions/{lemonSubscriptionId}",
            content);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Lemon cancel failed: {body}");
        }
    }

    public async Task ReactivateRenewalAsync(string lemonSubscriptionId)
    {
        if (string.IsNullOrWhiteSpace(lemonSubscriptionId))
            throw new InvalidOperationException("Subscription id is missing.");

        using var client = new HttpClient();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);

        client.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.api+json"));

        var payload = new
        {
            data = new
            {
                type = "subscriptions",
                id = lemonSubscriptionId,
                attributes = new
                {
                    cancelled = false
                }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/vnd.api+json");

        var response = await client.PatchAsync(
            $"https://api.lemonsqueezy.com/v1/subscriptions/{lemonSubscriptionId}",
            content);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Lemon reactivate failed: {body}");
        }
    }
}