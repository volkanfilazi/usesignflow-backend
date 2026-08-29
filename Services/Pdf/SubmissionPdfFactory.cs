using DynamicFormBuilder.Models.Pdf;
using DynamicFormBuilder.Repositories.Branding;
using DynamicFormBuilder.Repositories.Submission;
using DynamicFormBuilder.Services.Billing;

namespace DynamicFormBuilder.Services.Pdf
{
    public class SubmissionPdfFactory : ISubmissionPdfFactory
    {
        private readonly IFormSubmissionRepository _formSubmissionRepository;
        private readonly BillingOverviewService _billingOverviewService;
        private readonly IPdfBrandingSettingsRepository _pdfBrandingSettingsRepository;
        private readonly IPdfBrandingResolver _pdfBrandingResolver;
        private readonly IPdfService _pdfService;

        public SubmissionPdfFactory(
            IFormSubmissionRepository formSubmissionRepository,
            BillingOverviewService billingOverviewService,
            IPdfBrandingSettingsRepository pdfBrandingSettingsRepository,
            IPdfBrandingResolver pdfBrandingResolver,
            IPdfService pdfService)
        {
            _formSubmissionRepository = formSubmissionRepository;
            _billingOverviewService = billingOverviewService;
            _pdfBrandingSettingsRepository = pdfBrandingSettingsRepository;
            _pdfBrandingResolver = pdfBrandingResolver;
            _pdfService = pdfService;
        }

        public async Task<byte[]> GenerateAsync(string submissionId)
        {
            var submission = await _formSubmissionRepository.GetByIdAsync(submissionId);
            if (submission is null)
                throw new InvalidOperationException($"Submission not found: {submissionId}");

            return await GenerateAsync(submission);
        }

        public async Task<byte[]> GenerateAsync(FormSubmission submission)
        {
            if (submission is null)
                throw new ArgumentNullException(nameof(submission));

            var ownerUserId = submission.CreatedByUserId;
            if (string.IsNullOrWhiteSpace(ownerUserId))
                throw new InvalidOperationException("Submission owner user id is missing.");

            var billingOverview = await _billingOverviewService.GetAsync(ownerUserId);
            var planCode = billingOverview?.PlanCode ?? "Free";

            var brandingSettings = await _pdfBrandingSettingsRepository.GetByUserIdAsync(ownerUserId);
            var resolvedBranding = _pdfBrandingResolver.Resolve(planCode, brandingSettings);

            return await _pdfService.GenerateSubmissionPdfAsync(new GenerateSubmissionPdfRequest
            {
                Submission = submission,
                Branding = resolvedBranding
            });
        }
    }
}
