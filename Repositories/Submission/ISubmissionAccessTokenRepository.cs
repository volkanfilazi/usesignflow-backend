namespace DynamicFormBuilder.Repositories.Submission
{
    public interface ISubmissionAccessTokenRepository
    {
        Task CreateAsync(SubmissionAccessToken token);
        Task<SubmissionAccessToken?> GetByTokenHashAsync(string hash);
        Task RevokeActiveTokensBySubmissionIdAsync(string submissionId);
        Task DeleteBySubmissionIdAsync(string submissionId);
    }
}
