using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class SignatureRequest
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string SubmissionId { get; set; } = null!;

    public string RecipientEmail { get; set; } = null!;
    public string SignatureFieldId { get; set; } = null!;

    public string TokenHash { get; set; } = null!;
    public SignatureRequestStatus Status { get; set; } = SignatureRequestStatus.Pending;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
    public DateTime? SignedAtUtc { get; set; }
}

public enum SignatureRequestStatus
{
    Pending = 0,
    Opened = 1,
    Signed = 2,
    Expired = 3,
    Cancelled = 4
}

public class CreateFormSubmissionRequest
{
    public string FormId { get; set; } = null!;
    public List<FormAnswerDto> Answers { get; set; } = new();
}

public class FormAnswerDto
{
    public string FieldId { get; set; } = null!;
    public string? Value { get; set; }
}

public class UpdateFormSubmissionRequest
{
    public List<FormAnswerDto> Answers { get; set; } = new();
    public int RowVersion { get; set; }
}

public class SendForSignatureRequest
{
    public string RecipientEmail { get; set; } = null!;
    public string SignatureFieldId { get; set; } = null!;
}

public class SignSubmissionRequest
{
    public string SignatureDataBase64 { get; set; } = null!;
    public string? SignedByEmail { get; set; }
}

public class SendSubmissionAccessTokenRequest
{
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set;  } = string.Empty;
}

public class UpdateSubmissionByAccessTokenRequest
{
    public string Token { get; set; } = string.Empty;
    public int RowVersion { get; set; }
    public List<FormAnswerDto> Answers { get; set; } = new();
    public bool? AgreementAccepted { get; set; }
}