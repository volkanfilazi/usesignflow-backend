namespace DynamicFormBuilder.Services.Billing;

public class PdfExportGuard
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly IPlanEntitlementService _planEntitlementService;

    public PdfExportGuard(
        ISubscriptionService subscriptionService,
        IPlanEntitlementService planEntitlementService)
    {
        _subscriptionService = subscriptionService;
        _planEntitlementService = planEntitlementService;
    }

    public async Task EnsureCanExportPdfAsync(string userId)
    {
        var subscription = await _subscriptionService.GetOrCreateForUserAsync(userId);
        var entitlements = _planEntitlementService.Get(subscription.PlanCode);

        if (!entitlements.CanExportPdf)
            throw new UnauthorizedAccessException("Your current plan does not allow PDF export.");
    }
}