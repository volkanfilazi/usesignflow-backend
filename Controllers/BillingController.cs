using DynamicFormBuilder.Models;
using DynamicFormBuilder.Models.Billing;
using DynamicFormBuilder.Repositories.Billing;
using DynamicFormBuilder.Services.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DynamicFormBuilder.Controllers;

[ApiController]
[Authorize]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billingService;
    private readonly BillingOverviewService _billingOverviewService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IPlanEntitlementService _planEntitlementService;

    public BillingController(
        IBillingService billingService,
        ISubscriptionService subscriptionService,
        BillingOverviewService billingOverviewService,
        ISubscriptionRepository subscriptionRepository,
        IPlanEntitlementService planEntitlementService)
    {
        _billingService = billingService;
        _subscriptionService = subscriptionService;
        _planEntitlementService = planEntitlementService;
        _subscriptionRepository = subscriptionRepository;
        _billingOverviewService = billingOverviewService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var subscription = await _subscriptionService.GetOrCreateForUserAsync(userId);

        return Ok(new
        {
            planCode = subscription.PlanCode,
            status = subscription.Status,
            currentPeriodEndUtc = subscription.CurrentPeriodEndUtc,
            cancelAtPeriodEnd = subscription.CancelAtPeriodEnd
        });
    }

    [HttpGet("entitlements")]
    public async Task<IActionResult> GetEntitlements()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var subscription = await _subscriptionService.GetOrCreateForUserAsync(userId);
        var entitlements = _planEntitlementService.Get(subscription.PlanCode);

        return Ok(new
        {
            maxActiveFlows = entitlements.MaxActiveFlows,
            maxSubmissionsPerMonth = entitlements.MaxSubmissionsPerMonth,
            maxEmailPerMonth = entitlements.MaxEmailPerMonth,
            maxExportPdfPerMonth = entitlements.MaxExportPdfPerMonth,
            canSendEmail = entitlements.CanSendEmail,
            canExportPdf = entitlements.CanExportPdf,
            canRemoveBranding = entitlements.CanRemoveBranding
        });
    }

    [HttpPost("checkout")]
    public IActionResult CreateCheckout([FromBody] CreateCheckoutRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (request.PlanCode == PlanCode.Free)
            return BadRequest("Free plan does not require checkout.");

        var checkoutUrl = _billingService.CreateCheckoutUrl(userId, request.PlanCode);

        return Ok(new
        {
            checkoutUrl
        });
    }

    [HttpPost("change-plan")]
    public async Task<IActionResult> ChangePlan([FromBody] ChangePlanRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (request.PlanCode == PlanCode.Free)
            return BadRequest("Use cancel flow for free plan.");

        var subscription = await _subscriptionService.GetOrCreateForUserAsync(userId);

        if (string.IsNullOrWhiteSpace(subscription.LemonSubscriptionId))
            return BadRequest("No active paid subscription found.");

        if (subscription.PlanCode == request.PlanCode)
            return BadRequest("Already on this plan.");

        await _billingService.ChangePlanAsync(subscription, request.PlanCode);

        return Ok();
    }

    [Authorize]
    [HttpPost("cancel-renewal")]
    public async Task<IActionResult> CancelRenewal()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var subscription = await _subscriptionService.GetOrCreateForUserAsync(userId);

        if (subscription.PlanCode == PlanCode.Free ||
            string.IsNullOrWhiteSpace(subscription.LemonSubscriptionId))
        {
            return BadRequest(new ApiError
            {
                Code = "NO_ACTIVE_SUBSCRIPTION",
                Message = "No active paid subscription found."
            });
        }

        if (subscription.CancelAtPeriodEnd)
        {
            return BadRequest(new ApiError
            {
                Code = "SUBSCRIPTION_ALREADY_CANCELLED",
                Message = "Subscription is already set to cancel at period end."
            });
        }

        await _billingService.CancelRenewalAsync(subscription.LemonSubscriptionId);

        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.CancelAtPeriodEnd = true;
        subscription.UpdatedAtUtc = DateTime.UtcNow;

        await _subscriptionRepository.UpsertByUserIdAsync(subscription);

        return Ok(new
        {
            message = "Your subscription will cancel at the end of the current billing period.",
            currentPeriodEndUtc = subscription.CurrentPeriodEndUtc
        });
    }

    [Authorize]
    [HttpPost("reactivate-renewal")]
    public async Task<IActionResult> ReactivateRenewal()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var subscription = await _subscriptionService.GetOrCreateForUserAsync(userId);

        if (subscription.PlanCode == PlanCode.Free ||
            string.IsNullOrWhiteSpace(subscription.LemonSubscriptionId))
        {
            return BadRequest(new ApiError
            {
                Code = "NO_ACTIVE_SUBSCRIPTION",
                Message = "No paid subscription found."
            });
        }

        if (!subscription.CancelAtPeriodEnd)
        {
            return BadRequest(new ApiError
            {
                Code = "SUBSCRIPTION_NOT_CANCELLED",
                Message = "Subscription is already active."
            });
        }

        await _billingService.ReactivateRenewalAsync(subscription.LemonSubscriptionId);

        subscription.Status = SubscriptionStatus.Active;
        subscription.CancelAtPeriodEnd = false;
        subscription.UpdatedAtUtc = DateTime.UtcNow;

        await _subscriptionRepository.UpsertByUserIdAsync(subscription);

        return Ok(new
        {
            message = "Your subscription has been reactivated."
        });
    }

    public class ChangePlanRequest
    {
        public PlanCode PlanCode { get; set; }
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var result = await _billingOverviewService.GetAsync(userId);

        return Ok(result);
    }
}

public class CreateCheckoutRequest
{
    public PlanCode PlanCode { get; set; }
}