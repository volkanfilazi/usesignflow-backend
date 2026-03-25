public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime TokenExpiresAtUtc { get; set; }

    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAtUtc { get; set; }

    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    public bool RequiresTwoFactor { get; set; }
    public string? TwoFactorToken { get; set; }
}