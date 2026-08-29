namespace DynamicFormBuilder.Services.Pdf
{
    public interface ISubmissionPdfFactory
    {
        Task<byte[]> GenerateAsync(FormSubmission submission);
        Task<byte[]> GenerateAsync(string submissionId);
    }
}
