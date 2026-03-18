public static class SubmissionHelper
{
    public static void UpdateSubmissionStatus(FormSubmission submission)
    {
        var signatureFieldIds = submission.FieldsSnapshot
            .Where(f => f.Type == "signaturePad")
            .Select(f => f.FieldId)
            .ToHashSet();

        var totalSignatureFields = signatureFieldIds.Count;

        if (submission.Status == SubmissionStatus.Cancelled ||
            submission.Status == SubmissionStatus.Expired)
        {
            return;
        }

        if (totalSignatureFields == 0)
        {
            submission.Status = SubmissionStatus.Draft;
            return;
        }

        var signedCount = submission.Signatures
            .Where(s =>
                signatureFieldIds.Contains(s.FieldId) &&
                !string.IsNullOrWhiteSpace(s.SignatureUrl))
            .Select(s => s.FieldId)
            .Distinct()
            .Count();

        if (signedCount == 0)
        {
            submission.Status = SubmissionStatus.PendingSignature;
        }
        else if (signedCount < totalSignatureFields)
        {
            submission.Status = SubmissionStatus.PartiallySigned;
        }
        else
        {
            submission.Status = SubmissionStatus.Completed;
        }
    }

    public static string? SaveSignatureIfNeeded(string? value, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (!value.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            return value;

        var commaIndex = value.IndexOf(',');
        if (commaIndex < 0)
            throw new InvalidOperationException("Invalid data URL for signature.");

        var base64 = value[(commaIndex + 1)..];
        var bytes = Convert.FromBase64String(base64);

        var uploadsRoot = configuration["UploadSettings:PhysicalRoot"];
        if (string.IsNullOrWhiteSpace(uploadsRoot))
            throw new InvalidOperationException("UploadSettings:PhysicalRoot is missing.");

        var signaturesRoot = Path.Combine(uploadsRoot, "signatures");
        Directory.CreateDirectory(signaturesRoot);

        var fileName = $"{Guid.NewGuid()}.png";
        var fullPath = Path.Combine(signaturesRoot, fileName);

        System.IO.File.WriteAllBytes(fullPath, bytes);

        return $"/uploads/signatures/{fileName}";
    }
}