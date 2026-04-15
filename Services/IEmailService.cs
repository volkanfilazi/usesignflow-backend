namespace DynamicFormBuilder.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string verifyUrl, string? fullName);
    Task SendOneTimeCodeEmailAsync(
            string toEmail,
            string subject,
            string preheader,
            string title,
            string bodyText,
            string verificationCode,
            string footerText,
            string? fullName);
    Task SendSubmissionCompletedEmailAsync(string toEmail, string verifyUrl, string? fullName);
    Task SendPasswordResetEmailAsync(string toEmail, string verifyUrl, string? fullName);
    Task SendCompletedSubmissionPdfEmailAsync(
    string userId,
    string email,
    string subject,
    string senderName,
    string formName,
    byte[] pdfBytes,
    string? submissionId = null);

    Task SendSubmissionReminderEmailAsync(
            string userId,
            string email,
            string accessUrl,
            string senderName,
            string formName,
            string? submissionId = null);
    Task SendSubmissionSignerEmailAsync(
        string userId,
        string email,
        string subject,
        string accessUrl,
        string senderName,
        string formName,
        string? submissionId = null);
}