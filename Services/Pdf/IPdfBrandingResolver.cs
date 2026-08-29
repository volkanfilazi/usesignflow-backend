using DynamicFormBuilder.Models.Pdf;

namespace DynamicFormBuilder.Services.Pdf
{
    public interface IPdfBrandingResolver
    {
        ResolvedPdfBranding Resolve(
            string planCode,
            PdfBrandingSettings? settings
        );
    }
}
