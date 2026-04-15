using DynamicFormBuilder.Services.Submission;

namespace DynamicFormBuilder.Services.Background
{
    public class SubmissionReminderBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public SubmissionReminderBackgroundService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _serviceProvider.CreateScope();
                var reminderService = scope.ServiceProvider.GetRequiredService<ISubmissionReminderService>();

                try
                {
                    await reminderService.ProcessDueRemindersAsync();
                }
                catch
                {
                }

                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }
}
