namespace DynamicFormBuilder.Repositories.Submission
{
    public interface ISubmissionAccessTokenRepository
    {
        Task CreateAsync(SubmissionAccessToken token);
        Task<SubmissionAccessToken?> GetByTokenHashAsync(string hash);
        Task DeleteBySubmissionIdAsync(string submissionId);
    }
}
