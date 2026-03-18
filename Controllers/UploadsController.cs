using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using DynamicFormBuilder.Services;
using System.IdentityModel.Tokens.Jwt;
using DynamicFormBuilder.Models;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using ImageSharpSize = SixLabors.ImageSharp.Size;

[ApiController]
[Route("api/uploads")]
public class UploadsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly SubmissionAccessTokenRepository _submissionAccessTokenRepository;

    public UploadsController(IConfiguration configuration, SubmissionAccessTokenRepository submissionAccessTokenRepository)
    {
        _configuration = configuration;
        _submissionAccessTokenRepository = submissionAccessTokenRepository;
    }

    [AllowAnonymous]
    [HttpPost("signature")]
    public async Task<ActionResult<object>> UploadSignature(
    [FromForm] IFormFile file,
    [FromQuery] string? accessToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                return Forbid();

            var tokenHash = TokenHelper.ComputeSha256(accessToken);
            var token = await _submissionAccessTokenRepository.GetByTokenHashAsync(tokenHash);

            if (token is null || token.IsRevoked || token.ExpiresAtUtc < DateTime.UtcNow)
                return Forbid();
        }

        if (file == null || file.Length == 0)
            return BadRequest("File is required.");

        if (file.Length > 2 * 1024 * 1024)
            return BadRequest("File too large. Max 2MB allowed.");

        if (!file.ContentType.StartsWith("image/"))
            return BadRequest("Only image files are allowed.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp" };

        if (!allowedExtensions.Contains(extension))
            return BadRequest("Unsupported file type.");

        var uploadsRoot = _configuration["UploadSettings:PhysicalRoot"];
        if (string.IsNullOrWhiteSpace(uploadsRoot))
            throw new InvalidOperationException("UploadSettings:PhysicalRoot is missing.");

        var signaturesRoot = Path.Combine(uploadsRoot, "signatures");
        Directory.CreateDirectory(signaturesRoot);

        var fileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(signaturesRoot, fileName);

        await using var inputStream = file.OpenReadStream();
        using var image = await ImageSharpImage.LoadAsync(inputStream);

        image.Mutate(x =>
        {
            x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new ImageSharpSize(1200, 400)
            });
        });

        await using var outputStream = new FileStream(fullPath, FileMode.Create);
        await image.SaveAsync(outputStream, new PngEncoder());

        return Ok(new
        {
            fileName,
            url = $"/uploads/signatures/{fileName}"
        });
    }
}