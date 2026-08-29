using DynamicFormBuilder.Models.Pdf;

public interface IPdfService
{
    Task<byte[]> GenerateSubmissionPdfAsync(GenerateSubmissionPdfRequest request);
}

