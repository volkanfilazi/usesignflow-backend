using DynamicFormBuilder.Services.Billing;
using Microsoft.AspNetCore.Mvc;

namespace DynamicFormBuilder.Controllers;

[ApiController]
[Route("api/webhooks/lemonsqueezy")]
public class LemonWebhookController : ControllerBase
{
    private readonly ILemonWebhookVerifier _verifier;
    private readonly ILemonWebhookProcessor _processor;

    public LemonWebhookController(
        ILemonWebhookVerifier verifier,
        ILemonWebhookProcessor processor)
    {
        _verifier = verifier;
        _processor = processor;
    }

    [HttpPost]
    public async Task<IActionResult> Handle()
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync();

        var signature = Request.Headers["X-Signature"].FirstOrDefault();

        if (!_verifier.IsValid(rawBody, signature))
            return Unauthorized();

        await _processor.ProcessAsync(rawBody);

        return Ok();
    }
}