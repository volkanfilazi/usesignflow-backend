using DynamicFormBuilder.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class AuthDefinition
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public bool EmailVerified { get; set; } = false;
    public string? EmailVerificationTokenHash { get; set; }
    public DateTime? EmailVerificationTokenExpiresAtUtc { get; set; }
    public List<LegalAcceptance> LegalAcceptances { get; set; } = new();
}