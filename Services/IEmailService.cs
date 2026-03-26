namespace DynamicFormBuilder.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string verifyUrl, string? fullName);
    Task SendSubmissionCompletedEmailAsync(string toEmail, string verifyUrl, string? fullName);
    Task SendPasswordResetEmailAsync(string toEmail, string verifyUrl, string? fullName);

    Task SendSubmissionSignerEmailAsync(
        string userId,
        string email,
        string subject,
        string accessUrl,
        string senderName,
        string formName,
        string? submissionId = null);
}