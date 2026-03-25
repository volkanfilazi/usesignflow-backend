using DynamicFormBuilder.Models.Billing;

namespace DynamicFormBuilder.Services.Billing;

public class PlanEntitlements
{
    public int MaxActiveFlows { get; init; }
    public int MaxSubmissionsPerMonth { get; init; }
    public int MaxEmailPerMonth { get; init; }
    public int MaxExportPdfPerMonth { get; init; }

    public bool CanSendEmail { get; init; }
    public bool CanExportPdf { get; init; }
    public bool CanRemoveBranding { get; init; }
}

public class PlanEntitlementService : IPlanEntitlementService
{
    public PlanEntitlements Get(PlanCode planCode)
    {
        return planCode switch
        {
            PlanCode.Free => new PlanEntitlements
            {
                MaxActiveFlows = 2,
                MaxSubmissionsPerMonth = 10,
                MaxEmailPerMonth = 25,
                MaxExportPdfPerMonth = 0,
                CanSendEmail = true,
                CanExportPdf = false,
                CanRemoveBranding = false
            },

            PlanCode.Pro => new PlanEntitlements
            {
                MaxActiveFlows = 25,
                MaxSubmissionsPerMonth = 250,
                MaxEmailPerMonth = 500,
                MaxExportPdfPerMonth = 250,
                CanSendEmail = true,
                CanExportPdf = true,
                CanRemoveBranding = true
            },

            PlanCode.Business => new PlanEntitlements
            {
                MaxActiveFlows = 100,
                MaxSubmissionsPerMonth = 2000,
                MaxEmailPerMonth = 5000,
                MaxExportPdfPerMonth = 2000,
                CanSendEmail = true,
                CanExportPdf = true,
                CanRemoveBranding = true
            },

            _ => throw new ArgumentOutOfRangeException(nameof(planCode))
        };
    }
}