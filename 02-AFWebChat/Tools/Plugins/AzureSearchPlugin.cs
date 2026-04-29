using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using System.ComponentModel;

namespace AFWebChat.Tools.Plugins;

public class AzureSearchPlugin
{
    private readonly SearchClient _searchClient;
    private readonly ILogger<AzureSearchPlugin> _logger;
    private readonly string _semanticConfigName;
    private readonly int _defaultMaxResults;
    private readonly string _clientId;

    public AzureSearchPlugin(SearchClient searchClient, ILogger<AzureSearchPlugin> logger, IConfiguration configuration)
    {
        _searchClient = searchClient;
        _logger = logger;
        _semanticConfigName = configuration["AzureSearch:SemanticConfigName"] ?? "skill-semantic-config";
        _defaultMaxResults = configuration.GetValue<int>("AzureSearch:DefaultMaxResults", 5);
        _clientId = configuration["Tenant:ClientId"] ?? "default";
    }

    [Description("Busca información en documentos indexados usando Azure Search. Utiliza búsqueda híbrida semántica para encontrar contenido relevante.")]
    public async Task<string> SearchDocuments(
        [Description("La consulta de búsqueda en lenguaje natural")] string query,
        [Description("Número máximo de resultados (default: 5, máximo: 15)")] int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            maxResults = Math.Min(Math.Max(1, maxResults), 15);
            _logger.LogInformation("Azure Search: query={Query}, maxResults={MaxResults}", query, maxResults);

            var options = new SearchOptions
            {
                QueryType = SearchQueryType.Semantic,
                Size = maxResults,
                SemanticSearch = new SemanticSearchOptions
                {
                    SemanticConfigurationName = _semanticConfigName
                },
                IncludeTotalCount = true,
                Filter = $"ClientId eq '{_clientId}'"
            };

            options.Select.Add("Id");
            options.Select.Add("ClientId");
            options.Select.Add("ExpedienteId");
            options.Select.Add("DocumentoId");
            options.Select.Add("ChunkId");
            options.Select.Add("ChunkIndex");
            options.Select.Add("TipoDocumento");
            options.Select.Add("Titulo");
            options.Select.Add("FechaCreacion");
            options.Select.Add("Contenido");

            options.VectorSearch = new()
            {
                Queries = {
                    new VectorizableTextQuery(text: query)
                    {
                        KNearestNeighborsCount = 50,
                        Fields = { "Vector" },
                        Exhaustive = true
                    }
                },
            };

            var results = await _searchClient.SearchAsync<SearchDocument>(query, options, cancellationToken);
            var docs = new List<SearchDocument>();
            await foreach (var result in results.Value.GetResultsAsync())
                docs.Add(result.Document);

            if (docs.Count == 0)
                return "No se encontraron documentos relevantes. Intenta reformular la pregunta.";

            _logger.LogInformation("Azure Search: Found {Count} documents", docs.Count);

            var formattedResults = docs.Select((doc, index) =>
            {
                var titulo = GetField(doc, "Titulo");
                var contenido = GetField(doc, "Contenido");
                var tipo = GetField(doc, "TipoDocumento");
                var expediente = GetField(doc, "ExpedienteId");
                var documento = GetField(doc, "DocumentoId");

                return $"**Resultado {index + 1}:**\nTítulo: {titulo}\nContenido: {contenido}\n" +
                       (tipo != "N/A" ? $"Tipo: {tipo}\n" : "") +
                       (expediente != "N/A" ? $"Expediente: {expediente}\n" : "") +
                       (documento != "N/A" ? $"Documento ID: {documento}\n" : "");
            });

            return $"Se encontraron {docs.Count} documento(s):\n\n" + string.Join("\n---\n", formattedResults);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Search failed: {Status}", ex.Status);
            return $"Error al buscar: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected Azure Search error");
            return $"Error inesperado: {ex.Message}";
        }
    }

    private static string GetField(SearchDocument doc, string name) =>
        doc.ContainsKey(name) && doc[name] != null ? doc[name].ToString() ?? "N/A" : "N/A";
}
