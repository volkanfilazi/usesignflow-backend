using DynamicFormBuilder.Models.Pdf;

namespace DynamicFormBuilder.Services
{
    public interface IPdfBrandingResolver
    {
        ResolvedPdfBranding Resolve(
            string planCode,
            PdfBrandingSettings? settings
        );
    }
}
