using DynamicFormBuilder.Models.Pdf;
using DynamicFormBuilder.Repositories.Branding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DynamicFormBuilder.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/pdf-branding")]
    public class PdfBrandingController : ControllerBase
    {
        private readonly IPdfBrandingSettingsRepository _pdfBrandingSettingsRepository;

        public PdfBrandingController(
            IPdfBrandingSettingsRepository pdfBrandingSettingsRepository)
        {
            _pdfBrandingSettingsRepository = pdfBrandingSettingsRepository;
        }

        [HttpGet]
        public async Task<ActionResult<PdfBrandingSettingsResponse>> Get()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var settings = await _pdfBrandingSettingsRepository.GetByUserIdAsync(userId);

            if (settings is null)
            {
                return Ok(new PdfBrandingSettingsResponse
                {
                    LogoFileUrl = null,
                    CompanyName = null,
                    Website = null,
                    Email = null,
                    Phone = null,
                    Address = null,
                    BrandColorHex = "#8FE3A8"
                });
            }

            return Ok(new PdfBrandingSettingsResponse
            {
                LogoFileUrl = settings.LogoFileUrl,
                CompanyName = settings.CompanyName,
                Website = settings.Website,
                Email = settings.Email,
                Phone = settings.Phone,
                Address = settings.Address,
                BrandColorHex = settings.BrandColorHex
            });
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdatePdfBrandingSettingsRequest request)
        {
            if (request is null)
                return BadRequest("Request body is required.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            if (!IsValidHexOrEmpty(request.BrandColorHex))
                return BadRequest("BrandColorHex must be a valid hex color like #8FE3A8.");

            var existing = await _pdfBrandingSettingsRepository.GetByUserIdAsync(userId);

            var settings = new PdfBrandingSettings
            {
                Id = existing?.Id ?? Guid.NewGuid().ToString("N"),
                UserId = userId,

                LogoFileUrl = Normalize(request.LogoFileUrl),
                CompanyName = Normalize(request.CompanyName),
                Website = Normalize(request.Website),
                Email = Normalize(request.Email),
                Phone = Normalize(request.Phone),
                Address = Normalize(request.Address),
                BrandColorHex = NormalizeHexOrDefault(request.BrandColorHex, "#8FE3A8"),

                CreatedAtUtc = existing?.CreatedAtUtc ?? DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _pdfBrandingSettingsRepository.UpsertAsync(settings);

            return NoContent();
        }

        private static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim();
        }

        private static bool IsValidHexOrEmpty(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            var normalized = value.Trim();
            return System.Text.RegularExpressions.Regex.IsMatch(
                normalized,
                "^#[0-9A-Fa-f]{6}$"
            );
        }

        private static string NormalizeHexOrDefault(string? value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            var normalized = value.Trim();

            return System.Text.RegularExpressions.Regex.IsMatch(
                normalized,
                "^#[0-9A-Fa-f]{6}$"
            )
                ? normalized
                : fallback;
        }
    }
}