namespace DynamicFormBuilder.Models.Billing.Responses;

public class BillingEntitlementsResponse
{
    public int MaxActiveFlows { get; set; }
    public int MaxSubmissionsPerMonth { get; set; }
    public int MaxEmailPerMonth { get; set; }
    public int MaxExportPdfPerMonth { get; set; }
    public bool CanExportPdf { get; set; }
    public bool CanSendEmail { get; set; }
    public bool CanRemoveBranding { get; set; }
}