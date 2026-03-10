using DynamicFormBuilder.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FormBuilderApi.Controllers;

[ApiController]
[Route("api/forms")]
public class FormsController : ControllerBase
{
    private readonly FormRepository _repo;

    public FormsController(FormRepository repo) => _repo = repo;

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
        // minimal validation
        if (string.IsNullOrWhiteSpace(form.FormName))
            return BadRequest("formName is required.");

        if (form.Fields is null) form.Fields = new();

        // key unique check (case-insensitive)
        var keys = form.Fields
            .Where(f => !string.IsNullOrWhiteSpace(f.FieldId))
            .Select(f => f.FieldId.Trim().ToLowerInvariant())
            .ToList();

        if (keys.Count != keys.Distinct().Count())
            return BadRequest("Each field 'key' must be unique within the form.");

        // required select must have options
        foreach (var f in form.Fields)
        {
            if (string.Equals(f.Type, "select", StringComparison.OrdinalIgnoreCase)
                && (f.Options is null || f.Options.Count == 0))
            {
                return BadRequest($"Field '{f.FieldId}' is type 'select' but has no options.");
            }
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        // Let Mongo create Id
        form.Id = null;
        form.OwnerUserId = userId;
        form.CreatedAtUtc = DateTime.UtcNow;

        await _repo.CreateAsync(form);
        return CreatedAtAction(nameof(GetById), new { id = form.Id }, form);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] FormDefinition updated)
    {
        var existing = await _repo.GetByIdAsync(id);
        if (existing is null) return NotFound();

        updated.Id = id; // keep id stable
        await _repo.UpdateAsync(id, updated);
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