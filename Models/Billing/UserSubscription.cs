using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DynamicFormBuilder.Models.Billing;

public class UserSubscription
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string UserId { get; set; } = default!;

    [BsonRepresentation(BsonType.String)]
    public PlanCode PlanCode { get; set; } = PlanCode.Free;

    [BsonRepresentation(BsonType.String)]
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Inactive;

    public string? LemonCustomerId { get; set; }
    public string? LemonSubscriptionId { get; set; }
    public string? LemonVariantId { get; set; }
    public DateTime? CurrentPeriodStartUtc { get; set; }
    public DateTime? CurrentPeriodEndUtc { get; set; }
    public bool CancelAtPeriodEnd { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}