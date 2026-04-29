using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using System.ComponentModel;

namespace AFWebChat.Tools.Plugins;

public class LegalIndexPlugin
{
    private const string NORMATIVE_DOCUMENT_NAME = "MarcoNormativo.pdf";
    private readonly SearchClient? _searchClient;
    private readonly ILogger<LegalIndexPlugin> _logger;
    private readonly string _semanticConfigName;

    public LegalIndexPlugin(ILogger<LegalIndexPlugin> logger, IConfiguration configuration)
    {
        _logger = logger;
        _semanticConfigName = configuration["AzureSearch:SkillIndex:SemanticConfigName"] ?? "skill-semantic-config";
        var endpoint = configuration["AzureSearch:Endpoint"];
        var indexName = configuration["AzureSearch:SkillIndex:IndexName"] ?? "skill";
        var apiKey = configuration["AzureSearch:ApiKey"];

        if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey))
        {
            _searchClient = new SearchClient(new Uri(endpoint), indexName, new AzureKeyCredential(apiKey));
        }
        else
        {
            _logger.LogWarning("LegalIndexPlugin: Azure Search config missing.");
        }
    }

    [Description("Busca demandas en el índice legal. Soporta filtros por ExpedienteId, DocumentoId, Titulo, TipoDocumento.")]
    public async Task<string> SearchDemanda(
        [Description("Consulta en lenguaje natural sobre la demanda")] string query,
        [Description("Filtro opcional por ExpedienteId")] string? expedienteId = null,
        [Description("Filtro opcional por DocumentoId")] string? documentoId = null,
        [Description("Filtro opcional por Titulo")] string? titulo = null,
        [Description("Filtro opcional por TipoDocumento")] string? tipoDocumento = null,
        [Description("Máximo de resultados (default: 20)")] int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        if (_searchClient == null) return "El índice legal no está configurado.";
        try
        {
            maxResults = Math.Min(Math.Max(1, maxResults), 20);
            var filters = new List<string>();
            if (!string.IsNullOrEmpty(expedienteId)) filters.Add($"ExpedienteId eq '{EscapeOData(expedienteId)}'");
            if (!string.IsNullOrEmpty(titulo)) filters.Add($"Titulo eq '{EscapeOData(titulo)}'");
            if (!string.IsNullOrEmpty(documentoId)) filters.Add($"DocumentoId eq '{EscapeOData(documentoId)}'");
            if (!string.IsNullOrEmpty(tipoDocumento)) filters.Add($"TipoDocumento eq '{EscapeOData(tipoDocumento)}'");

            var options = BuildSearchOptions(maxResults, filters.Count > 0 ? string.Join(" and ", filters) : null);
            var results = await _searchClient.SearchAsync<SearchDocument>(query, options, cancellationToken);
            return await FormatResults(results, "demanda(s)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Legal search error");
            return $"Error al buscar demandas: {ex.Message}";
        }
    }

    [Description("Busca artículos en el marco normativo vigente para fundamentos legales.")]
    public async Task<string> SearchConstitution(
        [Description("Consulta sobre artículos normativos")] string query,
        [Description("Máximo de resultados (default: 15)")] int maxResults = 15,
        CancellationToken cancellationToken = default)
    {
        if (_searchClient == null) return "El índice legal no está configurado.";
        try
        {
            maxResults = Math.Min(Math.Max(1, maxResults), 15);
            var options = BuildSearchOptions(maxResults, $"Titulo eq '{NORMATIVE_DOCUMENT_NAME}'");
            var results = await _searchClient.SearchAsync<SearchDocument>(query, options, cancellationToken);
            return await FormatResults(results, "artículo(s) normativo(s)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Normative search error");
            return $"Error al buscar normativos: {ex.Message}";
        }
    }

    private SearchOptions BuildSearchOptions(int maxResults, string? filter)
    {
        var options = new SearchOptions
        {
            QueryType = SearchQueryType.Semantic,
            Size = maxResults,
            SemanticSearch = new() { SemanticConfigurationName = _semanticConfigName },
            IncludeTotalCount = true,
            Filter = filter
        };
        foreach (var f in new[] { "Id", "ExpedienteId", "DocumentoId", "ChunkId", "ChunkIndex", "TipoDocumento", "Titulo", "FechaCreacion", "Contenido" })
            options.Select.Add(f);
        options.VectorSearch = new()
        {
            Queries = { new VectorizableTextQuery(text: "") { KNearestNeighborsCount = 50, Fields = { "Vector" }, Exhaustive = true } }
        };
        return options;
    }

    private static async Task<string> FormatResults(Azure.Response<SearchResults<SearchDocument>> results, string label)
    {
        var docs = new List<SearchDocument>();
        await foreach (var r in results.Value.GetResultsAsync()) docs.Add(r.Document);
        if (docs.Count == 0) return $"No se encontraron {label} relevantes.";

        var formatted = docs.Select((doc, i) =>
        {
            var contenido = GetField(doc, "Contenido");
            var titulo = GetField(doc, "Titulo");
            var expediente = GetField(doc, "ExpedienteId");
            var tipo = GetField(doc, "TipoDocumento");
            return $"**Resultado {i + 1}:**\nTítulo: {titulo}\nExpediente: {expediente}\nTipo: {tipo}\nContenido: {contenido}";
        });
        return $"Se encontraron {docs.Count} {label}:\n\n" + string.Join("\n---\n", formatted);
    }

    private static string GetField(SearchDocument doc, string name) =>
        doc.ContainsKey(name) && doc[name] != null ? doc[name].ToString() ?? "N/A" : "N/A";

    private static string EscapeOData(string value) => value?.Replace("'", "''") ?? "";
}
