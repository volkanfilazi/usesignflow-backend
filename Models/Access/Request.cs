public class ResolveSubmissionAccessRequest
{
	public string Token { get; set; } = string.Empty;
}

public class ResolveSubmissionAccessResponse
{
	public string SubmissionId { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public bool IsAuthenticated { get; set; }
	public bool IsEmailMatch { get; set; }
}