namespace DynamicFormBuilder.Models.Form
{
    public class FormAgreementAcceptance
    {
        public string FieldId { get; set; } = null!;
        public string? AcceptedByUserId { get; set; }
        public string? AcceptedByEmail { get; set; }
        public DateTime AcceptedAtUtc { get; set; }
        public string? AcceptedFromIpAddress { get; set; }
        public string? AcceptedUserAgent { get; set; }
    }
}
