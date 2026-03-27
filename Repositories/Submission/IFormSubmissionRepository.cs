using DynamicFormBuilder.Models.Billing;

namespace DynamicFormBuilder.Repositories.Submission
{
    public interface IFormSubmissionRepository
    {
        Task<List<FormSubmission>> GetByUserIdAsync(string userId);
        Task CreateAsync(FormSubmission submission);
        Task<FormSubmission?> GetByIdAsync(string id);
        Task UpdateAsync(FormSubmission submission);
        Task<long> CountCreatedInPeriodAsync(string userId, DateTime periodStartUtc, DateTime periodEndUtc);
    }
}
