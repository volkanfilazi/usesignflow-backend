using DynamicFormBuilder.Repositories.Auth;
using DynamicFormBuilder.Repositories.Submission;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DynamicFormBuilder.Services.Submission
{
    public class SubmissionReminderService : ControllerBase, ISubmissionReminderService
    {
        private readonly IFormSubmissionRepository _formSubmissionRepository;
        private readonly IAuthRepository _authRepo;
        private readonly ISubmissionAccessTokenRepository _submissionAccessTokenRepository;
        private readonly ISubmissionSettingsRepository _submissionSettingsRepository;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public SubmissionReminderService(
            IFormSubmissionRepository formSubmissionRepository,
            IAuthRepository authRepository,
            ISubmissionAccessTokenRepository submissionAccessTokenRepository,
            ISubmissionSettingsRepository submissionSettingsRepository,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _formSubmissionRepository = formSubmissionRepository;
            _authRepo = authRepository;
            _submissionAccessTokenRepository = submissionAccessTokenRepository;
            _submissionSettingsRepository = submissionSettingsRepository;
            _emailService = emailService;
            _configuration = configuration;
        }

        public async Task ProcessDueRemindersAsync()
        {
            var nowUtc = DateTime.UtcNow;

            var dueSubmissions = await _formSubmissionRepository.GetReminderDueSubmissionsAsync(nowUtc);

            foreach (var submission in dueSubmissions)
            {
                if (string.IsNullOrWhiteSpace(submission.ExternalRecipientEmail))
                    continue;

                var ownerSettings = await _submissionSettingsRepository.GetByUserIdAsync(submission.CreatedByUserId);
                var tokenLifetimeDays = ownerSettings?.DefaultAccessTokenLifetimeDays ?? 3;

                await _submissionAccessTokenRepository.RevokeActiveTokensBySubmissionIdAsync(submission.Id!);

                var rawAccessToken = TokenHelper.GenerateSecureToken();
                var accessTokenHash = TokenHelper.ComputeSha256(rawAccessToken);

                var accessToken = new SubmissionAccessToken
                {
                    SubmissionId = submission.Id!,
                    Email = submission.ExternalRecipientEmail,
                    TokenHash = accessTokenHash,
                    CreatedAtUtc = nowUtc,
                    ExpiresAtUtc = nowUtc.AddDays(tokenLifetimeDays),
                    Purpose = Purpose.EditSubmission
                };

                await _submissionAccessTokenRepository.CreateAsync(accessToken);

                var frontendBaseUrl = _configuration["App:FrontendBaseUrl"]?.TrimEnd('/');
                if (string.IsNullOrWhiteSpace(frontendBaseUrl))
                    throw new InvalidOperationException("App:FrontendBaseUrl is missing.");

                var accessUrl = $"{frontendBaseUrl}/submission-access?token={Uri.EscapeDataString(rawAccessToken)}";

                var ownerUser = await _authRepo.GetByIdAsync(submission.CreatedByUserId);

                await _emailService.SendSubmissionReminderEmailAsync(
                    submission.CreatedByUserId,
                    submission.ExternalRecipientEmail!,
                    accessUrl,
                    ownerUser?.FullName ?? String.Empty,
                    submission.FormName,
                    submission.Id
                    );

                submission.ReminderCount += 1;
                submission.NextReminderAtUtc = submission.ReminderIntervalDays.HasValue
                    ? nowUtc.AddDays(submission.ReminderIntervalDays.Value)
                    : null;
                submission.UpdatedAtUtc = nowUtc;

                await _formSubmissionRepository.UpdateAsync(submission);
            }
        }
    }
}
