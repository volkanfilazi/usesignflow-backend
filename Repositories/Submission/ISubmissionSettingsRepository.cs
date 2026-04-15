using DynamicFormBuilder.Models.Submission;

namespace DynamicFormBuilder.Repositories.Submission
{
    public interface ISubmissionSettingsRepository
    {
        Task<SubmissionSettings?> GetByUserIdAsync(string userId);
        Task CreateAsync(SubmissionSettings settings);
        Task UpdateAsync(SubmissionSettings settings);
    }
}
