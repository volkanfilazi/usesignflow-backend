using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class SubmissionAccessToken
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string SubmissionId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public bool IsRevoked { get; set; } = false;
}