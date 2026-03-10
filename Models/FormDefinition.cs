using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class FormDefinition
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public string FormName { get; set; } = null!;
    public bool Expanded { get; set; } = false;
    public string Version { get; set; } = null!;

    public List<FieldDefinition> Fields { get; set; } = new();
}

public class FieldDefinition
{
    public string FieldId { get; set; } = null!;
    public string Label { get; set; } = null!;
    public string Type { get; set; } = "text";
    public bool Required { get; set; } = false;
    public List<string>? Options { get; set; }

    public int colSpan { get; set; } = 1 | 2 | 3 | 4;
}