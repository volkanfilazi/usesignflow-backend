public class ExternalLogin
{
    public string Provider { get; set; } = string.Empty;
    public string ProviderUserId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime LinkedAtUtc { get; set; } = DateTime.UtcNow;
}

public class GoogleAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string FrontendGoogleCallbackUrl { get; set; } = string.Empty;
}

public class GoogleLoginRequest
{
    public string Credential { get; set; } = string.Empty;
}