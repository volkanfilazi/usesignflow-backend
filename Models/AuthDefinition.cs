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
    public List<RefreshTokenDefinition> RefreshTokens { get; set; } = new();
    public List<ExternalLogin> ExternalLogins { get; set; } = new();

    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorSecret { get; set; }

    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeleteReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public bool IsAnonymized { get; set; } = false;
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}