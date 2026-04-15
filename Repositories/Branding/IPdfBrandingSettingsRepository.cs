using DynamicFormBuilder.Models.Pdf;

namespace DynamicFormBuilder.Repositories.Branding
{
    public interface IPdfBrandingSettingsRepository
    {
        Task<PdfBrandingSettings?> GetByUserIdAsync(string userId);
        Task UpsertAsync(PdfBrandingSettings settings);
    }
}
