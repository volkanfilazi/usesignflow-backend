using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using DynamicFormBuilder.Services.Billing;
using DynamicFormBuilder.Repositories.Form;

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

    [HttpGet("{id}")]
    public async Task<ActionResult<FormDefinition>> GetById(string id)
    {
        var form = await _repo.GetByIdAsync(id);
        return form is null ? NotFound() : Ok(form);
    }

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

        form.Id = null;
        form.OwnerUserId = userId;
        form.CreatedAtUtc = DateTime.UtcNow;

        await _repo.CreateAsync(form);
        return CreatedAtAction(nameof(GetById), new { id = form.Id }, form);
    }

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
        existing.Version = updated.Version;
        existing.Fields = updated.Fields;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        await _repo.UpdateAsync(id, existing);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _repo.GetByIdAsync(id);
        if (existing is null) return NotFound();

        await _repo.DeleteAsync(id);
        return NoContent();
    }
}