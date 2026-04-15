using DynamicFormBuilder.Models.Submission;
using DynamicFormBuilder.Repositories.Submission;
using Microsoft.AspNetCore.Authorization;
using DynamicFormBuilder.Models;
using DynamicFormBuilder.Models.Common;
using DynamicFormBuilder.Models.Form;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DynamicFormBuilder.Controllers
{
    [ApiController]
    [Route("api/submissions/settings")]
    public class SubmissionSettingsController : ControllerBase
    {
        private readonly ISubmissionSettingsRepository _submissionSettingsRepository;

        public SubmissionSettingsController(ISubmissionSettingsRepository submissionSettingsRepository)
        {
            _submissionSettingsRepository = submissionSettingsRepository;
        }

        [Authorize]
        [HttpPut("setting")]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateSubmissionSettingsRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var settings = await _submissionSettingsRepository.GetByUserIdAsync(userId);

            if (request.ReminderEnabledByDefault &&
                    request.DefaultReminderIntervalDays >= request.DefaultAccessTokenLifetimeDays)
            {
                return BadRequest(new
                {
                    code = "INVALID_REMINDER_INTERVAL",
                    message = "Reminder interval must be less than expiration days."
                });
            }

            if (settings is null)
            {
                settings = new SubmissionSettings
                {
                    UserId = userId,
                    DefaultAccessTokenLifetimeDays = request.DefaultAccessTokenLifetimeDays,
                    ReminderEnabledByDefault = request.ReminderEnabledByDefault,
                    DefaultReminderIntervalDays = request.DefaultReminderIntervalDays,
                    MaxReminderCount = request.MaxReminderCount,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                await _submissionSettingsRepository.CreateAsync(settings);
            }
            else
            {
                settings.DefaultAccessTokenLifetimeDays = request.DefaultAccessTokenLifetimeDays;
                settings.ReminderEnabledByDefault = request.ReminderEnabledByDefault;
                settings.DefaultReminderIntervalDays = request.DefaultReminderIntervalDays;
                settings.MaxReminderCount = request.MaxReminderCount;
                settings.UpdatedAtUtc = DateTime.UtcNow;

                await _submissionSettingsRepository.UpdateAsync(settings);
            }

            return NoContent();
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var settings = await _submissionSettingsRepository.GetByUserIdAsync(userId);

            if (settings is null)
            {
                return Ok(new SubmissionSettingsResponse
                {
                    DefaultAccessTokenLifetimeDays = 3,
                    ReminderEnabledByDefault = true,
                    DefaultReminderIntervalDays = 3,
                    MaxReminderCount = 3,
                    IsDefault = true
                });
            }

            return Ok(new SubmissionSettingsResponse
            {
                DefaultAccessTokenLifetimeDays = settings.DefaultAccessTokenLifetimeDays,
                ReminderEnabledByDefault = settings.ReminderEnabledByDefault,
                DefaultReminderIntervalDays = settings.DefaultReminderIntervalDays,
                MaxReminderCount = settings.MaxReminderCount,
                IsDefault = false
            });
        }
    }
}
