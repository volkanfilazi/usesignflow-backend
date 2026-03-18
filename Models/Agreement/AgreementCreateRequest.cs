namespace DynamicFormBuilder.Models
{
    public class AgreementCreateRequest
    {
        public string? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}