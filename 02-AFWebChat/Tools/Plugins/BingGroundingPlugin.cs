using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AFWebChat.Tools.Plugins;

/// <summary>
/// Plugin that provides Bing Grounding search functionality using Azure AI Foundry.
/// Searches for a Bing-connected agent in Foundry and invokes it, or falls back to Bing Search API.
/// </summary>
public class BingGroundingPlugin
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<BingGroundingPlugin> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public BingGroundingPlugin(IConfiguration configuration, ILogger<BingGroundingPlugin> logger, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    [Description("Busca información actualizada y confiable en la web usando Bing Search. Proporciona resultados de búsqueda web con fuentes verificables.")]
    public async Task<string> SearchWithBingGrounding(
        [Description("La consulta de búsqueda en lenguaje natural")] string query,
        [Description("Número máximo de resultados (default: 5)")] int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["BingSearch:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            return "Bing Search no está configurado. Configura 'BingSearch:ApiKey' en appsettings.json.";
        }

        try
        {
            _logger.LogInformation("Bing Search: query={Query}, maxResults={MaxResults}", query, maxResults);

            var client = _httpClientFactory.CreateClient("BingSearch");
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);

            var encodedQuery = Uri.EscapeDataString(query);
            var url = $"https://api.bing.microsoft.com/v7.0/search?q={encodedQuery}&count={Math.Min(maxResults, 10)}&mkt=es-MX";

            var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var searchResults = JsonDocument.Parse(json);

            var results = new List<string>();

            if (searchResults.RootElement.TryGetProperty("webPages", out var webPages) &&
                webPages.TryGetProperty("value", out var pages))
            {
                foreach (var page in pages.EnumerateArray())
                {
                    var name = page.GetProperty("name").GetString();
                    var snippet = page.GetProperty("snippet").GetString();
                    var pageUrl = page.GetProperty("url").GetString();
                    results.Add($"**{name}**\n{snippet}\nFuente: {pageUrl}");
                }
            }

            if (results.Count == 0)
            {
                return "No se encontraron resultados para la búsqueda. Intenta reformular la consulta.";
            }

            _logger.LogInformation("Bing Search: Found {Count} results", results.Count);
            return $"Resultados de Bing ({results.Count}):\n\n" + string.Join("\n\n---\n\n", results);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Bing Search HTTP error for query: {Query}", query);
            return $"Error HTTP en Bing Search: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Bing Search for query: {Query}", query);
            return $"Error inesperado en Bing Search: {ex.Message}";
        }
    }
}
