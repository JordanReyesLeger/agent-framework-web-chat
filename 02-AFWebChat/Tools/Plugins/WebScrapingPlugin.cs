using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace AFWebChat.Tools.Plugins;

public partial class WebScrapingPlugin
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebScrapingPlugin> _logger;

    public WebScrapingPlugin(IHttpClientFactory httpClientFactory, ILogger<WebScrapingPlugin> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [Description("Extrae el contenido de texto de una página web dada su URL. Útil para obtener información detallada de una página específica.")]
    public async Task<string> ScrapeWebPage(
        [Description("URL completa de la página web (ejemplo: https://example.com)")] string url,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "Error: URL vacía.";

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return "Error: URL no válida. Incluye http:// o https://";

        try
        {
            _logger.LogInformation("WebScraping: Fetching {Url}", url);
            var client = _httpClientFactory.CreateClient("WebScraping");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; AFWebChatBot/1.0)");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
            client.Timeout = TimeSpan.FromSeconds(30);

            var response = await client.GetAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var text = ExtractTextFromHtml(html);

            if (string.IsNullOrWhiteSpace(text))
                return "No se pudo extraer contenido de la página.";

            const int maxLength = 15_000;
            if (text.Length > maxLength)
                text = string.Concat(text.AsSpan(0, maxLength), "\n\n[... contenido truncado ...]");

            _logger.LogInformation("WebScraping: Extracted {Length} chars from {Url}", text.Length, url);
            return text;
        }
        catch (HttpRequestException ex) { return $"Error HTTP: {ex.Message}"; }
        catch (TaskCanceledException) { return "Timeout al acceder a la página."; }
        catch (Exception ex) { return $"Error inesperado: {ex.Message}"; }
    }

    private static string ExtractTextFromHtml(string html)
    {
        var cleaned = ScriptTagRegex().Replace(html, " ");
        cleaned = StyleTagRegex().Replace(cleaned, " ");
        cleaned = HeadTagRegex().Replace(cleaned, " ");
        cleaned = HtmlTagRegex().Replace(cleaned, " ");
        cleaned = cleaned.Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&#39;", "'");
        cleaned = WhitespaceRegex().Replace(cleaned, " ");
        cleaned = BlankLinesRegex().Replace(cleaned, "\n");
        return cleaned.Trim();
    }

    [GeneratedRegex(@"<script[^>]*>[\s\S]*?</script>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTagRegex();
    [GeneratedRegex(@"<style[^>]*>[\s\S]*?</style>", RegexOptions.IgnoreCase)]
    private static partial Regex StyleTagRegex();
    [GeneratedRegex(@"<head[^>]*>[\s\S]*?</head>", RegexOptions.IgnoreCase)]
    private static partial Regex HeadTagRegex();
    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();
    [GeneratedRegex(@"[^\S\n]+")]
    private static partial Regex WhitespaceRegex();
    [GeneratedRegex(@"(\s*\n){3,}")]
    private static partial Regex BlankLinesRegex();
}
