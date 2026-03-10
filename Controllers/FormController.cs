using DynamicFormBuilder.Services;
using Microsoft.AspNetCore.Mvc;

namespace FormBuilderApi.Controllers;

[ApiController]
[Route("api/forms")]
public class FormsController : ControllerBase
{
    private readonly FormRepository _repo;

    public FormsController(FormRepository repo) => _repo = repo;

    [HttpGet]
    public async Task<ActionResult<List<FormDefinition>>> GetAll()
        => Ok(await _repo.GetAllAsync());

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

        // Let Mongo create Id
        form.Id = null;

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