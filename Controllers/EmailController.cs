
using DynamicFormBuilder.Models.Billing;
using DynamicFormBuilder.Services.Billing;
using DynamicFormBuilder.Repositories.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using DynamicFormBuilder.Models;

namespace DynamicFormBuilder.Controllers;

[ApiController]
[Route("api/emails")]
public class FormSubmissionsController : ControllerBase
{
    private readonly EmailLogRepository _emailRepo;

    public FormSubmissionsController(
        EmailLogRepository emailRepo)
    {
        _emailRepo = emailRepo;
    }

    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<List<EmailLog>>> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var emails = await _emailRepo.GetByUserIdAsync(userId);

        return Ok(emails);
    }

}