namespace DynamicFormBuilder.Repositories.Auth
{
    public interface IOneTimeCodeRepository
    {
        Task CreateAsync(OneTimeCode code);
        Task InvalidateActiveCodesByTargetAsync(string target);
        Task<OneTimeCode?> GetLatestActiveBySubmissionIdAndTargetAsync(string submissionId, string target);
        Task UpdateAsync(OneTimeCode code);
    }
}
