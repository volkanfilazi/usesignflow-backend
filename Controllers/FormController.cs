using DynamicFormBuilder.Repositories.Form;
using DynamicFormBuilder.Services.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using System.Security.Claims;

namespace FormBuilderApi.Controllers;

[ApiController]
[Route("api/forms")]
public class FormsController : ControllerBase
{
    private readonly IFormRepository _repo;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IPlanEntitlementService _planEntitlementService;

    public FormsController(
        IFormRepository repo,
        IPlanEntitlementService planEntitlementService,
        ISubscriptionService subscriptionService)
    {
        _repo = repo;
        _planEntitlementService = planEntitlementService;
        _subscriptionService = subscriptionService;
    }

    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<List<FormDefinition>>> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var forms = await _repo.GetByUserIdAsync(userId);
        return Ok(forms);
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<ActionResult<FormDefinition>> GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest("id is required.");
        }

        if (!ObjectId.TryParse(id, out _))
        {
            return BadRequest("Invalid form id.");
        }

        var form = await _repo.GetByIdAsync(id);
        return form is null ? NotFound() : Ok(form);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<FormDefinition>> Create([FromBody] FormDefinition form)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var subscription = await _subscriptionService.GetOrCreateForUserAsync(userId);
        var entitlements = _planEntitlementService.Get(subscription.PlanCode);
        var activeFlowCount = await _repo.CountByUserIdAsync(userId);

        if (activeFlowCount >= entitlements.MaxActiveFlows)
        {
            return StatusCode(403, new
            {
                code = "PLAN_LIMIT_REACHED",
                message = $"Your current plan allows up to {entitlements.MaxActiveFlows} active flows.",
                currentPlan = subscription.PlanCode.ToString(),
                limit = entitlements.MaxActiveFlows
            });
        }


        if (string.IsNullOrWhiteSpace(form.FormName))
            return BadRequest("formName is required.");

        if (form.Fields is null) form.Fields = new();

        var keys = form.Fields
            .Where(f => !string.IsNullOrWhiteSpace(f.FieldId))
            .Select(f => f.FieldId.Trim().ToLowerInvariant())
            .ToList();

        //Duplicate check
        if (keys.Count != keys.Distinct().Count())
            return BadRequest("Each field 'key' must be unique within the form.");

        foreach (var f in form.Fields)
        {
            if (string.Equals(f.Type, "Dropdown", StringComparison.OrdinalIgnoreCase)
                && (f.Options is null || f.Options.Count == 0))
            {
                return BadRequest($"Field '{f.FieldId}' is type 'Dropdown' but has no options.");
            }
        }

        //No trust, Mongodb will generate new ObjecetId
        form.Id = null;
        form.OwnerUserId = userId;
        form.CreatedAtUtc = DateTime.UtcNow;

        await _repo.CreateAsync(form);

        return Ok(form);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] FormDefinition updated)
    {
        if (updated is null)
            return BadRequest();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var existing = await _repo.GetByIdAsync(id);
        if (existing is null)
            return NotFound();

        if (existing.OwnerUserId != userId)
            return Forbid();

        existing.FormName = updated.FormName;
        existing.AgreementContentHtml = updated.AgreementContentHtml;
        existing.Expanded = updated.Expanded;
        existing.RequiresVerification = updated.RequiresVerification;
        existing.Version = updated.Version;
        existing.Fields = updated.Fields;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        await _repo.UpdateAsync(id, existing);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest("id is required.");

        var existing = await _repo.GetByIdAsync(id);
        if (existing is null) return NotFound();

        await _repo.DeleteAsync(id);
        return NoContent();
    }
}