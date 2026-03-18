using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using DynamicFormBuilder.Services;
using System.IdentityModel.Tokens.Jwt;
using DynamicFormBuilder.Models;

[ApiController]
[Route("api/submissions")]
public class FormSubmissionsController : ControllerBase
{
    private readonly FormRepository _formRepo;
    private readonly FormSubmissionRepository _submissionRepo;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly IPdfService _pdfService;
    private readonly SubmissionAccessTokenRepository _submissionAccessTokenRepository;

    public FormSubmissionsController(
        FormRepository formRepo,
        FormSubmissionRepository submissionRepo,
        IConfiguration configuration,
        IEmailService emailService,
        IPdfService pdfService,
        SubmissionAccessTokenRepository submissionAccessTokenRepository)
    {
        _formRepo = formRepo;
        _emailService = emailService;
        _submissionRepo = submissionRepo;
        _configuration = configuration;
        _pdfService = pdfService;
        _submissionAccessTokenRepository = submissionAccessTokenRepository;
    }

    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<List<FormSubmission>>> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var submissions = await _submissionRepo.GetByUserIdAsync(userId);

        return Ok(submissions);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<FormSubmission>> Create([FromBody] CreateFormSubmissionRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var userEmail = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Email);

        var now = DateTime.UtcNow;

        var form = await _formRepo.GetByIdAsync(request.FormId);
        if (form is null)
            return NotFound("Form not found.");

        var signatureFieldIds = form.Fields
            .Where(f => f.Type == "signaturePad")
            .Select(f => f.FieldId)
            .ToHashSet();

        var normalizedAnswers = request.Answers.Select(x =>
        {
            var field = form.Fields.FirstOrDefault(f => f.FieldId == x.FieldId);
            var normalizedValue = x.Value;

            if (field?.Type == "signaturePad")
            {
                normalizedValue = SubmissionHelper.SaveSignatureIfNeeded(x.Value, _configuration);
            }

            return new FormAnswer
            {
                FieldId = x.FieldId,
                Value = normalizedValue
            };
        }).ToList();

        var submission = new FormSubmission
        {
            FormId = form.Id!,
            FormName = form.FormName,
            FormVersion = form.Version,
            CreatedByUserId = userId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Status = SubmissionStatus.Draft,
            FieldsSnapshot = form.Fields.Select(f => new FieldDefinition
            {
                FieldId = f.FieldId,
                Label = f.Label,
                Type = f.Type,
                AssignedTo = f.AssignedTo,
                Agreement = f.Agreement == null
                ? null
                : new AgreementSnapshot
                {
                    Id = f.Agreement.Id,
                    Title = f.Agreement.Title,
                    Content = f.Agreement.Content
                },
                Required = f.Required,
                Min = f.Min,
                Max = f.Max,
                MinLength = f.MinLength,
                MaxLength = f.MaxLength,
                Pattern = f.Pattern,
                Options = f.Options,
                ColSpan = f.ColSpan
            }).ToList(),
            Answers = normalizedAnswers,
            Signatures = normalizedAnswers
                .Where(x => signatureFieldIds.Contains(x.FieldId) && !string.IsNullOrWhiteSpace(x.Value))
                .Select(x => new FormSignature
                {
                    FieldId = x.FieldId,
                    SignedByUserId = userId,
                    SignedByEmail = userEmail,
                    SignatureUrl = x.Value,
                    SignedAtUtc = now
                })
                .ToList(),
            RowVersion = 1
        };

