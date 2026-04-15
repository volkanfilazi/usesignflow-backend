namespace DynamicFormBuilder.Models.Pdf
{
    public class PdfBrandingSettings
    {
        public string Id { get; set; } = default!;
        public string UserId { get; set; } = default!;

        public string? LogoFileUrl { get; set; }

        public string? CompanyName { get; set; }
        public string? Website { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }

        public string? BrandColorHex { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public class PdfBrandingSettingsResponse
    {
        public string? LogoFileUrl { get; set; }

        public string? CompanyName { get; set; }
        public string? Website { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }

        public string? BrandColorHex { get; set; }
    }

    public class UpdatePdfBrandingSettingsRequest
    {
        public string? LogoFileUrl { get; set; }

        public string? CompanyName { get; set; }
        public string? Website { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }

        public string? BrandColorHex { get; set; }
    }

    public class ResolvedPdfBranding
    {
        public bool ShowWatermark { get; set; }
        public bool ShowSignFlowLogo { get; set; }
        public bool ShowCustomLogo { get; set; }
        public bool ShowCompanyDetails { get; set; }

        public string? CustomLogoFileUrl { get; set; }

        public string HeaderTitleColorHex { get; set; } = "#0F172A";
        public string SectionAccentColorHex { get; set; } = "#1D4ED8";
        public string HeaderBackgroundColorHex { get; set; } = "#F1F5F9";
        public string BorderColorHex { get; set; } = "#E2E8F0";

        public string? CompanyName { get; set; }
        public string? Website { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
    }

    public class GenerateSubmissionPdfRequest
    {
        public FormSubmission Submission { get; set; } = default!;
        public ResolvedPdfBranding Branding { get; set; } = default!;
    }
}
