public class DisableTwoFactorRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string? Code { get; set; }
}