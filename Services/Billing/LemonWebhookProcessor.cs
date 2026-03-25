using System.Text.Json;
using DynamicFormBuilder.Models.Billing;
using System.Text.Json.Serialization;

namespace DynamicFormBuilder.Services.Billing;

public class LemonWebhookEnvelope
{
    [JsonPropertyName("meta")]
    public LemonMeta Meta { get; set; } = default!;

    [JsonPropertyName("data")]
    public LemonData Data { get; set; } = default!;
}

public class LemonMeta
{
    [JsonPropertyName("event_name")]
    public string EventName { get; set; } = default!;

    [JsonPropertyName("custom_data")]
    public Dictionary<string, JsonElement>? CustomData { get; set; }
}

public class LemonData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    [JsonPropertyName("attributes")]
    public LemonAttributes Attributes { get; set; } = default!;
}

public class LemonAttributes
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = default!;

    [JsonPropertyName("customer_id")]
    public JsonElement? CustomerId { get; set; }

    [JsonPropertyName("variant_id")]
    public JsonElement? VariantId { get; set; }

    [JsonPropertyName("renews_at")]
    public string? RenewsAt { get; set; }

    [JsonPropertyName("ends_at")]
    public string? EndsAt { get; set; }

    [JsonPropertyName("cancelled")]
    public bool Cancelled { get; set; }

    [JsonPropertyName("current_period_start")]
    public string? CurrentPeriodStart { get; set; }
}

public interface ILemonWebhookProcessor
{
    Task ProcessAsync(string rawBody);
}

public class LemonWebhookProcessor : ILemonWebhookProcessor
{
    private readonly BillingWebhookEventRepository _eventRepository;
    private readonly SubscriptionRepository _subscriptionRepository;
    private readonly ILemonPlanMapper _planMapper;

    public LemonWebhookProcessor(
        BillingWebhookEventRepository eventRepository,
        SubscriptionRepository subscriptionRepository,
        ILemonPlanMapper planMapper)
    {
        _eventRepository = eventRepository;
        _subscriptionRepository = subscriptionRepository;
        _planMapper = planMapper;
    }

    public async Task ProcessAsync(string rawBody)
    {
        var payload = JsonSerializer.Deserialize<LemonWebhookEnvelope>(
            rawBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (payload == null)
            return;

        var eventName = payload.Meta.EventName; 
        var eventKey = $"{eventName}:{payload.Data.Id}";

        if (await _eventRepository.ExistsAsync(eventKey))
            return;

        await _eventRepository.CreateAsync(new BillingWebhookEvent
        {
            EventName = eventName,
            EventIdempotencyKey = eventKey,
            PayloadJson = rawBody
        });

        switch (eventName)
        {
            case "subscription_created":
            case "subscription_updated":
            case "subscription_cancelled":
            case "subscription_expired":
                await UpsertSubscriptionAsync(payload);
                break;
        }
    }

    private async Task UpsertSubscriptionAsync(LemonWebhookEnvelope payload)
    {
        var attrs = payload.Data.Attributes;
        var userId = ResolveUserId(payload);

        var existing = await _subscriptionRepository.GetByUserIdAsync(userId)
            ?? new UserSubscription
            {
                UserId = userId,
                CreatedAtUtc = DateTime.UtcNow
            };

        existing.LemonSubscriptionId = payload.Data.Id;
        existing.LemonCustomerId = attrs.CustomerId?.ToString();
        existing.LemonVariantId = attrs.VariantId?.ToString();

        if (!string.IsNullOrWhiteSpace(existing.LemonVariantId))
            existing.PlanCode = _planMapper.MapVariantToPlan(existing.LemonVariantId);

        existing.Status = MapStatus(payload.Meta.EventName, attrs.Status);
        existing.CancelAtPeriodEnd = attrs.Cancelled;

        if (DateTime.TryParse(attrs.CurrentPeriodStart, out var currentPeriodStart))
        {
            existing.CurrentPeriodStartUtc = DateTime.SpecifyKind(
                currentPeriodStart.ToUniversalTime(),
                DateTimeKind.Utc);
        }

        if (DateTime.TryParse(attrs.RenewsAt, out var renewsAt))
        {
            existing.CurrentPeriodEndUtc = DateTime.SpecifyKind(
                renewsAt.ToUniversalTime(),
                DateTimeKind.Utc);
        }
        else if (DateTime.TryParse(attrs.EndsAt, out var endsAt))
        {
            existing.CurrentPeriodEndUtc = DateTime.SpecifyKind(
                endsAt.ToUniversalTime(),
                DateTimeKind.Utc);
        }

        if (!existing.CurrentPeriodStartUtc.HasValue && existing.CurrentPeriodEndUtc.HasValue)
        {
            existing.CurrentPeriodStartUtc = DateTime.SpecifyKind(
                existing.CurrentPeriodEndUtc.Value.AddMonths(-1).Date,
                DateTimeKind.Utc);
        }

        existing.UpdatedAtUtc = DateTime.UtcNow;

        await _subscriptionRepository.UpsertByUserIdAsync(existing);
    }

    private static string ResolveUserId(LemonWebhookEnvelope payload)
    {
        if (payload.Meta.CustomData != null &&
            payload.Meta.CustomData.TryGetValue("user_id", out var rawUserId))
        {
            var userId = rawUserId.GetString();
            if (!string.IsNullOrWhiteSpace(userId))
                return userId;
        }

        throw new InvalidOperationException("Webhook does not contain user_id.");
    }

    private static SubscriptionStatus MapStatus(string eventName, string? lemonStatus)
    {
        if (eventName == "subscription_cancelled")
            return SubscriptionStatus.Cancelled;

        if (eventName == "subscription_expired")
            return SubscriptionStatus.Expired;

        return lemonStatus?.ToLowerInvariant() switch
        {
            "active" => SubscriptionStatus.Active,
            "cancelled" => SubscriptionStatus.Cancelled,
            "expired" => SubscriptionStatus.Expired,
            "past_due" => SubscriptionStatus.PastDue,
            "paused" => SubscriptionStatus.Paused,
            _ => SubscriptionStatus.Inactive
        };
    }
}