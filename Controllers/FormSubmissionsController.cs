using DynamicFormBuilder.Models;
using DynamicFormBuilder.Models.Common;
using DynamicFormBuilder.Models.Form;
using DynamicFormBuilder.Models.Submission;
using DynamicFormBuilder.Repositories.Auth;
using DynamicFormBuilder.Repositories.Form;
using DynamicFormBuilder.Repositories.Submission;
using DynamicFormBuilder.Services;
using DynamicFormBuilder.Services.Pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

[ApiController]
[Route("api/submissions")]
public class FormSubmissionsController : ControllerBase
{
    private readonly IFormRepository _formRepo;
    private readonly ISubmissionPdfFactory _submissionPdfFactory;
    private readonly ISubmissionSettingsRepository _submissionSettingsRepository;
    private readonly IAuthRepository _authRepo;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly IPdfService _pdfService;
    private readonly ISubmissionAccessTokenRepository _submissionAccessTokenRepository;
    private readonly IFormSubmissionRepository _formSubmissionRepository;

    public FormSubmissionsController(
        IFormRepository formRepo,
        ISubmissionPdfFactory submissionPdfFactory,
        ISubmissionSettingsRepository submissionSettingsRepository,
        IAuthRepository authRepo,
        IConfiguration configuration,
        IEmailService emailService,
        IPdfService pdfService,
        IFormSubmissionRepository formSubmissionRepository,
        ISubmissionAccessTokenRepository submissionAccessTokenRepository)
    {
        _formRepo = formRepo;
        _submissionPdfFactory = submissionPdfFactory;
        _submissionSettingsRepository = submissionSettingsRepository;
        _authRepo = authRepo;
        _emailService = emailService;
        _configuration = configuration;
        _pdfService = pdfService;
        _formSubmissionRepository = formSubmissionRepository;
        _submissionAccessTokenRepository = submissionAccessTokenRepository;
    }

    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<List<FormSubmission>>> GetMine(
    [FromQuery] string? search,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? sortField = null,
    [FromQuery] string? sortDir = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var submissions = await _formSubmissionRepository
            .GetMineAsync(userId, search, page, pageSize, sortField, sortDir);

        return Ok(submissions);
    }

    [Authorize]
    [HttpGet("mine/summary")]
    public async Task<ActionResult<SubmissionSummaryResponse>> GetMineSummary(
    [FromQuery] DateTime? start,
    [FromQuery] DateTime? end)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var summary = await _formSubmissionRepository.GetMineSummaryAsync(userId, start, end);

