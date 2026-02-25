using System.Text;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace NeuroGateway.Utils.Parsing;

public static class DocumentExtractor
{
    public static string ExtractText(byte[] fileBytes, string documentType)
    {
        return documentType.ToLowerInvariant() switch
        {
            "pdf" => ExtractPdf(fileBytes),
            "docx" => ExtractDocx(fileBytes),
            _ => throw new ArgumentException($"Unsupported document type: {documentType}. Use 'pdf' or 'docx'.")
        };
    }

    public static string ExtractText(string base64Content, string documentType) =>
        ExtractText(Convert.FromBase64String(base64Content), documentType);

    private static string ExtractPdf(byte[] bytes)
    {
        using var doc = PdfDocument.Open(bytes);
        var sb = new StringBuilder();
        foreach (var page in doc.GetPages())
        {
            sb.AppendLine(page.Text);
            sb.AppendLine();
        }
        return sb.ToString().Trim();
    }

    private static string ExtractDocx(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var para in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
        {
            sb.AppendLine(para.InnerText);
        }
        return sb.ToString().Trim();
    }
}
