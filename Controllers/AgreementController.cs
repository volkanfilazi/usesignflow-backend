using System.Security.Claims;
using DynamicFormBuilder.Models;
using DynamicFormBuilder.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DynamicFormBuilder.Controllers
{
    [ApiController]
    [Route("api/agreements")]
    public class AgreementTemplateController : ControllerBase
    {
        private readonly AgreementTemplateRepository _agreementRepo;

        public AgreementTemplateController(AgreementTemplateRepository agreementTemplateRepository)
        {
            _agreementRepo = agreementTemplateRepository;
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<AgreementTemplate>> Create([FromBody] AgreementCreateRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var now = DateTime.UtcNow;

            var agreement = new AgreementTemplate
            {
                OwnerUserId = userId,
                Name = request.Name,
                Title = request.Title,
                Content = request.Content,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            await _agreementRepo.CreateAsync(agreement);
            return Ok(agreement);
        }

        [Authorize]
        [HttpGet]
        public async Task<ActionResult<List<AgreementTemplate>>> GetMine()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var agreements = await _agreementRepo.GetByOwnerUserIdAsync(userId);
            return Ok(agreements);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var existing = await _agreementRepo.GetByIdAsync(id);
            if (existing is null) return NotFound();

            await _agreementRepo.DeleteAsync(id);
            return NoContent();
        }
    }
}