using DynamicFormBuilder.Helper;
using DynamicFormBuilder.Models.Pdf;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using System;
using System.Linq;
using DynamicFormBuilder.Repositories.Submission;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using ImageSharpSize = SixLabors.ImageSharp.Size;

public class PdfService : IPdfService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ISubmissionAccessTokenRepository _submissionAccessTokenRepository;

    public PdfService(IWebHostEnvironment environment, IConfiguration configuration, ISubmissionAccessTokenRepository submissionAccessTokenRepository)
    {
        _environment = environment;
        _configuration = configuration;
        _submissionAccessTokenRepository = submissionAccessTokenRepository;
    }

    public async Task<byte[]> GenerateSubmissionPdfAsync(GenerateSubmissionPdfRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (request.Submission is null)
            throw new ArgumentException("Submission is required.", nameof(request));

        if (request.Branding is null)
            throw new ArgumentException("Branding is required.", nameof(request));

        var submission = request.Submission;
        var branding = request.Branding;

        var frontendBaseUrl = _configuration["App:FrontendBaseUrl"]?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            throw new InvalidOperationException("App:FrontendBaseUrl is missing.");

        var rawVerifyToken = TokenHelper.GenerateSecureToken();
        var verifyTokenHash = TokenHelper.ComputeSha256(rawVerifyToken);

        await _submissionAccessTokenRepository.CreateAsync(new SubmissionAccessToken
        {
            SubmissionId = submission.Id!,
            Email = string.Empty,
            TokenHash = verifyTokenHash,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddYears(1),
            Purpose = Purpose.ReadSubmission
        });

        var verifyUrl = $"{frontendBaseUrl}/verification-pdf-access?verifyToken={Uri.EscapeDataString(rawVerifyToken)}";

        var qrBytes = QrCodeHelper.GenerateQrCode(verifyUrl);

        try
        {
            var signFlowLogoPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "Assets",
                "signflow-logo.png"
            );

            var watermarkPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "Assets",
                "signflow-watermark.png"
            );

            var customLogoBytes = GetUploadImageBytes(branding.CustomLogoFileUrl);
            var hasCustomLogo = branding.ShowCustomLogo && customLogoBytes is not null && customLogoBytes.Length > 0;
            var hasSignFlowLogo = branding.ShowSignFlowLogo && File.Exists(signFlowLogoPath);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Element(header =>
                    {
                        header
                            .Background(branding.HeaderBackgroundColorHex)
                            .Padding(16)
                            .Row(row =>
                            {
                                row.RelativeItem().Column(inner =>
                                {
                                    inner.Item()
                                        .Text(submission.FormName ?? "Document")
                                        .FontSize(22)
                                        .Bold()
                                        .FontColor(branding.HeaderTitleColorHex);

                                    inner.Item()
                                        .PaddingTop(3)
                                        .Text("Completion Certificate")
                                        .FontSize(11)
                                        .SemiBold()
                                        .FontColor(Colors.Grey.Darken1);

                                    inner.Item()
                                        .PaddingTop(4)
                                        .Text($"Reference No: {submission.Id}")
                                        .FontSize(10)
                                        .FontColor(Colors.Grey.Darken2);

                                    inner.Item()
                                        .PaddingTop(2)
                                        .Text($"Status: {submission.Status}")
                                        .FontSize(10)
                                        .FontColor(Colors.Grey.Darken2);
                                });

                                row.ConstantItem(180).Element(right =>
                                {
                                    right.AlignRight().Column(col =>
                                    {
                                        col.Spacing(4);

                                        if (hasCustomLogo)
                                        {
                                            col.Item()
                                                .AlignRight()
                                                .Height(40)
                                                .Image(customLogoBytes!)
                                                .FitArea();
                                        }
                                        else if (hasSignFlowLogo)
                                        {
                                            col.Item()
                                                .AlignRight()
                                                .Height(40)
                                                .Image(signFlowLogoPath)
                                                .FitArea();
                                        }

                                        if (branding.ShowCompanyDetails)
                                        {
                                            if (!string.IsNullOrWhiteSpace(branding.CompanyName))
                                            {
                                                col.Item()
                                                    .AlignRight()
                                                    .Text(branding.CompanyName)
                                                    .FontSize(11)
                                                    .SemiBold()
                                                    .FontColor(branding.HeaderTitleColorHex);
                                            }

                                            if (!string.IsNullOrWhiteSpace(branding.Website))
                                            {
                                                col.Item()
                                                    .AlignRight()
                                                    .Text(branding.Website)
                                                    .FontSize(9)
                                                    .FontColor(Colors.Grey.Darken2);
                                            }

                                            if (!string.IsNullOrWhiteSpace(branding.Email))
                                            {
                                                col.Item()
                                                    .AlignRight()
                                                    .Text(branding.Email)
                                                    .FontSize(9)
                                                    .FontColor(Colors.Grey.Darken2);
                                            }

                                            if (!string.IsNullOrWhiteSpace(branding.Phone))
                                            {
                                                col.Item()
                                                    .AlignRight()
                                                    .Text(branding.Phone)
                                                    .FontSize(9)
                                                    .FontColor(Colors.Grey.Darken2);
                                            }
                                        }
                                    });
                                });
                            });
                    });

                    page.Content().Layers(layers =>
                    {
                        if (branding.ShowWatermark && File.Exists(watermarkPath))
                        {
                            layers.Layer().AlignCenter().AlignMiddle().Element(x =>
                            {
                                x.Width(250).Image(watermarkPath).FitWidth();
                            });
                        }

                        layers.PrimaryLayer().PaddingVertical(15).Column(column =>
                        {
                            column.Spacing(18);

                            column.Item()
                                .Border(1)
                                .BorderColor(branding.BorderColorHex)
                                .Padding(12)
                                .Column(summary =>
                                {
                                    summary.Spacing(4);

                                    summary.Item()
                                        .Text("Certificate Summary")
                                        .FontSize(12)
                                        .SemiBold()
                                        .FontColor(branding.SectionAccentColorHex);

                                    summary.Item()
                                        .Text($"Created At: {submission.CreatedAtUtc:yyyy-MM-dd HH:mm}")
                                        .FontSize(10)
                                        .FontColor(Colors.Grey.Darken2);

                                    summary.Item()
                                        .Text($"Sent To External: {(submission.SentToExternalAtUtc.HasValue ? submission.SentToExternalAtUtc.Value.ToString("yyyy-MM-dd HH:mm") : "-")}")
                                        .FontSize(10)
                                        .FontColor(Colors.Grey.Darken2);

                                    summary.Item()
                                        .Text($"Owner Confirmed: {(submission.OwnerConfirmedAtUtc.HasValue ? submission.OwnerConfirmedAtUtc.Value.ToString("yyyy-MM-dd HH:mm") : "-")}")
                                        .FontSize(10)
                                        .FontColor(Colors.Grey.Darken2);

                                    summary.Item()
                                        .Text($"External Confirmed: {(submission.ExternalConfirmedAtUtc.HasValue ? submission.ExternalConfirmedAtUtc.Value.ToString("yyyy-MM-dd HH:mm") : "-")}")
                                        .FontSize(10)
                                        .FontColor(Colors.Grey.Darken2);

                                    var completedAtUtc = submission.Status == SubmissionStatus.Completed
                                    ? submission.UpdatedAtUtc
                                    : (DateTime?)null;

                                    summary.Item()
                                        .Text($"Completed At: {(completedAtUtc.HasValue ? completedAtUtc.Value.ToString("yyyy-MM-dd HH:mm") : "-")}")
                                        .FontSize(10)
                                        .FontColor(Colors.Grey.Darken2);
                                });

                            if (!string.IsNullOrWhiteSpace(submission.AgreementContentHtml))
                            {
                                RenderAgreementHtmlRichText(column, submission, branding);
                            }

                            column.Item()
                                .Text("Submission Details")
                                .FontSize(14)
                                .Bold()
                                .FontColor(branding.SectionAccentColorHex);

                            foreach (var field in submission.FieldsSnapshot)
                            {
                                if (field.Type == "Signature")
                                    continue;

                                var answer = submission.Answers
                                    .FirstOrDefault(a => a.FieldId == field.FieldId)?.Value;

                                RenderField(column, submission, field, answer, branding);
                            }

                            var signatureFields = submission.FieldsSnapshot
                                .Where(f => f.Type == "Signature")
                                .ToList();

                            if (signatureFields.Any())
                            {
                                column.Item()
                                    .PaddingTop(8)
                                    .Text("Signature Records")
                                    .FontSize(14)
                                    .Bold()
                                    .FontColor(branding.SectionAccentColorHex);

                                foreach (var field in signatureFields)
                                {
                                    var signature = submission.Signatures?
                                        .FirstOrDefault(s => s.FieldId == field.FieldId);

                                    column.Item()
                                        .Padding(10)
                                        .Column(item =>
                                        {
                                            item.Spacing(6);

                                            item.Item()
                                                .Text(field.Label)
                                                .FontSize(9)
                                                .FontColor(Colors.Grey.Darken1)
                                                .SemiBold();

                                            if (signature != null && !string.IsNullOrWhiteSpace(signature.SignatureUrl))
                                            {
                                                var imageBytes = GetUploadImageBytes(signature.SignatureUrl);
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
                                                        item.Item()
                                                            .Text("Signature image could not be rendered")
                                                            .FontColor(Colors.Red.Darken1);
                                                    }
                                                }
                                                else
                                                {
                                                    item.Item()
                                                        .Text("Signature not found")
                                                        .FontColor(Colors.Red.Darken1);
                                                }

                                                item.Item()
                                                    .Text(
                                                        $"Signed by: {signature.SignedByEmail ?? "-"} | " +
                                                        $"Signed at: {signature.SignedAtUtc:yyyy-MM-dd HH:mm}"
                                                    )
                                                    .FontSize(9)
                                                    .FontColor(Colors.Grey.Darken1);

                                                item.Item()
                                                    .Text(
                                                        $"IP: {MaskIp(signature?.SignedFromIpAddress)} | " +
                                                        $"Device: {signature?.SignedUserAgent ?? "-"}"
                                                    )
                                                    .FontSize(9)
                                                    .FontColor(Colors.Grey.Darken1);
                                            }
                                            else
                                            {
                                                item.Item()
                                                    .Text("-")
                                                    .FontColor(Colors.Grey.Darken1);
                                            }
                                        });
                                }
                            }
                        });
                    });

                    page.Footer().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Column(column =>
                        {
                            column.Item().Text(text =>
                            {
                                if (branding.ShowCompanyDetails && !string.IsNullOrWhiteSpace(branding.CompanyName))
                                {
                                    text.Span(branding.CompanyName + " • ")
                                        .FontSize(9)
                                        .FontColor(Colors.Grey.Darken1);
                                }

                                text.Span("Generated at ")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken1);

                                text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"))
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken1);
                            });

                            //column.Item().Text(verifyUrl).FontSize(7);
                            column.Item().Text($"Reference: {submission.Id}")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken1);
                            column.Item().Text("Scan QR to verify")
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken1);
                        });

                        row.ConstantItem(60).Height(60).Image(qrBytes);
                    });
                });
            });

            var pdfBytes = document.GeneratePdf();
            return pdfBytes;
        }
        catch (Exception ex)
        {
            throw new Exception($"PDF generation failed for submission {submission.Id}: {ex.Message}", ex);
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

    private byte[]? GetUploadImageBytes(string? fileUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
                return null;

            if (fileUrl.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                var commaIndex = fileUrl.IndexOf(',');
                if (commaIndex < 0)
                    return null;

                var base64 = fileUrl[(commaIndex + 1)..];
                return Convert.FromBase64String(base64);
            }

            if (fileUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            {
                var uploadsRoot = _configuration["UploadSettings:PhysicalRoot"];
                if (string.IsNullOrWhiteSpace(uploadsRoot))
                    return null;

                var relativePart = fileUrl["/uploads/".Length..]
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

    private void RenderAgreementHtml(IContainer container, string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        container.Column(column =>
        {
            column.Spacing(8);

            var rootNodes = doc.DocumentNode.Name.Equals("#document", StringComparison.OrdinalIgnoreCase)
                ? doc.DocumentNode.ChildNodes
                : doc.DocumentNode.SelectNodes("./*") ?? doc.DocumentNode.ChildNodes;

            foreach (var node in rootNodes)
            {
                RenderBlockNode(column, node);
            }
        });
    }

    private void RenderHeading(ColumnDescriptor column, HtmlNode node, float fontSize, bool bold)
    {
        var textValue = HtmlEntity.DeEntitize(node.InnerText).Trim();
        if (string.IsNullOrWhiteSpace(textValue))
            return;

        var alignment = GetTextAlignment(node);

        var item = ApplyAlignment(column.Item(), alignment);

        item.Text(text =>
        {
            var span = text.Span(textValue).FontSize(fontSize).FontColor(Colors.Black);

            if (bold)
                span.Bold();
        });
    }

    private void RenderBlockNode(ColumnDescriptor column, HtmlNode node)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            var textValue = HtmlEntity.DeEntitize(node.InnerText).Trim();

            if (!string.IsNullOrWhiteSpace(textValue))
            {
                column.Item()
                    .Text(textValue)
                    .FontSize(11)
                    .FontColor(Colors.Grey.Darken3);
            }

            return;
        }

        switch (node.Name.ToLowerInvariant())
        {
            case "h1":
                RenderHeading(column, node, 22, true);
                break;

            case "h2":
                RenderHeading(column, node, 18, true);
                break;

            case "h3":
                RenderHeading(column, node, 14, true);
                break;

            case "p":
                RenderParagraph(column, node);
                break;

            case "ul":
                RenderUnorderedList(column, node);
                break;

            case "ol":
                RenderOrderedList(column, node);
                break;

            case "br":
                column.Item().Height(4);
                break;

            case "div":
                foreach (var child in node.ChildNodes)
                    RenderBlockNode(column, child);
                break;

            default:
                foreach (var child in node.ChildNodes)
                    RenderBlockNode(column, child);
                break;
        }
    }

    private void RenderParagraph(ColumnDescriptor column, HtmlNode node)
    {
        if (string.IsNullOrWhiteSpace(HtmlEntity.DeEntitize(node.InnerText).Trim()))
            return;

        var alignment = GetTextAlignment(node);

        ApplyAlignment(column.Item(), alignment)
            .Text(text =>
            {
                text.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Grey.Darken3));

                foreach (var child in node.ChildNodes)
                {
                    RenderInlineNode(text, child);
                }
            });
    }

    private void RenderInlineNode(TextDescriptor text, HtmlNode node)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            var content = HtmlEntity.DeEntitize(node.InnerText);

            if (!string.IsNullOrWhiteSpace(content))
                text.Span(content);

            return;
        }

        switch (node.Name.ToLowerInvariant())
        {
            case "strong":
            case "b":
                text.Span(HtmlEntity.DeEntitize(node.InnerText)).Bold();
                break;

            case "em":
            case "i":
                text.Span(HtmlEntity.DeEntitize(node.InnerText)).Italic();
                break;

            case "u":
                text.Span(HtmlEntity.DeEntitize(node.InnerText)).Underline();
                break;

            case "a":
                {
                    var href = node.GetAttributeValue("href", string.Empty);
                    var linkText = HtmlEntity.DeEntitize(node.InnerText);

                    if (!string.IsNullOrWhiteSpace(linkText))
                    {
                        if (!string.IsNullOrWhiteSpace(href))
                            text.Hyperlink(href, linkText);
                        else
                            text.Span(linkText).Underline();
                    }

                    break;
                }

            case "br":
                text.Span("\n");
                break;

            default:
                foreach (var child in node.ChildNodes)
                {
                    RenderInlineNode(text, child);
                }
                break;
        }
    }

    private void RenderUnorderedList(ColumnDescriptor column, HtmlNode node)
    {
        var items = node.Elements("li").ToList();

        foreach (var li in items)
        {
            var textValue = HtmlEntity.DeEntitize(li.InnerText).Trim();
            if (string.IsNullOrWhiteSpace(textValue))
                continue;

            var alignment = GetTextAlignment(li);

            ApplyAlignment(column.Item().PaddingLeft(12), alignment)
                .Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Grey.Darken3));
                    text.Span("• ").SemiBold();

                    foreach (var child in li.ChildNodes)
                    {
                        RenderInlineNode(text, child);
                    }
                });
        }
    }

    private void RenderOrderedList(ColumnDescriptor column, HtmlNode node)
    {
        var items = node.Elements("li").ToList();

        for (var i = 0; i < items.Count; i++)
        {
            var li = items[i];
            var textValue = HtmlEntity.DeEntitize(li.InnerText).Trim();

            if (string.IsNullOrWhiteSpace(textValue))
                continue;

            var alignment = GetTextAlignment(li);

            ApplyAlignment(column.Item().PaddingLeft(12), alignment)
                .Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Grey.Darken3));
                    text.Span($"{i + 1}. ").SemiBold();

                    foreach (var child in li.ChildNodes)
                    {
                        RenderInlineNode(text, child);
                    }
                });
        }
    }

    private string GetTextAlignment(HtmlNode node)
    {
        var style = node.GetAttributeValue("style", string.Empty);

        if (!string.IsNullOrWhiteSpace(style))
        {
            var lower = style.ToLowerInvariant();

            if (lower.Contains("text-align: center"))
                return "center";

            if (lower.Contains("text-align:right") || lower.Contains("text-align: right"))
                return "right";

            if (lower.Contains("text-align:left") || lower.Contains("text-align: left"))
                return "left";
        }

        return "left";
    }

    private IContainer ApplyAlignment(IContainer container, string alignment)
    {
        return alignment switch
        {
            "center" => container.AlignCenter(),
            "right" => container.AlignRight(),
            _ => container.AlignLeft()
        };
    }

    private void RenderAgreementHtmlRichText(
        ColumnDescriptor column,
        FormSubmission submission,
        ResolvedPdfBranding branding)
    {
        column.Item()
            .Text("Subject")
            .FontSize(14)
            .Bold()
            .FontColor(branding.SectionAccentColorHex);

        column.Item()
              .Border(1)
              .BorderColor(branding.BorderColorHex)
              .Padding(12)
              .Column(col =>
              {
                  if (!string.IsNullOrWhiteSpace(submission.AgreementContentHtml))
                  {
                      col.Item().Element(x => RenderAgreementHtml(x, submission.AgreementContentHtml));
                  }
              });
    }

    private void RenderField(ColumnDescriptor column, FormSubmission submission, FieldDefinition field, string? answer, ResolvedPdfBranding branding)
    {
        switch (field.Type)
        {
            case "ShortText":
            case "Email":
            case "Number":
            case "Dropdown":
                RenderInlineField(column, field.Label, answer);
                break;

            case "Checkbox":
                RenderBooleanField(column, field.Label, answer);
                break;

            case "LongText":
                RenderBlockField(column, field.Label, answer);
                break;

            case "Agreement":
                RenderAgreementField(column, field, submission, branding);
                break;

            case "Signature":
                break;

            default:
                RenderInlineField(column, field.Label, answer);
                break;
        }
    }

    private void RenderInlineField(ColumnDescriptor column, string label, string? value)
    {
        column.Item().Row(row =>
        {
            row.RelativeItem(2).Text(label + ":")
                .FontSize(10)
                .SemiBold();

            row.RelativeItem(3).Text(string.IsNullOrWhiteSpace(value) ? "-" : value)
                .FontSize(11);
        });
    }

    private void RenderBlockField(ColumnDescriptor column, string label, string? value)
    {
        column.Item().Column(col =>
        {
            col.Spacing(4);

            col.Item().Text(label)
                .FontSize(10)
                .SemiBold();

            col.Item().Text(string.IsNullOrWhiteSpace(value) ? "-" : value)
                .FontSize(11);
        });
    }

    private void RenderBooleanField(ColumnDescriptor column, string label, string? value)
    {
        var isChecked = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

        column.Item().Text($"{(isChecked ? "[x]" : "[ ]")} {label}")
            .FontSize(11);
    }

    private void RenderAgreementField(ColumnDescriptor column, FieldDefinition field, FormSubmission submission, ResolvedPdfBranding branding)
    {
        var agreementTitle = string.IsNullOrWhiteSpace(field.Agreement?.Title)
            ? field.Label
            : field.Agreement.Title;

        var agreementAnswer = submission.Answers
            .FirstOrDefault(a => a.FieldId == field.FieldId)?.Value;

        var isAccepted = string.Equals(agreementAnswer, "true", StringComparison.OrdinalIgnoreCase);

        var acceptance = submission.AgreementAcceptances?
            .FirstOrDefault(x => x.FieldId == field.FieldId);

        column.Item()
            .Border(1)
            .BorderColor(branding.BorderColorHex)
            .Padding(12)
            .Column(col =>
            {
                col.Spacing(6);

                col.Item()
                    .Text(agreementTitle)
                    .FontSize(11)
                    .SemiBold();

                if (!string.IsNullOrWhiteSpace(field.Agreement?.Content))
                {
                    col.Item().Element(container =>
                    {
                        RenderAgreementHtml(container, field.Agreement.Content);
                    });
                }

                col.Item()
                    .PaddingTop(4)
                    .Text(isAccepted ? "[x] I accept this agreement" : "[ ] I accept this agreement")
                    .FontSize(10);

                col.Item()
                    .PaddingTop(4)
                    .Text(
                        $"Accepted by: {acceptance?.AcceptedByEmail ?? "-"} | " +
                        $"Accepted at: {(acceptance != null ? acceptance.AcceptedAtUtc.ToString("yyyy-MM-dd HH:mm") : "-")}"
                    )
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken1);

                col.Item()
                    .Text(
                        $"IP: {MaskIp(acceptance?.AcceptedFromIpAddress)} | " +
                        $"Device: {acceptance?.AcceptedUserAgent ?? "-"}"
                    )
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken1);
            });
    }

    private string MaskIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return "-";

        if (ip.Contains('.'))
        {
            var parts = ip.Split('.');
            if (parts.Length == 4)
                return $"{parts[0]}.{parts[1]}.{parts[2]}.xxx";
        }

        if (ip.Contains(':'))
        {
            return ip.Substring(0, Math.Min(10, ip.Length)) + "...";
        }

        return ip;
    }
}