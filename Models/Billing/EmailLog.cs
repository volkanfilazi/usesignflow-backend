using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DynamicFormBuilder.Models.Billing;

public class EmailLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;

    public string UserId { get; set; } = default!;
    public string ToEmail { get; set; } = default!;
    public string EmailType { get; set; } = default!;
    public string RelatedEntityId { get; set; } = string.Empty;
    public string Subject { get; set; } = default!;
    public EmailLogStatus Status { get; set; } = EmailLogStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }
}

public enum EmailLogStatus
{
    Pending,
    Sent,
    Failed
}