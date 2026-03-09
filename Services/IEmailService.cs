namespace DynamicFormBuilder.Services
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string toEmail, string verifyUrl, string? fullName);
    }
}
