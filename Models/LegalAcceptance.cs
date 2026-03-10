namespace DynamicFormBuilder.Models
{
    public class LegalAcceptance
    {
        public string Type { get; set; } = default!;
        public string Version { get; set; } = default!;
        public string Hash { get; set; } = default!;
        public DateTime AcceptedAtUtc { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string Source { get; set; } = "register";
    }
}
