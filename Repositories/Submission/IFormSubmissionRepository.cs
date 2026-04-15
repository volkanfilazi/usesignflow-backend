using DynamicFormBuilder.Models.Billing;
using DynamicFormBuilder.Models.Common.Query;
using DynamicFormBuilder.Models.Submission;

namespace DynamicFormBuilder.Repositories.Submission
{
    public interface IFormSubmissionRepository
    {
        Task<List<FormSubmission>> GetByUserIdAsync(string userId);
        Task CreateAsync(FormSubmission submission);
        Task<FormSubmission?> GetByIdAsync(string id);
        Task UpdateAsync(FormSubmission submission);
        Task<PagedResult<FormSubmission>> GetMineAsync(string userId, string? search, int page, int pageSize, string? sortField, string? sortDir);
        Task<SubmissionSummaryResponse> GetMineSummaryAsync(string userId, DateTime? start, DateTime? end);
        Task<SubmissionTrendResponse> GetMineTrendAsync(string userId, DateTime? start, DateTime? end);
        Task<long> CountCreatedInPeriodAsync(string userId, DateTime periodStartUtc, DateTime periodEndUtc);
        Task<List<FormSubmission>> GetReminderDueSubmissionsAsync(DateTime nowUtc);
    }
}