        return Ok(summary);
    }

    [Authorize]
    [HttpGet("mine/trend")]
    public async Task<ActionResult<SubmissionTrendResponse>> GetMineTrend(
    [FromQuery] DateTime? start,
    [FromQuery] DateTime? end)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var endUtc = end ?? DateTime.UtcNow;
        var startUtc = start ?? endUtc.AddDays(-29);

        if (startUtc > endUtc)
        {
            return BadRequest(new ApiError
            {
                Code = "INVALID_DATE_RANGE",
                Message = "Start date cannot be greater than end date."
            });
        }

        var trend = await _formSubmissionRepository.GetMineTrendAsync(userId, start, end);

        return Ok(trend);
    }

    /*
     this area for internal users to create a new submission for a form.
     */
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

        var ownerFields = form.Fields
            .Where(f => f.AssignedTo == AssignedTo.You)
            .ToList();

        var ownerFieldIds = ownerFields
            .Select(f => f.FieldId)
            .ToHashSet();

        var signatureFieldIds = ownerFields
            .Where(f => f.Type == "Signature")
            .Select(f => f.FieldId)
            .ToHashSet();

        var normalizedAnswers = request.Answers
            .Where(x => ownerFieldIds.Contains(x.FieldId))
            .Select(x =>
            {
                var field = ownerFields.First(f => f.FieldId == x.FieldId);
                var normalizedValue = x.Value;

                if (field.Type == "Signature")
                {
                    normalizedValue = SubmissionHelper.SaveSignatureIfNeeded(x.Value, _configuration);
                }

                return new FormAnswer
                {
                    FieldId = x.FieldId,
                    Value = normalizedValue
                };
            })
            .ToList();

        var requestMetadata = new RequestMetadata
        {
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        var hasClientStep = form.Fields.Any(f => f.AssignedTo == AssignedTo.Client);

        var submission = new FormSubmission
        {
            FormId = form.Id!,
            FormName = form.FormName,
            RequiresVerification = form.RequiresVerification,
            FormVersion = form.Version,
            AgreementContentHtml = form.AgreementContentHtml,
            Status = SubmissionStatus.Drafted,
            HasClientStep = hasClientStep,
            OwnerConfirmed = false,
            ExternalConfirmed = false,
            CreatedByUserId = userId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
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
                SignedAtUtc = now,
                SignedFromIpAddress = requestMetadata.IpAddress,
                SignedUserAgent = requestMetadata.UserAgent
            })
            .ToList(),
            RowVersion = 1
        };

        SubmissionHelper.UpdateSubmissionStatus(submission);
        await _formSubmissionRepository.CreateAsync(submission);
        return Ok(submission);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FormSubmission>> GetById(
    string id,
    [FromQuery] string? accessToken,
    [FromQuery] string? verifyToken)
    {
        var submission = await _formSubmissionRepository.GetByIdAsync(id);

        if (submission is null)
            return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // 1) Internal owner flow
        if (!string.IsNullOrWhiteSpace(userId) && submission.CreatedByUserId == userId)
            return Ok(submission);

        // 2) Verify flow
        if (!string.IsNullOrWhiteSpace(verifyToken))
        {
            var verifyTokenHash = TokenHelper.ComputeSha256(verifyToken);
            var token = await _submissionAccessTokenRepository.GetByTokenHashAsync(verifyTokenHash);

            if (token is null || token.IsRevoked || token.ExpiresAtUtc < DateTime.UtcNow)
                return Forbid();

            if (token.SubmissionId != id)
                return Forbid();

            if (token.Purpose != Purpose.ReadSubmission)
                return Forbid();

            if (submission.Status != SubmissionStatus.Completed)
                return BadRequest(new ApiError
                {
                    Code = "DOCUMENT_NOT_FINALIZED",
                    Message = "This submission is not completed yet."
                });

            return Ok(submission);
        }

        // 3) Edit flow
        if (string.IsNullOrWhiteSpace(accessToken))
            return Forbid();

        var tokenHash = TokenHelper.ComputeSha256(accessToken);
        var access = await _submissionAccessTokenRepository.GetByTokenHashAsync(tokenHash);

        if (access is null || access.IsRevoked || access.ExpiresAtUtc < DateTime.UtcNow)
            return Forbid();

        if (access.SubmissionId != id)
            return Forbid();

        if (access.Purpose != Purpose.EditSubmission)
            return Forbid();

        if (submission.Status == SubmissionStatus.Completed)
            return BadRequest(new ApiError
            {
                Code = "SUBMISSION_COMPLETED",
                Message = "This submission has already been completed."
            });

        if (submission.Status == SubmissionStatus.Cancelled)
            return BadRequest(new ApiError
            {
                Code = "SUBMISSION_CANCELLED",
                Message = "This submission has been cancelled."
            });

        if (submission.Status == SubmissionStatus.Expired)
            return BadRequest(new ApiError
            {
                Code = "SUBMISSION_EXPIRED",
                Message = "This submission has expired."
            });

        return Ok(submission);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateFormSubmissionRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var existing = await _formSubmissionRepository.GetByIdAsync(id);
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
            SubmissionStatus.Expired,
            SubmissionStatus.Pending,
        };

        if (lockedStatuses.Contains(existing.Status))
        {
            return StatusCode(403, new
            {
                message = "This submission is locked and cannot be modified."
            });
        }

        var ownerFields = existing.FieldsSnapshot
            .Where(f => f.AssignedTo == AssignedTo.You)
            .ToList();

        var ownerFieldIds = ownerFields
            .Select(f => f.FieldId)
            .ToHashSet();

        var incomingOwnerAnswers = request.Answers
            .Where(x => ownerFieldIds.Contains(x.FieldId))
            .ToList();

        foreach (var incoming in incomingOwnerAnswers)
        {
            var normalizedValue = incoming.Value;

            if (ownerFields.Any(f => f.FieldId == incoming.FieldId && f.Type == "Signature"))
            {
                normalizedValue = SubmissionHelper.SaveSignatureIfNeeded(incoming.Value, _configuration);
            }

            var existingAnswer = existing.Answers.FirstOrDefault(a => a.FieldId == incoming.FieldId);

            if (existingAnswer is null)
            {
                existing.Answers.Add(new FormAnswer
                {
                    FieldId = incoming.FieldId,
                    Value = normalizedValue
                });
            }
            else
            {
                existingAnswer.Value = normalizedValue;
            }
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Email);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var requestMetadata = new RequestMetadata
        {
            IpAddress = ip,
            UserAgent = userAgent
        };

        SyncSignatures(existing, request.Answers, userId, userEmail, requestMetadata);

        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.RowVersion++;

        SubmissionHelper.UpdateSubmissionStatus(existing);
        await _formSubmissionRepository.UpdateAsync(existing);
        return NoContent();
    }

    [Authorize]
    [HttpPost("{submissionId}/cancel")]
    public async Task<ActionResult> CancelSubmission(string submissionId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var submission = await _formSubmissionRepository.GetByIdAsync(submissionId);
        if (submission is null)
            return NotFound("Submission not found.");

        if (submission.CreatedByUserId != userId)
            return Forbid();

        if (submission.Status == SubmissionStatus.Completed)
            return BadRequest("Completed submissions cannot be cancelled.");

        if (submission.Status == SubmissionStatus.Cancelled)
            return BadRequest("Submission is already cancelled.");

        if (submission.Status == SubmissionStatus.Expired)
            return BadRequest("Expired submissions cannot be cancelled.");

        submission.Status = SubmissionStatus.Cancelled;
        submission.UpdatedAtUtc = DateTime.UtcNow;

        await _formSubmissionRepository.UpdateAsync(submission);

        await _submissionAccessTokenRepository.DeleteBySubmissionIdAsync(submission.Id!);

        return Ok(new
        {
            message = "Submission cancelled successfully."
        });
    }

    // Sending an access link to an external recipient for a submission || send a completed submission pdf
    [Authorize]
    [HttpPost("{submissionId}/send-to-external")]
    public async Task<ActionResult> SendToExternal(
    string submissionId,
    [FromBody] SendSubmissionAccessTokenRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var submission = await _formSubmissionRepository.GetByIdAsync(submissionId);
        if (submission is null)
            return NotFound();

        if (submission.CreatedByUserId != userId)
            return Forbid();

        if (submission.Status is SubmissionStatus.Cancelled or SubmissionStatus.Expired)
        {
            return BadRequest(new ApiError
            {
                Code = "SUBMISSION_LOCKED",
                Message = "This submission cannot be sent."
            });
        }

        var missingOwnerRequired = SubmissionHelper
            .GetMissingRequiredFields(submission, AssignedTo.You);

        if (missingOwnerRequired.Any())
        {
            return BadRequest(new ApiError
            {
                Code = "OWNER_FIELDS_INCOMPLETE",
                Message = "Owner required fields must be completed before sending."
            });
        }

        var nowUtc = DateTime.UtcNow;

        submission.OwnerConfirmed = true;
        submission.OwnerConfirmedAtUtc = nowUtc;
        submission.UpdatedAtUtc = nowUtc;
        submission.RowVersion++;

        var hasClientStep = SubmissionHelper.HasClientStep(submission);

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Email is required.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var fullName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;


        // If client doesnt have any steps to completed, we can send the completed PDF directly to the external recipient.
        if (!hasClientStep)
        {
            submission.ExternalRecipientEmail = normalizedEmail;
            submission.SentToExternalAtUtc = null;

            SubmissionHelper.DisableReminder(submission);
            SubmissionHelper.UpdateSubmissionStatus(submission);

            await _formSubmissionRepository.UpdateAsync(submission);

            var pdfBytes = await _submissionPdfFactory.GenerateAsync(submission);

            await _emailService.SendCompletedSubmissionPdfEmailAsync(
                userId,
                normalizedEmail,
                request.Subject,
                fullName,
                submission.FormName,
                pdfBytes,
                submissionId);

            return Ok(new
            {
                message = "Completed submission PDF sent successfully."
            });
        }

        await _submissionAccessTokenRepository.RevokeActiveTokensBySubmissionIdAsync(submissionId);
        var settings = await _submissionSettingsRepository.GetByUserIdAsync(userId);

        var tokenLifetimeDays = settings?.DefaultAccessTokenLifetimeDays ?? 3;
        var reminderEnabled = settings?.ReminderEnabledByDefault ?? false;
        var reminderIntervalDays = settings?.DefaultReminderIntervalDays ?? 3;
        var maxReminderCount = settings?.MaxReminderCount ?? 3;

        var rawAccessToken = TokenHelper.GenerateSecureToken();
        var accessTokenHash = TokenHelper.ComputeSha256(rawAccessToken);

        var accessToken = new SubmissionAccessToken
        {
            SubmissionId = submission.Id!,
            Email = normalizedEmail,
            TokenHash = accessTokenHash,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.AddDays(tokenLifetimeDays),
            Purpose = Purpose.EditSubmission
        };

        SubmissionHelper.ApplyReminderSettings(
            submission,
            reminderEnabled,
            reminderIntervalDays,
            maxReminderCount,
            nowUtc
            );

        await _submissionAccessTokenRepository.CreateAsync(accessToken);

        var frontendBaseUrl = _configuration["App:FrontendBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            throw new InvalidOperationException("App:FrontendBaseUrl is missing.");

        var accessUrl =
            $"{frontendBaseUrl}/submission-access?token={Uri.EscapeDataString(rawAccessToken)}";

        await _emailService.SendSubmissionSignerEmailAsync(
            userId,
            normalizedEmail,
            request.Subject,
            accessUrl,
            fullName,
            submission.FormName,
            submissionId);

        submission.ExternalRecipientEmail = normalizedEmail;
        submission.SentToExternalAtUtc = nowUtc;

        SubmissionHelper.UpdateSubmissionStatus(submission);
        await _formSubmissionRepository.UpdateAsync(submission);

        return Ok(new
        {
            message = "Access link sent successfully."
        });
    }

    // Resolving access for external users using the access token to determine if they can access the submission and if they are authenticated.
    [AllowAnonymous]
    [HttpPost("access/resolve")]
    public async Task<ActionResult<ResolveSubmissionAccessResponse>> ResolveSubmissionAccess(
    [FromBody] ResolveSubmissionAccessRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new ApiError
            {
                Code = "TOKEN_REQUIRED",
                Message = "Token is required."
            });

        var tokenHash = TokenHelper.ComputeSha256(request.Token);

        var accessToken = await _submissionAccessTokenRepository.GetByTokenHashAsync(tokenHash);
        if (accessToken is null)
            return BadRequest(new ApiError
            {
                Code = "SUBMISSION_CANCELLED",
                Message = "This submission has been cancelled."
            });

        var submission = await _formSubmissionRepository.GetByIdAsync(accessToken.SubmissionId);

        if (submission is null)
            return NotFound();

        if (submission.Status == SubmissionStatus.Completed)
            return BadRequest(new ApiError
            {
                Code = "SUBMISSION_COMPLETED",
                Message = "This submission has already been completed."
            });

        if (submission.Status == SubmissionStatus.Cancelled)
            return BadRequest(new ApiError
            {
                Code = "SUBMISSION_COMPLETED",
                Message = "This submission has been cancelled."
            });

        if (submission.Status == SubmissionStatus.Expired)
            return BadRequest(new ApiError
            {
                Code = "SUBMISSION_EXPIRED",
                Message = "This submission has expired."
            });

        if (accessToken.IsRevoked)
            return BadRequest(new ApiError
            {
                Code = "TOKEN_REVOKED",
                Message = "Access token has been revoked."
            });

        if (accessToken.ExpiresAtUtc < DateTime.UtcNow)
            return BadRequest(new ApiError
            {
                Code = "TOKEN_EXPIRED",
                Message = "Access token has expired."
            });

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
            RequiresVerification = submission.RequiresVerification,
            IsAuthenticated = isAuthenticated,
            IsEmailMatch = isEmailMatch
        });
    }

    // External users can update their submission using the access token.
    [AllowAnonymous]
    [HttpPut("access/{id}")]
    public async Task<IActionResult> UpdateByAccessToken(
    string id,
    [FromBody] UpdateSubmissionByAccessTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest("Token is required.");

        var existing = await _formSubmissionRepository.GetByIdAsync(id);
        if (existing is null)
            return NotFound();

        if (existing.Status != SubmissionStatus.Pending)
            return BadRequest("This submission is not editable.");

        if (existing.RowVersion != request.RowVersion)
            return Conflict("This record was changed by another user.");

        var tokenHash = TokenHelper.ComputeSha256(request.Token);

        var accessToken = await _submissionAccessTokenRepository.GetByTokenHashAsync(tokenHash);
        if (accessToken is null)
            return Forbid();

        if (accessToken.Purpose != Purpose.EditSubmission)
            return Unauthorized();

        if (accessToken.IsRevoked)
        {
            return BadRequest(new
            {
                code = "LINK_REVOKED",
                message = "This access link is no longer valid because a newer link was sent."
            });
        }

        if (accessToken.ExpiresAtUtc < DateTime.UtcNow)
            return BadRequest("Access token has expired.");

        if (accessToken.UsedAtUtc != null)
            return BadRequest("Access token has already been used.");

        if (accessToken.SubmissionId != id)
            return Forbid();

        if (request.Answers is null)
            return BadRequest("Answers are required.");

        var allFieldIds = existing.FieldsSnapshot
            .Select(f => f.FieldId)
            .ToHashSet();

        var unknownFieldIds = request.Answers
            .Where(x => !allFieldIds.Contains(x.FieldId))
            .Select(x => x.FieldId)
            .Distinct()
            .ToList();

        if (unknownFieldIds.Any())
            return BadRequest("Unknown field id.");

        var externalFieldIds = existing.FieldsSnapshot
            .Where(f => f.AssignedTo == AssignedTo.Client)
            .Select(f => f.FieldId)
            .ToHashSet();

        var invalidFieldIds = request.Answers
            .Where(x => !externalFieldIds.Contains(x.FieldId))
            .Select(x => x.FieldId)
            .Distinct()
            .ToList();

        if (invalidFieldIds.Any())
            return Forbid();

        var incomingAnswers = request.Answers
            .Where(x => externalFieldIds.Contains(x.FieldId))
            .ToList();

        var nowUtc = DateTime.UtcNow;

        foreach (var answer in incomingAnswers)
        {
            var normalizedValue = answer.Value;

            if (existing.FieldsSnapshot.Any(f => f.FieldId == answer.FieldId && f.Type == "Signature"))
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

        var ip = HttpContext?.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request?.Headers.UserAgent.ToString();

        var requestMetadata = new RequestMetadata
        {
            IpAddress = ip,
            UserAgent = userAgent
        };

        SyncSignatures(existing, incomingAnswers, null, accessToken.Email, requestMetadata);
        SyncAgreementAcceptances(existing, incomingAnswers, null, accessToken.Email, requestMetadata);

        var externalAgreementFields = existing.FieldsSnapshot
            .Where(f => f.AssignedTo == AssignedTo.Client && f.Type == "Agreement")
            .ToList();

        var acceptedExternalAgreementFieldIds = incomingAnswers
            .Where(a =>
                externalAgreementFields.Any(f => f.FieldId == a.FieldId) &&
                bool.TryParse(a.Value, out var accepted) &&
                accepted)
            .Select(a => a.FieldId)
            .ToHashSet();

        var requiredExternalAgreementFields = externalAgreementFields
            .Where(f => f.Required)
            .ToList();

        foreach (var field in requiredExternalAgreementFields)
        {
            if (!acceptedExternalAgreementFieldIds.Contains(field.FieldId))
            {
                return BadRequest(new
                {
                    code = "AGREEMENT_ACCEPTANCE_REQUIRED",
                    message = $"Agreement '{field.Label}' must be accepted.",
                    fieldId = field.FieldId
                });
            }
        }

        var requiredExternalFields = existing.FieldsSnapshot
            .Where(f => f.AssignedTo == AssignedTo.Client && f.Required && f.Type != "Agreement")
            .ToList();

        foreach (var field in requiredExternalFields)
        {
            var answer = existing.Answers.FirstOrDefault(x => x.FieldId == field.FieldId);

            var isEmpty = answer == null || string.IsNullOrWhiteSpace(answer.Value);

            if (isEmpty)
            {
                return BadRequest(new
                {
                    code = "REQUIRED_FIELD_MISSING",
                    message = $"Field '{field.Label}' is required.",
                    fieldId = field.FieldId
                });
            }
        }

        existing.ExternalConfirmed = true;
        existing.ExternalConfirmedAtUtc = nowUtc;
        existing.UpdatedAtUtc = nowUtc;
        existing.RowVersion++;

        SubmissionHelper.DisableReminder(existing);
        SubmissionHelper.UpdateSubmissionStatus(existing);

        await _formSubmissionRepository.UpdateAsync(existing);

        if (existing.Status == SubmissionStatus.Completed)
        {
            await _submissionAccessTokenRepository.RevokeActiveTokensBySubmissionIdAsync(id);

            var ownerUser = await _authRepo.GetByIdAsync(existing.CreatedByUserId);

            if (ownerUser != null && ownerUser.NotificationsEnabled)
            {
                var frontendBaseUrl = _configuration["App:FrontendBaseUrl"]?.TrimEnd('/');

                if (string.IsNullOrWhiteSpace(frontendBaseUrl))
                    throw new InvalidOperationException("App:FrontendBaseUrl is missing.");

                var submissionUrl = $"{frontendBaseUrl}/dashboard/submissions/{existing.Id}/view";
                await _emailService.SendSubmissionCompletedEmailAsync(ownerUser.Email, submissionUrl, ownerUser.FullName);
            }

            var normalizedEmail = existing.ExternalRecipientEmail?.Trim().ToLowerInvariant();
            var pdfBytes = await _submissionPdfFactory.GenerateAsync(existing);

            if (!string.IsNullOrWhiteSpace(normalizedEmail))
            {
                await _emailService.SendCompletedSubmissionPdfEmailAsync(
                String.Empty,
                normalizedEmail,
                $"Completed PDF - {existing.FormName}",
                string.Empty,
                existing.FormName,
                pdfBytes,
                existing.Id);
            }

            return Ok(new
            {
                message = "Completed submission PDF sent successfully."
            });
        }

        return NoContent();
    }

    [HttpGet("verification-pdf-access")]
    public async Task<IActionResult> VerifyPdfAccess([FromQuery(Name = "verifyToken")] string verifyToken)
    {
        if (string.IsNullOrWhiteSpace(verifyToken))
            return Unauthorized();

        var tokenHash = TokenHelper.ComputeSha256(verifyToken);

        var accessToken = await _submissionAccessTokenRepository.GetByTokenHashAsync(tokenHash);
        if (accessToken == null)
            return Unauthorized();

        if (accessToken.IsRevoked || accessToken.RevokedAtUtc.HasValue)
            return Unauthorized();

        if (accessToken.ExpiresAtUtc < DateTime.UtcNow)
            return Unauthorized();

        if (accessToken.Purpose != Purpose.ReadSubmission)
            return Unauthorized();

        var submission = await _formSubmissionRepository.GetByIdAsync(accessToken.SubmissionId);
        if (submission == null)
            return NotFound();

        if (submission.Status != SubmissionStatus.Completed)
            return BadRequest(new
            {
                code = "DOCUMENT_NOT_FINALIZED",
                message = "This submission is not completed yet."
            });

        return Ok(new ResolveVerifyTokenResponse
        {
            SubmissionId = submission.Id!,
            RequiresVerification = submission.RequiresVerification
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
        {
            return BadRequest(new
            {
                code = "LINK_REVOKED",
                message = "This access link is no longer valid because a newer link was sent."
            });
        }

        if (accessToken.ExpiresAtUtc < DateTime.UtcNow)
            return BadRequest("Access token has expired.");

        var submission = await _formSubmissionRepository.GetByIdAsync(accessToken.SubmissionId);
        if (submission is null)
            return NotFound("Submission not found.");

        return Ok(submission);
    }

    private void SyncSignatures(
    FormSubmission submission,
    List<FormAnswerDto> answers,
    string? userId,
    string? userEmail,
    RequestMetadata? requestMetadata)
    {
        var now = DateTime.UtcNow;

        submission.Signatures ??= new List<FormSignature>();

        var signatureFieldIds = submission.FieldsSnapshot
            .Where(f => f.Type == "Signature")
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
                    SignedAtUtc = now,
                    SignedFromIpAddress = requestMetadata?.IpAddress,
                    SignedUserAgent = requestMetadata?.UserAgent
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

    // Download pdf
    [Authorize]
    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> DownloadPdf(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var submission = await _formSubmissionRepository.GetByIdAsync(id);
        if (submission is null)
            return NotFound();

        if (submission.CreatedByUserId != userId)
            return Forbid();

        var pdfBytes = await _submissionPdfFactory.GenerateAsync(submission);

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

            var submission = await _formSubmissionRepository.GetByIdAsync(id);
            if (submission is null)
                return NotFound();

            var existing = await _formSubmissionRepository.GetByIdAsync(id);

            if (existing is null)
                return NotFound();

            if (existing.Status == SubmissionStatus.Cancelled)
                return BadRequest("This submission has been cancelled.");

            if (existing.Status == SubmissionStatus.Expired)
                return BadRequest("This submission has expired.");

            var pdfBytes = await _submissionPdfFactory.GenerateAsync(submission);

            return File(pdfBytes, "application/pdf", $"submission-{submission.Id}.pdf");
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    private static void SyncAgreementAcceptances(
    FormSubmission submission,
    List<FormAnswerDto> incomingAnswers,
    string? acceptedByUserId,
    string? acceptedByEmail,
    RequestMetadata? requestMetadata)
    {
        if (submission.FieldsSnapshot == null || submission.FieldsSnapshot.Count == 0)
            return;

        submission.AgreementAcceptances ??= new List<FormAgreementAcceptance>();

        var agreementFields = submission.FieldsSnapshot
            .Where(f => f.Type == "Agreement")
            .ToList();

        if (!agreementFields.Any())
            return;

        var acceptedAgreementFieldIds = incomingAnswers
            .Where(a =>
                agreementFields.Any(f => f.FieldId == a.FieldId) &&
                bool.TryParse(a.Value, out var accepted) &&
                accepted)
            .Select(a => a.FieldId)
            .ToHashSet();

        foreach (var fieldId in acceptedAgreementFieldIds)
        {
            var existingAcceptance = submission.AgreementAcceptances
                .FirstOrDefault(x => x.FieldId == fieldId);

            if (existingAcceptance == null)
            {
                submission.AgreementAcceptances.Add(new FormAgreementAcceptance
                {
                    FieldId = fieldId,
                    AcceptedByUserId = acceptedByUserId,
                    AcceptedByEmail = acceptedByEmail,
                    AcceptedAtUtc = DateTime.UtcNow,
                    AcceptedFromIpAddress = requestMetadata?.IpAddress,
                    AcceptedUserAgent = requestMetadata?.UserAgent
                });
            }
        }
    }
}
