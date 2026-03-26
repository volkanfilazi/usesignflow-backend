public class VerifyTwoFactorRequest
{
	public string TwoFactorToken { get; set; } = default!;
	public string Code { get; set; } = default!;
}