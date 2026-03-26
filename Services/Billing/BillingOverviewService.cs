using DynamicFormBuilder.Models.Billing;
using DynamicFormBuilder.Models.Billing.Responses;
using DynamicFormBuilder.Repositories.Billing;

namespace DynamicFormBuilder.Services.Billing;

public class BillingOverviewService
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly IPlanEntitlementService _planEntitlementService;

    private readonly FormRepository _formRepository;
    private readonly FormSubmissionRepository _submissionRepository;
    private readonly EmailLogRepository _emailRepo;
    private readonly IPdfService _pdfService;

    public BillingOverviewService(
        ISubscriptionService subscriptionService,
        IPlanEntitlementService planEntitlementService,
        FormRepository formRepository,
        FormSubmissionRepository submissionRepository,
        EmailLogRepository emailLogRepository,
        IPdfService pdfService)
    {
        _subscriptionService = subscriptionService;
        _planEntitlementService = planEntitlementService;
        _formRepository = formRepository;
        _submissionRepository = submissionRepository;
        _emailRepo = emailLogRepository;
        _pdfService = pdfService;
    }

    public async Task<BillingOverviewResponse> GetAsync(string userId)
    {
        var subscription = await _subscriptionService.GetOrCreateForUserAsync(userId);

        if (!subscription.CurrentPeriodStartUtc.HasValue || !subscription.CurrentPeriodEndUtc.HasValue)
            throw new InvalidOperationException("Subscription period is not initialized.");

        var periodStartUtc = DateTime.SpecifyKind(subscription.CurrentPeriodStartUtc.Value, DateTimeKind.Utc);
        var periodEndUtc = DateTime.SpecifyKind(subscription.CurrentPeriodEndUtc.Value, DateTimeKind.Utc);

        var entitlements = _planEntitlementService.Get(subscription.PlanCode);

        var activeFlows = await _formRepository.CountByUserIdAsync(userId);

        var submissions = await _submissionRepository.CountCreatedInPeriodAsync(
            userId,
            periodStartUtc,
            periodEndUtc);

        var emails = await _emailRepo.CountSentInPeriodAsync(
            userId,
            periodStartUtc,
            periodEndUtc);

        return new BillingOverviewResponse
        {
            PlanCode = subscription.PlanCode.ToString(),
            Status = subscription.Status.ToString(),
            CurrentPeriodStartUtc = periodStartUtc,
            CurrentPeriodEndUtc = periodEndUtc,
            CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,

            Entitlements = new BillingEntitlementsDto
            {
                MaxActiveFlows = entitlements.MaxActiveFlows,
                MaxSubmissionsPerMonth = entitlements.MaxSubmissionsPerMonth,
                MaxEmailPerMonth = entitlements.MaxEmailPerMonth,
                MaxExportPdfPerMonth = entitlements.MaxExportPdfPerMonth,
                CanSendEmail = entitlements.CanSendEmail,
                CanExportPdf = entitlements.CanExportPdf,
                CanRemoveBranding = entitlements.CanRemoveBranding
            },

            Usage = new BillingUsageDto
            {
                ActiveFlowsUsed = activeFlows,
                SubmissionsUsedThisMonth = submissions,
                EmailsUsedThisMonth = emails
            }
        };
    }
}