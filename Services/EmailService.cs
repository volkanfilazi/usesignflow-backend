using DynamicFormBuilder.Models.Billing;
using DynamicFormBuilder.Models.Email;
using DynamicFormBuilder.Repositories.Billing;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Net;
using Microsoft.Extensions.Options;

namespace DynamicFormBuilder.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly EmailLogRepository _emailLogRepository;
        private readonly EmailSettings _emailSettings;

        public EmailService(
            IConfiguration configuration,
            EmailLogRepository emailLogRepository,
            IOptions<EmailSettings> emailOptions)
        {
            _configuration = configuration;
            _emailLogRepository = emailLogRepository;
            _emailSettings = emailOptions.Value;
        }

        private async Task SendActionEmailAsync(
    string toEmail,
    string subject,
    string preheader,
    string title,
    string bodyText,
    string actionText,
    string actionUrl,
    string footerText,
    string? fullName)
        {
            var safeName = WebUtility.HtmlEncode(fullName ?? "there");
            var safeUrl = WebUtility.HtmlEncode(actionUrl);
            var safeBodyText = WebUtility.HtmlEncode(bodyText);
            var safeActionText = WebUtility.HtmlEncode(actionText);

            var html = BuildBaseEmailLayout(
                preheader: preheader,
                title: title,
                bodyHtml: $@"
<p style=""margin:0; font-size:16px; line-height:1.8; color:#4b5563; text-align:center;"">
  Hi <strong style=""color:#111827;"">{safeName}</strong>, {safeBodyText}
</p>

<div style=""padding:28px 0 0 0; text-align:center;"">
  <a href=""{safeUrl}""
     style=""display:inline-block; padding:15px 28px; background:linear-gradient(135deg,#92e3a9,#6dd58c); color:#0f172a; text-decoration:none; font-size:16px; font-weight:700; border-radius:16px; box-shadow:0 14px 30px rgba(109,213,140,0.28);"">
    {safeActionText}
  </a>
</div>

<div style=""padding:28px 0 0 0;"">
  <div style=""background:#f8fffa; border:1px solid #dff5e6; border-radius:20px; padding:18px 20px;"">
    <p style=""margin:0; font-size:14px; line-height:1.7; color:#6b7280;"">
      If the button does not work, copy and paste the following link into your browser:
    </p>
    <p style=""margin:12px 0 0 0; word-break:break-all; font-size:13px; line-height:1.7; color:#1f6f3d;"">
      <a href=""{safeUrl}"" style=""color:#1f6f3d; text-decoration:none;"">{safeUrl}</a>
    </p>
  </div>
</div>",
                footerText: footerText
            );

            await SendHtmlEmailAsync(toEmail, subject, html);
        }

        public async Task SendOneTimeCodeEmailAsync(
            string toEmail,
            string subject,
            string preheader,
            string title,
            string bodyText,
            string verificationCode,
            string footerText,
            string? fullName)
        {
            var safeName = WebUtility.HtmlEncode(fullName ?? "there");
            var safeBodyText = WebUtility.HtmlEncode(bodyText);
            var safeVerificationCode = WebUtility.HtmlEncode(verificationCode);

            var html = BuildBaseEmailLayout(
                preheader: preheader,
                title: title,
                bodyHtml: $@"
<p style=""margin:0; font-size:16px; line-height:1.8; color:#4b5563; text-align:center;"">
  Hi <strong style=""color:#111827;"">{safeName}</strong>, {safeBodyText}
</p>

<div style=""padding:28px 0 0 0; text-align:center;"">
  <div style=""display:inline-block; padding:18px 28px; background:#f8fffa; border:1px solid #dff5e6; border-radius:20px; box-shadow:0 14px 30px rgba(109,213,140,0.14);"">
    <div style=""font-size:12px; font-weight:700; letter-spacing:1.4px; text-transform:uppercase; color:#6b7280; margin-bottom:10px;"">
      Verification Code
    </div>
    <div style=""font-size:32px; font-weight:800; letter-spacing:8px; color:#111827;"">
      {safeVerificationCode}
    </div>
  </div>
</div>

<div style=""padding:24px 0 0 0;"">
  <div style=""background:#f8fffa; border:1px solid #dff5e6; border-radius:20px; padding:18px 20px;"">
    <p style=""margin:0; font-size:14px; line-height:1.7; color:#6b7280; text-align:center;"">
      This code is valid for a limited time and can only be used once.
    </p>
  </div>
</div>",
                footerText: footerText
            );

            await SendHtmlEmailAsync(toEmail, subject, html);
        }

        public Task SendVerificationEmailAsync(string toEmail, string verifyUrl, string? fullName)
        {
            return SendActionEmailAsync(
                toEmail: toEmail,
                subject: "Verify your email",
                preheader: "Email verification required",
                title: "Verify your email address",
                bodyText: "thanks for creating your account. Please confirm your email address to activate your account and continue securely.",
                actionText: "Verify Email",
                actionUrl: verifyUrl,
                footerText: "If you did not create this account, you can safely ignore this email.",
                fullName: fullName
            );
        }

        public Task SendSubmissionCompletedEmailAsync(string toEmail, string submissionUrl, string? fullName)
        {
            return SendActionEmailAsync(
                toEmail: toEmail,
                subject: "Your submission is complete",
                preheader: "Your submission has been completed",
                title: "Your form is ready",
                bodyText: "your submission has been completed. You can review the details by clicking the button below.",
                actionText: "View Submission",
                actionUrl: submissionUrl,
                footerText: "If you were not expecting this email, you can safely ignore this email.",
                fullName: fullName
            );
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string resetUrl, string? fullName)
        {
            var safeName = WebUtility.HtmlEncode(fullName ?? "there");
            var safeUrl = WebUtility.HtmlEncode(resetUrl);

            var subject = "Reset your password";

            var html = BuildBaseEmailLayout(
                preheader: "Password reset request",
                title: "Reset your password",
                bodyHtml: $@"
<p style=""margin:0; font-size:16px; line-height:1.8; color:#4b5563; text-align:center;"">
  Hi <strong style=""color:#111827;"">{safeName}</strong>, we received a request to reset your password.
  Click the button below to create a new password for your account.
</p>

<div style=""padding:28px 0 0 0; text-align:center;"">
  <a href=""{safeUrl}""
     style=""display:inline-block; padding:15px 28px; background:linear-gradient(135deg,#f9c46b,#f4a261); color:#0f172a; text-decoration:none; font-size:16px; font-weight:700; border-radius:16px; box-shadow:0 14px 30px rgba(244,162,97,0.28);"">
    Reset Password
  </a>
</div>

<div style=""padding:28px 0 0 0;"">
  <div style=""background:#fffaf5; border:1px solid #fde7d7; border-radius:20px; padding:18px 20px;"">
    <p style=""margin:0 0 10px 0; font-size:15px; font-weight:700; color:#111827;"">
      This link is valid for 1 hour.
    </p>
    <p style=""margin:0; font-size:14px; line-height:1.7; color:#6b7280;"">
      If the button does not work, copy and paste the following link into your browser:
    </p>
    <p style=""margin:12px 0 0 0; word-break:break-all; font-size:13px; line-height:1.7; color:#b45309;"">
      <a href=""{safeUrl}"" style=""color:#b45309; text-decoration:none;"">{safeUrl}</a>
    </p>
  </div>
</div>",
                footerText: "If you did not request a password reset, you can safely ignore this email."
            );

            await SendHtmlEmailAsync(toEmail, subject, html);
        }

        public async Task SendSubmissionSignerEmailAsync(
            string userId,
            string email,
            string subject,
            string accessUrl,
            string senderName,
            string formName,
            string? submissionId = null)
        {
            var log = new EmailLog
            {
                UserId = userId,
                ToEmail = email,
                EmailType = "SubmissionInvite",
                RelatedEntityId = submissionId ?? string.Empty,
                Subject = subject,
                Status = EmailLogStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _emailLogRepository.CreateAsync(log);

            try
            {
                var safeEmail = WebUtility.HtmlEncode(email);
                var safeUrl = WebUtility.HtmlEncode(accessUrl);
                var safeSenderName = WebUtility.HtmlEncode(senderName);
                var safeFormName = WebUtility.HtmlEncode(formName);

                var html = BuildBaseEmailLayout(
                    preheader: "Signature request",
                    title: "You have received a form to review and sign",
                    bodyHtml: $@"
                        <p style=""margin:0; font-size:16px; line-height:1.7; color:#374151;"">
                            <strong>{safeSenderName}</strong> sent you a document via <strong>SignFlow</strong>.
                        </p>

                        <p style=""margin:16px 0 0 0; font-size:16px; line-height:1.7; color:#374151;"">
                            Document: <strong>{safeFormName}</strong>
                        </p>

                        <p style=""margin:16px 0 0 0; font-size:16px; line-height:1.7; color:#374151;"">
                            Please review and complete it using the secure link below.
                        </p>

                        <div style=""padding:24px 0 0 0; text-align:center;"">
                            <a href=""{safeUrl}"" style=""display:inline-block; padding:14px 24px; background:#6dd58c; color:#111827; text-decoration:none; font-size:16px; font-weight:700; border-radius:12px;"">
                                Review document
                            </a>
                        </div>

                        <p style=""margin:24px 0 0 0; font-size:14px; line-height:1.7; color:#6b7280;"">
                            This secure link is valid for a limited time.
                        </p>

                        <p style=""margin:12px 0 0 0; font-size:14px; line-height:1.7; color:#6b7280;"">
                            If the button does not work, copy and paste this link into your browser:
                        </p>

                        <p style=""margin:12px 0 0 0; word-break:break-all; font-size:13px; line-height:1.7; color:#1f6f3d;"">
                            <a href=""{safeUrl}"" style=""color:#1f6f3d; text-decoration:none;"">{safeUrl}</a>
                        </p>

                        <p style=""margin:24px 0 0 0; font-size:14px; line-height:1.7; color:#6b7280;"">
                            If you have questions, reply to this email.
                        </p>", 
                    footerText: $@"You received this email because <strong style=""color:#111827;"">{safeSenderName}</strong> used SignFlow to send you a document.
                        If you were not expecting this email, you can ignore it or contact us at <strong style=""color:#111827;"">support@usesignflow.com</strong>."
                );

                await SendHtmlEmailAsync(email, subject, html);

                log.Status = EmailLogStatus.Sent;
                log.SentAtUtc = DateTime.UtcNow;
                log.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                log.Status = EmailLogStatus.Failed;
                log.ErrorMessage = ex.Message;
                log.SentAtUtc = null;

                throw;
            }
            finally
            {
                await _emailLogRepository.UpdateAsync(log.Id, log);
            }
        }

        public async Task SendSubmissionReminderEmailAsync(
            string userId,
            string email,
            string accessUrl,
            string senderName,
            string formName,
            string? submissionId = null)
        {
            var log = new EmailLog
            {
                UserId = userId,
                ToEmail = email,
                EmailType = "SubmissionReminder",
                RelatedEntityId = submissionId ?? string.Empty,
                Subject = $"Reminder: {formName} is waiting for your review",
                Status = EmailLogStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _emailLogRepository.CreateAsync(log);

            try
            {
                var safeEmail = WebUtility.HtmlEncode(email);
                var safeUrl = WebUtility.HtmlEncode(accessUrl);
                var safeSenderName = WebUtility.HtmlEncode(senderName);
                var safeFormName = WebUtility.HtmlEncode(formName);

                var subject = $"Reminder: {formName} is waiting for your review";

                var html = BuildBaseEmailLayout(
                    preheader: "Reminder: document pending",
                    title: "Reminder: your document is still waiting",
                    bodyHtml: $@"
                <p style=""margin:0; font-size:16px; line-height:1.7; color:#374151;"">
                    <strong>{safeSenderName}</strong> previously sent you a document via <strong>SignFlow</strong>.
                </p>

                <p style=""margin:16px 0 0 0; font-size:16px; line-height:1.7; color:#374151;"">
                    Document: <strong>{safeFormName}</strong>
                </p>

                <p style=""margin:16px 0 0 0; font-size:16px; line-height:1.7; color:#374151;"">
                    This is a friendly reminder that your action is still required.
                </p>

                <div style=""padding:24px 0 0 0; text-align:center;"">
                    <a href=""{safeUrl}"" style=""display:inline-block; padding:14px 24px; background:#6dd58c; color:#111827; text-decoration:none; font-size:16px; font-weight:700; border-radius:12px;"">
                        Review document
                    </a>
                </div>

                <p style=""margin:24px 0 0 0; font-size:14px; line-height:1.7; color:#6b7280;"">
                    If the button does not work, copy and paste this link into your browser:
                </p>

                <p style=""margin:12px 0 0 0; word-break:break-all; font-size:13px; line-height:1.7; color:#1f6f3d;"">
                    <a href=""{safeUrl}"" style=""color:#1f6f3d; text-decoration:none;"">{safeUrl}</a>
                </p>

                <p style=""margin:24px 0 0 0; font-size:14px; line-height:1.7; color:#6b7280;"">
                    If you have already completed this document, you can ignore this reminder.
                </p>",
                    footerText: $@"This reminder was sent because <strong style=""color:#111827;"">{safeSenderName}</strong> used SignFlow to request your action.
                If you were not expecting this email, you can ignore it or contact us at <strong style=""color:#111827;"">support@usesignflow.com</strong>."
                );

                await SendHtmlEmailAsync(email, subject, html);

                log.Status = EmailLogStatus.Sent;
                log.SentAtUtc = DateTime.UtcNow;
                log.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                log.Status = EmailLogStatus.Failed;
                log.ErrorMessage = ex.Message;
                log.SentAtUtc = null;

                throw;
            }
            finally
            {
                await _emailLogRepository.UpdateAsync(log.Id, log);
            }
        }

        public async Task SendCompletedSubmissionPdfEmailAsync(
    string userId,
    string email,
    string subject,
    string senderName,
    string formName,
    byte[] pdfBytes,
    string? submissionId = null)
        {
            var log = new EmailLog
            {
                UserId = userId,
                ToEmail = email,
                EmailType = $"Completed PDF - {formName}",
                RelatedEntityId = submissionId ?? string.Empty,
                Subject = subject,
                Status = EmailLogStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _emailLogRepository.CreateAsync(log);

            try
            {
                var safeEmail = WebUtility.HtmlEncode(email);
                var safeSenderName = WebUtility.HtmlEncode(senderName);
                var safeFormName = WebUtility.HtmlEncode(formName);

                var html = BuildBaseEmailLayout(
                    preheader: "Completed document",
                    title: "Your completed document is attached",
                    bodyHtml: $@"
<p style=""margin:0; font-size:16px; line-height:1.8; color:#4b5563; text-align:center;"">
  <strong style=""color:#111827;"">{safeSenderName}</strong> has shared the completed document for
  <strong style=""color:#111827;"">{safeFormName}</strong>.
</p>

<div style=""padding:24px 0 0 0;"">
  <div style=""background:#f8fffa; border:1px solid #dff5e6; border-radius:20px; padding:18px 20px;"">
    <p style=""margin:0; font-size:14px; line-height:1.7; color:#6b7280;"">
      The signed PDF is attached to this email for your records.
    </p>
  </div>
</div>",
                    footerText: $@"This email was sent to <strong style=""color:#111827;"">{safeEmail}</strong>."
                );

                var attachments = new List<EmailAttachment>
        {
            new EmailAttachment
            {
                FileName = $"{SanitizeFileName(formName)}-{submissionId ?? "submission"}.pdf",
                Content = pdfBytes,
                ContentType = "application/pdf"
            }
        };

                await SendHtmlEmailAsync(email, subject, html, attachments);

                log.Status = EmailLogStatus.Sent;
                log.SentAtUtc = DateTime.UtcNow;
                log.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                log.Status = EmailLogStatus.Failed;
                log.ErrorMessage = ex.Message;
                log.SentAtUtc = null;
                throw;
            }
            finally
            {
                await _emailLogRepository.UpdateAsync(log.Id, log);
            }
        }

        private static string HtmlToPlainText(string subject, string html)
        {
            var text = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
            text = WebUtility.HtmlDecode(text);
            text = text.Replace("\r", "").Trim();
            return $"{subject}\n\n{text}";
        }

        public async Task SendHtmlEmailAsync(
    string to,
    string subject,
    string html,
    List<EmailAttachment>? attachments = null)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailSettings.FromName, _emailSettings.FromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.ReplyTo.Add(MailboxAddress.Parse(_emailSettings.ReplyToAddress ?? _emailSettings.FromAddress));
            message.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = html,
                TextBody = HtmlToPlainText(subject, html)
            };

            if (attachments != null)
            {
                foreach (var attachment in attachments)
                {
                    builder.Attachments.Add(
                        attachment.FileName,
                        attachment.Content,
                        ContentType.Parse(attachment.ContentType));
                }
            }

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_emailSettings.SmtpUser, _emailSettings.SmtpPass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        private string BuildBaseEmailLayout(
            string preheader,
            string title,
            string bodyHtml,
            string footerText)
        {
            var safePreheader = WebUtility.HtmlEncode(preheader);
            var safeTitle = WebUtility.HtmlEncode(title);
            var safeFooter = footerText;

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

        private static string SanitizeFileName(string value)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(value.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        }
    }
}