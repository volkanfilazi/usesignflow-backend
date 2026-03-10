namespace DynamicFormBuilder.Models
{
    public class LegalDocumentInfo
    {
        public string Type { get; set; } = default!;
        public string Version { get; set; } = default!;
        public string FilePath { get; set; } = default!;
        public string Hash { get; set; } = default!;
    }
}
