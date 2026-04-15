using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SendGrid.Helpers.Mail;

public class ResolveSubmissionAccessRequest
{
	public string Token { get; set; } = string.Empty;
}

public class ResolveSubmissionAccessResponse
{
	public string SubmissionId { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public bool IsAuthenticated { get; set; }
	public bool RequiresVerification { get; set; }
	public bool IsEmailMatch { get; set; }
}

public class ResolveVerifyTokenResponse
{
    public string SubmissionId { get; set; } = string.Empty;
    public bool RequiresVerification { get; set; }
}

public class SendOneTimeCodeRequest
{
    public string VerifyToken { get; set; } = string.Empty;
}

public class VerifyOneTimeCodeRequest
{
    public string VerifyToken { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class SubmissionVerificationLog
{
    public string SubmissionId { get; set; } = default!;
    public string TokenHash { get; set; } = default!;

    public Channel Channel { get; set; } = Channel.Email; // Email / SMS

    public string TargetMasked { get; set; } = default!; // a***@mail.com

    public DateTime CodeSentAtUtc { get; set; }
    public DateTime VerifiedAtUtc { get; set; }

    public string IpAddress { get; set; } = default!;
    public string UserAgent { get; set; } = default!;
}

public enum Channel
{
    Email = 0,
    SMS = 1,
}
public class OneTimeCode
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string SubmissionId { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public bool IsUsed { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}