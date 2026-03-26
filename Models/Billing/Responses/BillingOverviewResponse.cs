namespace DynamicFormBuilder.Models.Billing.Responses;

public class BillingOverviewResponse
{
    public string PlanCode { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime? CurrentPeriodStartUtc { get; set; }
    public DateTime? CurrentPeriodEndUtc { get; set; }
    public bool CancelAtPeriodEnd { get; set; }

    public BillingEntitlementsDto Entitlements { get; set; } = default!;
    public BillingUsageDto Usage { get; set; } = default!;
}

public class BillingEntitlementsDto
{
    public int MaxActiveFlows { get; set; }
    public int MaxSubmissionsPerMonth { get; set; }
    public int MaxEmailPerMonth { get; set; }
    public int MaxExportPdfPerMonth { get; set; }
    public bool CanSendEmail { get; set; }
    public bool CanExportPdf { get; set; }
    public bool CanRemoveBranding { get; set; }
}

public class BillingUsageDto
{
    public long ActiveFlowsUsed { get; set; }
    public long SubmissionsUsedThisMonth { get; set; }
    public long EmailsUsedThisMonth { get; set; }
    public long PdfExportsUsedThisMonth { get; set; }
}