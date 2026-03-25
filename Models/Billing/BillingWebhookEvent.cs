using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DynamicFormBuilder.Models.Billing;

public class BillingWebhookEvent
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string EventName { get; set; } = default!;
    public string EventIdempotencyKey { get; set; } = default!;
    public string PayloadJson { get; set; } = default!;
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
}