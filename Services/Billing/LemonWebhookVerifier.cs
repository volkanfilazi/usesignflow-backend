using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace DynamicFormBuilder.Services.Billing;

public interface ILemonWebhookVerifier
{
    bool IsValid(string requestBody, string? signatureHeader);
}

public class LemonWebhookVerifier : ILemonWebhookVerifier
{
    private readonly LemonOptions _options;

    public LemonWebhookVerifier(IOptions<LemonOptions> options)
    {
        _options = options.Value;
    }

    public bool IsValid(string requestBody, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        var secretBytes = Encoding.UTF8.GetBytes(_options.WebhookSecret);
        var bodyBytes = Encoding.UTF8.GetBytes(requestBody);

        using var hmac = new HMACSHA256(secretBytes);
        var hash = hmac.ComputeHash(bodyBytes);
        var computed = Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(signatureHeader.ToLowerInvariant()));
    }
}