        SubmissionHelper.UpdateSubmissionStatus(submission);
        await _submissionRepo.CreateAsync(submission);
        return Ok(submission);
    }

    [AllowAnonymous]
    [HttpGet("{id}")]
    public async Task<ActionResult<FormSubmission>> GetById(string id, [FromQuery] string? accessToken)
    {
        var submission = await _submissionRepo.GetByIdAsync(id);

        if (submission is null)
            return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrWhiteSpace(userId) && submission.CreatedByUserId == userId)
            return Ok(submission);

        if (string.IsNullOrWhiteSpace(accessToken))
            return Forbid();

        var tokenHash = TokenHelper.ComputeSha256(accessToken);
        var token = await _submissionAccessTokenRepository.GetByTokenHashAsync(tokenHash);

        if (token is null || token.IsRevoked || token.ExpiresAtUtc < DateTime.UtcNow)
            return Forbid();

        if (token.SubmissionId != id)
            return Forbid();

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

        var lockedStatuses = new[]
        {
            SubmissionStatus.Completed,
            SubmissionStatus.Cancelled,
            SubmissionStatus.Expired
        };

        if (lockedStatuses.Contains(existing.Status))
        {
            return StatusCode(403, new
            {
                message = "This submission is locked and cannot be modified."
            });
        }

        var signatureFieldIds = existing.FieldsSnapshot
            .Where(f => f.Type == "signaturePad")
            .Select(f => f.FieldId)
            .ToHashSet();

        existing.Answers = request.Answers.Select(x =>
        {
            var normalizedValue = x.Value;

            if (signatureFieldIds.Contains(x.FieldId))
            {
                normalizedValue = SubmissionHelper.SaveSignatureIfNeeded(x.Value, _configuration);
                x.Value = normalizedValue;
            }

            return new FormAnswer
            {
                FieldId = x.FieldId,
                Value = normalizedValue
            };
        }).ToList();

        var userEmail = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Email);

        SyncSignatures(existing, request.Answers, userId, userEmail);

        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.RowVersion++;

        await _submissionRepo.UpdateAsync(id, existing);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPut("access/{id}")]
    public async Task<IActionResult> UpdateByAccessToken(
    string id,
    [FromBody] UpdateSubmissionByAccessTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest("Token is required.");

        var existing = await _submissionRepo.GetByIdAsync(id);
        if (existing is null)
            return NotFound();

        if (existing.RowVersion != request.RowVersion)
            return Conflict("This record was changed by another user.");

        var tokenHash = TokenHelper.ComputeSha256(request.Token);

        var accessToken = await _submissionAccessTokenRepository.GetByTokenHashAsync(tokenHash);
        if (accessToken is null)
            return NotFound("Access token not found.");

        if (accessToken.IsRevoked)
            return BadRequest("Access token has been revoked.");

        if (accessToken.ExpiresAtUtc < DateTime.UtcNow)
            return BadRequest("Access token has expired.");

        if (accessToken.SubmissionId != id)
            return Forbid();

        var externalFieldIds = existing.FieldsSnapshot
            .Where(f => f.AssignedTo == AssignedTo.External)
            .Select(f => f.FieldId)
            .ToHashSet();

        var incomingAnswers = request.Answers
            .Where(x => externalFieldIds.Contains(x.FieldId))
            .ToList();

        foreach (var answer in incomingAnswers)
        {
            var normalizedValue = answer.Value;

            if (existing.FieldsSnapshot.Any(f => f.FieldId == answer.FieldId && f.Type == "signaturePad"))
            {
                normalizedValue = SubmissionHelper.SaveSignatureIfNeeded(answer.Value, _configuration);
                answer.Value = normalizedValue;
            }

            var existingAnswer = existing.Answers.FirstOrDefault(x => x.FieldId == answer.FieldId);

            if (existingAnswer is null)
            {
                existing.Answers.Add(new FormAnswer
                {
                    FieldId = answer.FieldId,
                    Value = normalizedValue
                });
            }
            else
            {
                existingAnswer.Value = normalizedValue;
            }
        }

        SyncSignatures(existing, incomingAnswers, null, accessToken.Email);

        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.RowVersion++;

        await _submissionRepo.UpdateAsync(id, existing);
        return NoContent();
    }

    [Authorize]
    [HttpPost("{submissionId}/send-for-signature")]
    public async Task<ActionResult> SendToSigner(
    string submissionId,
    [FromBody] SendSubmissionAccessTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Email is required.");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var submission = await _submissionRepo.GetByIdAsync(submissionId);
        if (submission is null)
            return NotFound("Submission not found.");

        if (submission.CreatedByUserId != userId)
            return Forbid();

        var hasExternalFields = submission.FieldsSnapshot.Any(f => f.AssignedTo == AssignedTo.External);
        if (!hasExternalFields)
            return BadRequest("This submission has no external fields.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var rawAccessToken = TokenHelper.GenerateSecureToken();
        var accessTokenHash = TokenHelper.ComputeSha256(rawAccessToken);

        var accessToken = new SubmissionAccessToken
        {
            SubmissionId = submission.Id!,
            Email = normalizedEmail,
            TokenHash = accessTokenHash,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(3)
        };

        await _submissionAccessTokenRepository.CreateAsync(accessToken);

        var frontendBaseUrl = _configuration["App:FrontendBaseUrl"]?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            throw new InvalidOperationException("App:FrontendBaseUrl is missing.");

        var accessUrl =
            $"{frontendBaseUrl}/submission-access?token={Uri.EscapeDataString(rawAccessToken)}";

        var fullName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

        await _emailService.SendSubmissionSignerEmailAsync(
            normalizedEmail,
            accessUrl,
            fullName,
            "Please review and sign the submission."
        );

        return Ok(new
        {
            message = "Access link sent successfully."
        });
    }

    [AllowAnonymous]
    [HttpPost("access/resolve")]
    public async Task<ActionResult<ResolveSubmissionAccessResponse>> ResolveSubmissionAccess(
        [FromBody] ResolveSubmissionAccessRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest("Token is required.");

        var tokenHash = TokenHelper.ComputeSha256(request.Token);

        var accessToken = await _submissionAccessTokenRepository.GetByTokenHashAsync(tokenHash);
        if (accessToken is null)
            return NotFound("Access token not found.");

        if (accessToken.IsRevoked)
            return BadRequest("Access token has been revoked.");

        if (accessToken.ExpiresAtUtc < DateTime.UtcNow)
            return BadRequest("Access token has expired.");

        var submission = await _submissionRepo.GetByIdAsync(accessToken.SubmissionId);
        if (submission is null)
            return NotFound("Submission not found.");

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var currentUserEmail = User.FindFirstValue(ClaimTypes.Email);

        var isAuthenticated = !string.IsNullOrWhiteSpace(currentUserId);
        var isEmailMatch =
            !string.IsNullOrWhiteSpace(currentUserEmail) &&
            string.Equals(
                currentUserEmail.Trim(),
                accessToken.Email.Trim(),
                StringComparison.OrdinalIgnoreCase);

        return Ok(new ResolveSubmissionAccessResponse
        {
            SubmissionId = submission.Id!,
            Email = accessToken.Email,
            IsAuthenticated = isAuthenticated,
            IsEmailMatch = isEmailMatch
        });
    }

    [AllowAnonymous]
    [HttpPost("access/content")]
    public async Task<ActionResult<FormSubmission>> GetSubmissionByAccessToken(
    [FromBody] ResolveSubmissionAccessRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest("Token is required.");

        var tokenHash = TokenHelper.ComputeSha256(request.Token);

        var accessToken = await _submissionAccessTokenRepository.GetByTokenHashAsync(tokenHash);
        if (accessToken is null)
            return NotFound("Access token not found.");

        if (accessToken.IsRevoked)
            return BadRequest("Access token has been revoked.");

        if (accessToken.ExpiresAtUtc < DateTime.UtcNow)
            return BadRequest("Access token has expired.");

        var submission = await _submissionRepo.GetByIdAsync(accessToken.SubmissionId);
        if (submission is null)
            return NotFound("Submission not found.");

        return Ok(submission);
    }

    private void SyncSignatures(
    FormSubmission submission,
    List<FormAnswerDto> answers,
    string? userId,
    string? userEmail)
    {
        var now = DateTime.UtcNow;

        submission.Signatures ??= new List<FormSignature>();

        var signatureFieldIds = submission.FieldsSnapshot
            .Where(f => f.Type == "signaturePad")
            .Select(f => f.FieldId)
            .ToHashSet();

        var signatureAnswers = answers
            .Where(x => signatureFieldIds.Contains(x.FieldId))
            .ToList();

        foreach (var answer in signatureAnswers)
        {
            var existingSignature = submission.Signatures
                .FirstOrDefault(s => s.FieldId == answer.FieldId);

            if (string.IsNullOrWhiteSpace(answer.Value))
            {
                if (existingSignature is not null)
                {
                    submission.Signatures.Remove(existingSignature);
                }

                continue;
            }

            var normalizedSignatureUrl = SubmissionHelper.SaveSignatureIfNeeded(answer.Value, _configuration);

            if (existingSignature is null)
            {
                submission.Signatures.Add(new FormSignature
                {
                    FieldId = answer.FieldId,
                    SignedByUserId = userId,
                    SignedByEmail = userEmail,
                    SignatureUrl = normalizedSignatureUrl,
                    SignedAtUtc = now
                });
            }
            else
            {
                existingSignature.SignatureUrl = normalizedSignatureUrl;
                existingSignature.SignedAtUtc = now;
                existingSignature.SignedByUserId = userId;
                existingSignature.SignedByEmail = userEmail;
            }
        }

        SubmissionHelper.UpdateSubmissionStatus(submission);
    }

    [Authorize]
    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> DownloadPdf(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var submission = await _submissionRepo.GetByIdAsync(id);
        if (submission is null)
            return NotFound();

        if (submission.CreatedByUserId != userId)
            return Forbid();

        var pdfBytes = await _pdfService.GenerateSubmissionPdfAsync(submission);

        return File(pdfBytes, "application/pdf", $"submission-{submission.Id}.pdf");
    }

    [AllowAnonymous]
    [HttpGet("access/{id}/pdf")]
    public async Task<IActionResult> DownloadPdfByAccessToken(string id, [FromQuery] string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest("Token is required.");

            var tokenHash = TokenHelper.ComputeSha256(token);
            var accessToken = await _submissionAccessTokenRepository.GetByTokenHashAsync(tokenHash);

            if (accessToken is null)
                return NotFound("Access token not found.");

            if (accessToken.IsRevoked || accessToken.ExpiresAtUtc < DateTime.UtcNow)
                return Forbid();

            if (accessToken.SubmissionId != id)
                return Forbid();

            var submission = await _submissionRepo.GetByIdAsync(id);
            if (submission is null)
                return NotFound();

            var pdfBytes = await _pdfService.GenerateSubmissionPdfAsync(submission);

            return File(pdfBytes, "application/pdf", $"submission-{submission.Id}.pdf");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return StatusCode(500, "PDF generation failed.");
        }
    }
}
