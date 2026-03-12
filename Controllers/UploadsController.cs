using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DynamicFormBuilder.Controllers;

[ApiController]
[Route("api/uploads")]
public class UploadsController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public UploadsController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [Authorize]
    [HttpPost("signature")]
    public async Task<ActionResult<object>> UploadSignature([FromForm] IFormFile file)
    {
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

        var uploadsRoot = Path.Combine("/var/www/uploads", "signatures");
        Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(uploadsRoot, fileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        return Ok(new
        {
            fileName,
            url = $"/uploads/signatures/{fileName}"
        });
    }
}