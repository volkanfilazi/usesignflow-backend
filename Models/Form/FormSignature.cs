public class FormSignature
{
    public string FieldId { get; set; } = null!;
    public string? SignedByUserId { get; set; }
    public string? SignedByEmail { get; set; }
    public string? SignatureUrl { get; set; }
    public DateTime? SignedAtUtc { get; set; }
}