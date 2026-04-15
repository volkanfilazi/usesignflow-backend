using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Drawing.Text;

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

    public DateTime? RevokedAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public bool IsRevoked { get; set; } = false;
    public Purpose Purpose { get; set; } = Purpose.EditSubmission;
}

public enum Purpose
{
    ReadSubmission = 1,
    EditSubmission = 2
}