public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string verifyUrl, string? fullName);

    Task SendSubmissionSignerEmailAsync(
        string email,
        string accessUrl,
        string senderName,
        string formName);
}