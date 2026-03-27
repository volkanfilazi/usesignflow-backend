using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class FormSubmission
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string FormId { get; set; } = null!;
    public string FormName { get; set; } = null!;
    public string? AgreementContentHtml { get; set; }
    public string FormVersion { get; set; } = "1.0.0";
    public string CreatedByUserId { get; set; } = null!;
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Drafted;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<FieldDefinition> FieldsSnapshot { get; set; } = new();
    public List<FormAnswer> Answers { get; set; } = new();
    public List<FormSignature> Signatures { get; set; } = new();
    public int RowVersion { get; set; } = 1;

    public bool OwnerConfirmed { get; set; } = false;
    public DateTime? OwnerConfirmedAtUtc { get; set; }

    public bool ExternalConfirmed { get; set; } = false;
    public DateTime? ExternalConfirmedAtUtc { get; set; }

    public string? ExternalRecipientEmail { get; set; }
    public DateTime? SentToExternalAtUtc { get; set; }
}