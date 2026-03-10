using Microsoft.AspNetCore.Mvc;

namespace DynamicFormBuilder.Controllers
{
    [ApiController]
    [Route("api/legal")]
    public class LegalController : ControllerBase
    {
        [HttpGet("terms/current")]
        public IActionResult GetCurrentTerms()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "legal", "terms_v1.html");

            if (!System.IO.File.Exists(filePath))
                return NotFound("Terms document not found.");

            var html = System.IO.File.ReadAllText(filePath);

            return Content(html, "text/html");
        }

        [HttpGet("privacy/current")]
        public IActionResult GetCurrentPrivacy()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "legal", "privacy_v1.html");

            if (!System.IO.File.Exists(filePath))
                return NotFound("Terms document not found.");

            var html = System.IO.File.ReadAllText(filePath);

            return Content(html, "text/html");
        }

        [HttpGet("terms/{version}")]
        public IActionResult GetTermsByVersion(string version)
        {
            var safeVersion = version.Trim().ToLowerInvariant();
            var fileName = $"terms_{safeVersion}.html";
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "legal", fileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound("Requested terms version not found.");

            var html = System.IO.File.ReadAllText(filePath);

            return Content(html, "text/html");
        }
    }
}