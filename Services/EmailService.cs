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
            var safeName = WebUtility.HtmlEncode(fullName ?? "there");
            var safeUrl = WebUtility.HtmlEncode(verifyUrl);

            var html = BuildBaseEmailLayout(
                preheader: "Email verification required",
                title: "Verify your email address",
                bodyHtml: $@"
<p style=""margin:0; font-size:16px; line-height:1.8; color:#4b5563; text-align:center;"">
  Hi <strong style=""color:#111827;"">{safeName}</strong>, thanks for creating your account.
  Please confirm your email address to activate your account and continue securely.
</p>

<div style=""padding:28px 0 0 0; text-align:center;"">
  <a href=""{safeUrl}""
     style=""display:inline-block; padding:15px 28px; background:linear-gradient(135deg,#92e3a9,#6dd58c); color:#0f172a; text-decoration:none; font-size:16px; font-weight:700; border-radius:16px; box-shadow:0 14px 30px rgba(109,213,140,0.28);"">
    Verify Email
  </a>
</div>

<div style=""padding:28px 0 0 0;"">
  <div style=""background:#f8fffa; border:1px solid #dff5e6; border-radius:20px; padding:18px 20px;"">
    <p style=""margin:0 0 10px 0; font-size:15px; font-weight:700; color:#111827;"">
      This link is valid for 24 hours.
    </p>
    <p style=""margin:0; font-size:14px; line-height:1.7; color:#6b7280;"">
      If the button does not work, copy and paste the following link into your browser:
    </p>
    <p style=""margin:12px 0 0 0; word-break:break-all; font-size:13px; line-height:1.7; color:#1f6f3d;"">
      <a href=""{safeUrl}"" style=""color:#1f6f3d; text-decoration:none;"">{safeUrl}</a>
    </p>
  </div>
</div>",
                footerText: "If you did not create this account, you can safely ignore this email."
            );

            await SendHtmlEmailAsync(toEmail, "Verify your email", html);
        }

        public async Task SendSubmissionSignerEmailAsync(
            string email,
            string accessUrl,
            string senderName,
            string formName)
        {
            var safeEmail = WebUtility.HtmlEncode(email);
            var safeUrl = WebUtility.HtmlEncode(accessUrl);
            var safeSenderName = WebUtility.HtmlEncode(senderName);
            var safeFormName = WebUtility.HtmlEncode(formName);

            var html = BuildBaseEmailLayout(
                preheader: "Signature request",
                title: "You have received a form to review and sign",
                bodyHtml: $@"
<p style=""margin:0; font-size:16px; line-height:1.8; color:#4b5563; text-align:center;"">
  <strong style=""color:#111827;"">{safeSenderName}</strong> has sent you the form
  <strong style=""color:#111827;"">{safeFormName}</strong> to review and sign.
</p>

<div style=""padding:28px 0 0 0; text-align:center;"">
  <a href=""{safeUrl}""
     style=""display:inline-block; padding:15px 28px; background:linear-gradient(135deg,#92e3a9,#6dd58c); color:#0f172a; text-decoration:none; font-size:16px; font-weight:700; border-radius:16px; box-shadow:0 14px 30px rgba(109,213,140,0.28);"">
    Open Submission
  </a>
</div>

<div style=""padding:28px 0 0 0;"">
  <div style=""background:#f8fffa; border:1px solid #dff5e6; border-radius:20px; padding:18px 20px;"">
    <p style=""margin:0 0 10px 0; font-size:15px; font-weight:700; color:#111827;"">
      This access link is valid for a limited time.
    </p>
    <p style=""margin:0; font-size:14px; line-height:1.7; color:#6b7280;"">
      If the button does not work, copy and paste the following link into your browser:
    </p>
    <p style=""margin:12px 0 0 0; word-break:break-all; font-size:13px; line-height:1.7; color:#1f6f3d;"">
      <a href=""{safeUrl}"" style=""color:#1f6f3d; text-decoration:none;"">{safeUrl}</a>
    </p>
  </div>
</div>",
                footerText: $@"This email was sent to <strong style=""color:#111827;"">{safeEmail}</strong>.
If you were not expecting this request, you can safely ignore it."
            );

            await SendHtmlEmailAsync(email, $"Signature request · {formName}", html);
        }

        private async Task SendHtmlEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(
                _configuration["Email:FromName"],
                _configuration["Email:FromAddress"]));

            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            message.Body = new BodyBuilder
            {
                HtmlBody = htmlBody
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

        private string BuildBaseEmailLayout(
            string preheader,
            string title,
            string bodyHtml,
            string footerText)
        {
            var safePreheader = WebUtility.HtmlEncode(preheader);
            var safeTitle = WebUtility.HtmlEncode(title);
            var safeFooter = footerText; // body/footer içeriği bazı yerlerde kontrollü HTML içeriyor

            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>{safeTitle}</title>
</head>
<body style=""margin:0; padding:0; background-color:#f8fffa; font-family:Arial, Helvetica, sans-serif; color:#1f2937;"">
  <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""background-color:#f8fffa; margin:0; padding:24px 0;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""max-width:640px; background:#ffffff; border-radius:28px; overflow:hidden; border:1px solid #dff5e6; box-shadow:0 20px 50px rgba(17,24,39,0.08);"">

          <tr>
            <td style=""padding:32px 32px 20px 32px; background:linear-gradient(135deg, #f3fff7 0%, #ebfbf0 100%);"">
              <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"">
                <tr>
                  <td align=""center"" style=""vertical-align:middle;"">
                    <img
                        src=""https://usesignflow.com/signflow-logo.svg""
                        alt=""SignFlow logo""
                        width=""180""
                        style=""display:block; height:auto; margin:0 auto;""
                    />
                    <div style=""font-size:14px; color:#6b7280; margin-top:12px;"">
                      Contract & Signature Platform
                    </div>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <tr>
            <td style=""padding:16px 32px 0 32px; text-align:center;"">
              <div style=""display:inline-block; padding:8px 14px; border-radius:999px; background:#e8f9ee; color:#1f6f3d; font-size:13px; font-weight:700;"">
                {safePreheader}
              </div>
            </td>
          </tr>

          <tr>
            <td style=""padding:18px 32px 0 32px; text-align:center;"">
              <h1 style=""margin:0; font-size:32px; line-height:1.2; color:#111827;"">
                {safeTitle}
              </h1>
            </td>
          </tr>

          <tr>
            <td style=""padding:18px 32px 0 32px;"">
              {bodyHtml}
            </td>
          </tr>

          <tr>
            <td style=""padding:24px 32px 0 32px;"">
              <div style=""height:1px; background:#edf2f7;""></div>
            </td>
          </tr>

          <tr>
            <td style=""padding:22px 32px 32px 32px; text-align:center;"">
              <p style=""margin:0 0 8px 0; font-size:14px; color:#6b7280; line-height:1.7;"">
                {safeFooter}
              </p>
              <p style=""margin:0; font-size:13px; color:#9ca3af;"">
                © SignFlow · Secure digital contracts and signatures
              </p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
        }
    }
}