using DynamicFormBuilder.Models.Pdf;

namespace DynamicFormBuilder.Services.Pdf
{
    public class PdfBrandingResolver : IPdfBrandingResolver
    {
        public ResolvedPdfBranding Resolve(string planCode, PdfBrandingSettings? settings)
        {
            var normalizedPlan = planCode?.Trim() ?? "Free";
            var accent = NormalizeHex(settings?.BrandColorHex, "#8FE3A8");

            if (normalizedPlan.Equals("Business", StringComparison.OrdinalIgnoreCase))
            {
                return new ResolvedPdfBranding
                {
                    ShowWatermark = false,
                    ShowSignFlowLogo = false,
                    ShowCustomLogo = !string.IsNullOrWhiteSpace(settings?.LogoFileUrl),
                    ShowCompanyDetails = true,
                    CustomLogoFileUrl = settings?.LogoFileUrl,

                    HeaderTitleColorHex = "#0F172A",
                    HeaderBackgroundColorHex = accent,
                    SectionAccentColorHex = accent,
                    BorderColorHex = accent,

                    CompanyName = settings?.CompanyName,
                    Website = settings?.Website,
                    Email = settings?.Email,
                    Phone = settings?.Phone,
                    Address = settings?.Address
                };
            }

            if (normalizedPlan.Equals("Pro", StringComparison.OrdinalIgnoreCase))
            {
                return new ResolvedPdfBranding
                {
                    ShowWatermark = false,
                    ShowSignFlowLogo = string.IsNullOrWhiteSpace(settings?.LogoFileUrl),
                    ShowCustomLogo = !string.IsNullOrWhiteSpace(settings?.LogoFileUrl),
                    ShowCompanyDetails = false,
                    CustomLogoFileUrl = settings?.LogoFileUrl,

                    HeaderTitleColorHex = "#0F172A",
                    HeaderBackgroundColorHex = "#F8FAFC",
                    SectionAccentColorHex = "#0F172A",
                    BorderColorHex = "#E2E8F0",

                    CompanyName = settings?.CompanyName
                };
            }

            return new ResolvedPdfBranding
            {
                ShowWatermark = true,
                ShowSignFlowLogo = true,
                ShowCustomLogo = false,
                ShowCompanyDetails = false,

                HeaderTitleColorHex = "#0F172A",
                HeaderBackgroundColorHex = "#F1F5F9",
                SectionAccentColorHex = "#2563EB",
                BorderColorHex = "#E2E8F0",

                CompanyName = "SignFlow"
            };
        }

        private static string NormalizeHex(string? input, string fallback)
        {
            if (string.IsNullOrWhiteSpace(input))
                return fallback;

            var value = input.Trim();

            if (!value.StartsWith("#"))
                value = "#" + value;

            return System.Text.RegularExpressions.Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$")
                ? value
                : fallback;
        }

        private static string DarkenHex(string hex, int percent)
        {
            var safeHex = NormalizeHex(hex, "#8FE3A8").Replace("#", "");
            var num = Convert.ToInt32(safeHex, 16);
            var amt = (int)Math.Round(2.55 * percent);

            var r = Math.Max((num >> 16) - amt, 0);
            var g = Math.Max(((num >> 8) & 0x00ff) - amt, 0);
            var b = Math.Max((num & 0x0000ff) - amt, 0);

            return $"#{(0x1000000 + (r << 16) + (g << 8) + b).ToString("X").Substring(1)}";
        }
    }
}
