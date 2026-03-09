using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Net;

namespace DynamicFormBuilder.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendVerificationEmailAsync(string toEmail, string verifyUrl, string? fullName)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(
                _configuration["Email:FromName"],
                _configuration["Email:FromAddress"]));

            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = "Verify your email";

            var safeName = WebUtility.HtmlEncode(fullName ?? "");
            var safeUrl = WebUtility.HtmlEncode(verifyUrl);

            message.Body = new BodyBuilder
            {
                HtmlBody = $@"
                    <h2>Merhaba {safeName}</h2>
                    <p>Hesabını doğrulamak için aşağıdaki linke tıkla:</p>
                    <p><a href=""{safeUrl}"">Email adresimi doğrula</a></p>
                    <p>Bu link 24 saat geçerlidir.</p>"
            }.ToMessageBody();

            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(
                _configuration["Email:SmtpHost"],
                int.Parse(_configuration["Email:SmtpPort"]!),
                SecureSocketOptions.StartTls);

            await smtp.AuthenticateAsync(
                _configuration["Email:SmtpUser"],
                _configuration["Email:SmtpPass"]);

            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }
    }
}