namespace DynamicFormBuilder.Models.Billing.Responses;

public class BillingMeResponse
{
    public string PlanCode { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime? CurrentPeriodEndUtc { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
}