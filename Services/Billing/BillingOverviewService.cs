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
        var entitlements = _planEntitlementService.Get(subscription.PlanCode);

        var periodStartUtc = ResolvePeriodStartUtc(subscription);
        var periodEndUtc = ResolvePeriodEndUtc(subscription);

        var activeFlows = await _formRepository.CountByUserIdAsync(userId);
        var submissions = await _submissionRepository.CountCreatedInPeriodAsync(
            userId,
            periodStartUtc,
            periodEndUtc
        );
        var emails = await _emailRepo.CountSentInPeriodAsync(
            userId,
            periodStartUtc,
            periodEndUtc
        );

        return new BillingOverviewResponse
        {
            PlanCode = subscription.PlanCode.ToString(),
            Status = subscription.Status.ToString(),
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
    private DateTime ResolvePeriodStartUtc(UserSubscription subscription)
    {
        if (subscription.CurrentPeriodStartUtc.HasValue)
            return DateTime.SpecifyKind(subscription.CurrentPeriodStartUtc.Value, DateTimeKind.Utc);

        if (subscription.PlanCode == PlanCode.Free)
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        if (subscription.CurrentPeriodEndUtc.HasValue)
        {
            return DateTime.SpecifyKind(
                subscription.CurrentPeriodEndUtc.Value.AddMonths(-1).Date,
                DateTimeKind.Utc);
        }

        var fallbackNow = DateTime.UtcNow;
        return new DateTime(fallbackNow.Year, fallbackNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private DateTime ResolvePeriodEndUtc(UserSubscription subscription)
    {
        if (subscription.CurrentPeriodEndUtc.HasValue)
            return DateTime.SpecifyKind(subscription.CurrentPeriodEndUtc.Value, DateTimeKind.Utc);

        var periodStartUtc = ResolvePeriodStartUtc(subscription);
        return DateTime.SpecifyKind(periodStartUtc.AddMonths(1), DateTimeKind.Utc);
    }
}