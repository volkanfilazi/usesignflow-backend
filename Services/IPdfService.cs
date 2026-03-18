public interface IPdfService
{
    Task<byte[]> GenerateSubmissionPdfAsync(FormSubmission submission);
}