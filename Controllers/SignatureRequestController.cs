using Microsoft.AspNetCore.Mvc;
using DynamicFormBuilder.Services;

[ApiController]
[Route("api/signature-requests")]
public class SignatureRequestsController : ControllerBase
{
    private readonly SignatureRequestRepository _signatureRepo;
    private readonly FormSubmissionRepository _submissionRepo;
    private readonly IConfiguration _configuration;

    public SignatureRequestsController(
        SignatureRequestRepository signatureRepo,
        IConfiguration configuration,
        FormSubmissionRepository submissionRepo)
    {
        _signatureRepo = signatureRepo;
        _submissionRepo = submissionRepo;
        _configuration = configuration;
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> GetByToken(string token)
    {
        var tokenHash = TokenHelper.ComputeSha256(token);
        var req = await _signatureRepo.GetByTokenHashAsync(tokenHash);
        if (req is null) return NotFound();

        if (req.ExpiresAtUtc < DateTime.UtcNow)
            return BadRequest("Signature link expired.");

        var submission = await _submissionRepo.GetByIdAsync(req.SubmissionId);
        if (submission is null) return NotFound();

        return Ok(new
        {
            SignatureFieldId = req.SignatureFieldId,
            Submission = submission
        });
    }

    [HttpPost("{token}/sign")]
    public async Task<IActionResult> Sign(string token, [FromBody] SignSubmissionRequest request)
    {
        var tokenHash = TokenHelper.ComputeSha256(token);
        var req = await _signatureRepo.GetByTokenHashAsync(tokenHash);
        if (req is null) return NotFound();

        if (req.ExpiresAtUtc < DateTime.UtcNow)
            return BadRequest("Signature link expired.");

        var submission = await _submissionRepo.GetByIdAsync(req.SubmissionId);
        if (submission is null) return NotFound();

        var signature = submission.Signatures
            .FirstOrDefault(x => x.FieldId == req.SignatureFieldId);

        if (signature is null)
        {
            signature = new FormSignature
            {
                FieldId = req.SignatureFieldId
            };
            submission.Signatures.Add(signature);
        }

        signature.SignedByEmail = request.SignedByEmail;
        signature.SignatureUrl = SubmissionHelper.SaveSignatureIfNeeded(request.SignatureDataBase64, _configuration);
        signature.SignedAtUtc = DateTime.UtcNow;

        submission.UpdatedAtUtc = DateTime.UtcNow;
        submission.RowVersion++;

        SubmissionHelper.UpdateSubmissionStatus(submission);

        await _submissionRepo.UpdateAsync(submission.Id!, submission);
        return NoContent();
    }
}