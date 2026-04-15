public static class SubmissionHelper
{
    public static void UpdateSubmissionStatus(FormSubmission submission)
    {
        if (submission.Status == SubmissionStatus.Cancelled ||
            submission.Status == SubmissionStatus.Expired)
        {
            return;
        }

        if (!submission.OwnerConfirmed)
        {
            submission.Status = SubmissionStatus.Drafted;
            return;
        }

        var hasClientStep = submission.FieldsSnapshot.Any(f => f.AssignedTo == AssignedTo.Client);

        if (!hasClientStep)
        {
            submission.Status = SubmissionStatus.Completed;
            return;
        }

        submission.Status = submission.ExternalConfirmed
            ? SubmissionStatus.Completed
            : SubmissionStatus.Pending;
    }

    public static bool HasClientStep(FormSubmission submission)
    {
        return submission.FieldsSnapshot.Any(f => f.AssignedTo == AssignedTo.Client);
    }

    public static bool IsFieldCompleted(FieldDefinition field, FormSubmission submission)
    {
        if (field.Type == "Signature")
        {
            return submission.Signatures.Any(s =>
                s.FieldId == field.FieldId &&
                !string.IsNullOrWhiteSpace(s.SignatureUrl));
        }

        var answerValue = submission.Answers
            .FirstOrDefault(a => a.FieldId == field.FieldId)?.Value;

        if (field.Type == "Agreement" || field.Type == "Checkbox")
        {
            return string.Equals(answerValue, "true", StringComparison.OrdinalIgnoreCase);
        }

        return !string.IsNullOrWhiteSpace(answerValue);
    }

    public static IReadOnlyList<FieldDefinition> GetMissingRequiredFields(
        FormSubmission submission,
        AssignedTo assignedTo)
    {
        return submission.FieldsSnapshot
            .Where(f => f.AssignedTo == assignedTo && f.Required)
            .Where(f => !IsFieldCompleted(f, submission))
            .ToList();
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

    public static void ApplyReminderSettings(
    FormSubmission submission,
    bool reminderEnabled,
    int reminderIntervalDays,
    int maxReminderCount,
    DateTime nowUtc)
    {
        submission.ReminderEnabled = reminderEnabled;
        submission.ReminderIntervalDays = reminderEnabled ? reminderIntervalDays : null;
        submission.NextReminderAtUtc = reminderEnabled ? nowUtc.AddDays(reminderIntervalDays) : null;
        submission.ReminderCount = 0;
        submission.MaxReminderCount = reminderEnabled ? maxReminderCount : null;
    }

    public static void DisableReminder(FormSubmission submission)
    {
        submission.ReminderEnabled = false;
        submission.ReminderIntervalDays = null;
        submission.NextReminderAtUtc = null;
        submission.MaxReminderCount = null;
    }
}