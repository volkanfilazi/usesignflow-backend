namespace DynamicFormBuilder.Services.Submission
{
    public interface ISubmissionReminderService
    {
        Task ProcessDueRemindersAsync();
    }
}
