using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace AFWebChat.Services;

public interface IDocumentTextExtractor
{
    Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);
}

public class DocumentTextExtractorService : IDocumentTextExtractor
{
    private readonly ILogger<DocumentTextExtractorService> _logger;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".txt"
    };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const int MaxExtractedChars = 50_000; // ~12K tokens

    public DocumentTextExtractorService(ILogger<DocumentTextExtractorService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
            throw new ArgumentException($"Tipo de archivo '{extension}' no soportado. Tipos permitidos: {string.Join(", ", AllowedExtensions)}");

        if (stream.CanSeek && stream.Length > MaxFileSizeBytes)
            throw new ArgumentException($"El archivo excede el límite de {MaxFileSizeBytes / (1024 * 1024)} MB.");

        _logger.LogInformation("Extracting text from {FileName} ({Extension})", fileName, extension);

        var text = extension switch
        {
            ".pdf" => await ExtractFromPdfAsync(stream, cancellationToken),
            ".docx" => await ExtractFromDocxAsync(stream, cancellationToken),
            ".txt" => await ExtractFromTxtAsync(stream, cancellationToken),
            _ => throw new ArgumentException($"Tipo de archivo '{extension}' no soportado.")
        };

        if (text.Length > MaxExtractedChars)
        {
            _logger.LogWarning("Extracted text from {FileName} truncated from {OriginalLength} to {MaxLength} chars",
                fileName, text.Length, MaxExtractedChars);
            text = text[..MaxExtractedChars] + "\n\n[... Texto truncado por límite de tamaño ...]";
        }

        _logger.LogInformation("Extracted {CharCount} characters from {FileName}", text.Length, fileName);
        return text;
    }

    private Task<string> ExtractFromPdfAsync(Stream stream, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var sb = new StringBuilder();
            using var document = PdfDocument.Open(stream);

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pageText = page.Text;
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    sb.AppendLine(pageText);
                    sb.AppendLine();
                }
            }

            var result = sb.ToString().Trim();
            if (string.IsNullOrWhiteSpace(result))
            {
                return "[El PDF no contiene texto extraíble. Puede tratarse de un documento escaneado o basado en imágenes.]";
            }

            return result;
        }, cancellationToken);
    }

    private Task<string> ExtractFromDocxAsync(Stream stream, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var sb = new StringBuilder();
            using var document = WordprocessingDocument.Open(stream, false);
            var body = document.MainDocumentPart?.Document?.Body;

            if (body == null)
                return "[El documento Word no contiene contenido extraíble.]";

            foreach (var paragraph in body.Elements<Paragraph>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = paragraph.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                    sb.AppendLine(text);
            }

            var result = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(result)
                ? "[El documento Word no contiene texto extraíble.]"
                : result;
        }, cancellationToken);
    }

    private static async Task<string> ExtractFromTxtAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(text)
            ? "[El archivo de texto está vacío.]"
            : text.Trim();
    }

    public static bool IsSupported(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return AllowedExtensions.Contains(extension);
    }
}
