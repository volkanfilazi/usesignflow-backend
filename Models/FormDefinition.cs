using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using DynamicFormBuilder.Models;

public class FormDefinition
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string OwnerUserId { get; set; } = string.Empty;
    public string FormName { get; set; } = null!;
    public string? AgreementContentHtml { get; set; }
    public bool Expanded { get; set; } = false;
    public string Version { get; set; } = "1.0.0";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public List<FieldDefinition> Fields { get; set; } = new();
}

public class FieldDefinition
{
    public string FieldId { get; set; } = null!;
    public string Label { get; set; } = null!;
    public string Type { get; set; } = "ShortText";
    public string? SignatureType { get; set; }
    public AssignedTo AssignedTo { get; set; } = AssignedTo.You;
    public AgreementSnapshot? Agreement { get; set; }
    public bool Required { get; set; } = false;
    public int? Min { get; set; }
    public int? Max { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? Pattern { get; set; }
    public List<string>? Options { get; set; }
    public int ColSpan { get; set; } = 1;
}

public enum AssignedTo
{
    You,
    Client
}