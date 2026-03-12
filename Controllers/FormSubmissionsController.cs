using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using DynamicFormBuilder.Services;

[ApiController]
[Route("api/form-submissions")]
public class FormSubmissionsController : ControllerBase
{
    private readonly FormRepository _formRepo;
    private readonly FormSubmissionRepository _submissionRepo;

    public FormSubmissionsController(
        FormRepository formRepo,
        FormSubmissionRepository submissionRepo)
    {
        _formRepo = formRepo;
        _submissionRepo = submissionRepo;
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<FormSubmission>> Create([FromBody] CreateFormSubmissionRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var form = await _formRepo.GetByIdAsync(request.FormId);
        if (form is null)
            return NotFound("Form not found.");

        var submission = new FormSubmission
        {
            FormId = form.Id!,
            FormVersion = form.Version,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Status = SubmissionStatus.Draft,
            Answers = request.Answers.Select(x => new FormAnswer
            {
                FieldId = x.FieldId,
                Value = x.Value
            }).ToList(),
            Signatures = new List<FormSignature>(),
            RowVersion = 1
        };

        await _submissionRepo.CreateAsync(submission);
        return Ok(submission);
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<ActionResult<FormSubmission>> GetById(string id)
    {
        var submission = await _submissionRepo.GetByIdAsync(id);
        if (submission is null)
            return NotFound();

        return Ok(submission);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateFormSubmissionRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var existing = await _submissionRepo.GetByIdAsync(id);
        if (existing is null)
            return NotFound();

        if (existing.CreatedByUserId != userId)
            return Forbid();

        if (existing.RowVersion != request.RowVersion)
            return Conflict("This record was changed by another user.");

        existing.Answers = request.Answers.Select(x => new FormAnswer
        {
            FieldId = x.FieldId,
            Value = x.Value
        }).ToList();

        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.RowVersion++;

        await _submissionRepo.UpdateAsync(id, existing);
        return NoContent();
    }
}