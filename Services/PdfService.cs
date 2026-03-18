using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using ImageSharpSize = SixLabors.ImageSharp.Size;

public class PdfService : IPdfService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public PdfService(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    public Task<byte[]> GenerateSubmissionPdfAsync(FormSubmission submission)
    {
        try
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Element(header =>
                    {
                        header.Column(col =>
                        {
                            col.Item().Background(Colors.Grey.Lighten3).Padding(16).Column(inner =>
                            {
                                inner.Item().Text(submission.FormName ?? "Document")
                                    .FontSize(22)
                                    .Bold();

                                inner.Item().PaddingTop(4).Text($"Reference No: {submission.Id}")
                                    .FontSize(10)
                                    .FontColor(Colors.Grey.Darken2);

                                inner.Item().PaddingTop(2).Text($"Status: {submission.Status}")
                                    .FontSize(10)
                                    .FontColor(Colors.Grey.Darken2);
                            });
                        });
                    });

                    page.Content().PaddingVertical(15).Column(column =>
                    {
                        column.Spacing(18);

                        column.Item().Text("Form Details")
                            .FontSize(14)
                            .Bold()
                            .FontColor(Colors.Blue.Darken2);

                        foreach (var field in submission.FieldsSnapshot)
                        {
                            if (field.Type == "signaturePad")
                                continue;

                            if (field.Type == "agreement")
                            {
                                var agreementTitle = string.IsNullOrWhiteSpace(field.Agreement?.Title)
                                    ? field.Label
                                    : field.Agreement.Title;

                                var agreementContent = string.IsNullOrWhiteSpace(field.Agreement?.Content)
                                    ? "-"
                                    : field.Agreement.Content;

                                column.Item().Border(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(10)
                                    .Column(item =>
                                    {
                                        item.Spacing(6);

                                        item.Item().Text(agreementTitle)
                                            .FontSize(10)
                                            .FontColor(Colors.Grey.Darken1)
                                            .SemiBold();

                                        item.Item().Text(agreementContent)
                                            .FontSize(11);
                                    });

                                continue;
                            }

                            var answer = submission.Answers
                                .FirstOrDefault(a => a.FieldId == field.FieldId)?.Value;

                            column.Item().Border(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(10)
                                .Column(item =>
                                {
                                    item.Spacing(3);

                                    item.Item().Text(field.Label)
                                        .FontSize(9)
                                        .FontColor(Colors.Grey.Darken1)
                                        .SemiBold();

                                    item.Item().Text(string.IsNullOrWhiteSpace(answer) ? "-" : answer)
                                        .FontSize(11);
                                });
                        }

                        var signatureFields = submission.FieldsSnapshot
                            .Where(f => f.Type == "signaturePad")
                            .ToList();

                        if (signatureFields.Any())
                        {
                            column.Item().PaddingTop(8).Text("Signatures")
                                .FontSize(14)
                                .Bold()
                                .FontColor(Colors.Blue.Darken2);

                            foreach (var field in signatureFields)
                            {
                                var signature = submission.Signatures?
                                    .FirstOrDefault(s => s.FieldId == field.FieldId);

                                column.Item().Border(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(10)
                                    .Column(item =>
                                    {
                                        item.Spacing(6);

                                        item.Item().Text(field.Label)
                                            .FontSize(9)
                                            .FontColor(Colors.Grey.Darken1)
                                            .SemiBold();

                                        if (signature != null && !string.IsNullOrWhiteSpace(signature.SignatureUrl))
                                        {
                                            var imageBytes = GetSignatureBytes(signature.SignatureUrl);
                                            var normalizedImageBytes = NormalizeImageForPdf(imageBytes);

                                            if (normalizedImageBytes != null)
                                            {
                                                try
                                                {
                                                    item.Item()
                                                        .Background(Colors.White)
                                                        .Border(1)
                                                        .BorderColor(Colors.Grey.Lighten1)
                                                        .Padding(10)
                                                        .MinHeight(70)
                                                        .MaxHeight(120)
                                                        .AlignMiddle()
                                                        .AlignCenter()
                                                        .Image(normalizedImageBytes)
                                                        .FitArea();
                                                }
                                                catch
                                                {
                                                    item.Item().Text("Signature image could not be rendered")
                                                        .FontColor(Colors.Red.Darken1);
                                                }
                                            }
                                            else
                                            {
                                                item.Item().Text("Signature not found")
                                                    .FontColor(Colors.Red.Darken1);
                                            }

                                            item.Item().Text(
                                                    $"Signed by: {signature.SignedByEmail ?? "-"} | " +
                                                    $"Signed at: {signature.SignedAtUtc:yyyy-MM-dd HH:mm}")
                                                .FontSize(9)
                                                .FontColor(Colors.Grey.Darken1);
                                        }
                                        else
                                        {
                                            item.Item().Text("-")
                                                .FontColor(Colors.Grey.Darken1);
                                        }
                                    });
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Generated at ")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);

                        text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"))
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });
            });

            var pdfBytes = document.GeneratePdf();
            return Task.FromResult(pdfBytes);
        }
        catch (Exception ex)
        {
            throw new Exception($"PDF generation failed for submission {submission.Id}: {ex}");
        }
    }

    private byte[]? NormalizeImageForPdf(byte[]? imageBytes, int maxWidth = 800, int maxHeight = 250)
    {
        try
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return null;

            using var image = ImageSharpImage.Load(imageBytes);

            if (image.Width <= maxWidth && image.Height <= maxHeight)
                return imageBytes;

            image.Mutate(x =>
            {
                x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new ImageSharpSize(maxWidth, maxHeight)
                });
            });

            using var ms = new MemoryStream();
            image.Save(ms, new PngEncoder());
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private byte[]? GetSignatureBytes(string signatureUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(signatureUrl))
                return null;

            if (signatureUrl.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                var commaIndex = signatureUrl.IndexOf(',');
                if (commaIndex < 0)
                    return null;

                var base64 = signatureUrl[(commaIndex + 1)..];
                return Convert.FromBase64String(base64);
            }

            if (signatureUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            {
                var uploadsRoot = _configuration["UploadSettings:PhysicalRoot"];
                if (string.IsNullOrWhiteSpace(uploadsRoot))
                    return null;

                var relativePart = signatureUrl["/uploads/".Length..]
                    .Replace('/', Path.DirectorySeparatorChar);

                var fullPath = Path.Combine(uploadsRoot, relativePart);

                if (System.IO.File.Exists(fullPath))
                    return System.IO.File.ReadAllBytes(fullPath);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}