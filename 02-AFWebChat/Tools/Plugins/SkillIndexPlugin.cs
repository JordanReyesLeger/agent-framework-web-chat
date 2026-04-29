using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using System.ComponentModel;

namespace AFWebChat.Tools.Plugins;

public class SkillIndexPlugin
{
    private readonly SearchClient? _searchClient;
    private readonly ILogger<SkillIndexPlugin> _logger;
    private readonly string _semanticConfigName;
    private readonly string _clientId;

    public SkillIndexPlugin(ILogger<SkillIndexPlugin> logger, IConfiguration configuration)
    {
        _logger = logger;
        _semanticConfigName = configuration["AzureSearch:SkillIndex:SemanticConfigName"] ?? "skill-semantic-config";
        _clientId = configuration["Tenant:ClientId"] ?? "default";
        var endpoint = configuration["AzureSearch:Endpoint"];
        var indexName = configuration["AzureSearch:SkillIndex:IndexName"] ?? "skill";
        var apiKey = configuration["AzureSearch:ApiKey"];

        if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey))
        {
            _searchClient = new SearchClient(new Uri(endpoint), indexName, new AzureKeyCredential(apiKey));
        }
        else
        {
            _logger.LogWarning("SkillIndexPlugin: Azure Search config missing.");
        }
    }

    [Description("Busca documentos en el índice 'skill' usando búsqueda híbrida semántica. Soporta filtros por ExpedienteId, DocumentoId, Titulo, TipoDocumento.")]
    public async Task<string> SearchSkillDocuments(
        [Description("Consulta en lenguaje natural")] string query,
        [Description("Filtro opcional por ExpedienteId")] string? expedienteId = null,
        [Description("Filtro opcional por DocumentoId")] string? documentoId = null,
        [Description("Filtro opcional por Titulo")] string? titulo = null,
        [Description("Filtro opcional por TipoDocumento")] string? tipoDocumento = null,
        [Description("Máximo de resultados (default: 20)")] int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        if (_searchClient == null) return "El índice 'skill' no está configurado.";
        try
        {
            maxResults = Math.Min(Math.Max(1, maxResults), 20);
            var filters = new List<string> { $"ClientId eq '{_clientId}'" };
            if (!string.IsNullOrEmpty(expedienteId)) filters.Add($"ExpedienteId eq '{expedienteId}'");
            if (!string.IsNullOrEmpty(titulo)) filters.Add($"Titulo eq '{titulo}'");
            if (!string.IsNullOrEmpty(documentoId)) filters.Add($"DocumentoId eq '{documentoId}'");
            if (!string.IsNullOrEmpty(tipoDocumento)) filters.Add($"TipoDocumento eq '{tipoDocumento}'");

            var options = new SearchOptions
            {
                QueryType = SearchQueryType.Semantic,
                Size = maxResults,
                SemanticSearch = new() { SemanticConfigurationName = _semanticConfigName },
                IncludeTotalCount = true,
                Filter = string.Join(" and ", filters)
            };
            foreach (var f in new[] { "Id", "ExpedienteId", "DocumentoId", "ChunkId", "ChunkIndex", "TipoDocumento", "Titulo", "FechaCreacion", "Contenido" })
                options.Select.Add(f);
            options.VectorSearch = new()
            {
                Queries = { new VectorizableTextQuery(text: query) { KNearestNeighborsCount = 50, Fields = { "Vector" }, Exhaustive = true } }
            };

            var results = await _searchClient.SearchAsync<SearchDocument>(query, options, cancellationToken);
            var docs = new List<SearchDocument>();
            await foreach (var r in results.Value.GetResultsAsync()) docs.Add(r.Document);

            if (docs.Count == 0) return "No se encontraron documentos relevantes.";

            var formatted = docs.Select((doc, i) =>
            {
                var contenido = GetField(doc, "Contenido");
                var titulo2 = GetField(doc, "Titulo");
                var expediente2 = GetField(doc, "ExpedienteId");
                var tipo = GetField(doc, "TipoDocumento");
                return $"**Resultado {i + 1}:**\nTítulo: {titulo2}\nExpediente: {expediente2}\nTipo: {tipo}\nContenido: {contenido}";
            });
            return $"Se encontraron {docs.Count} documento(s):\n\n" + string.Join("\n---\n", formatted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Skill index search error");
            return $"Error al buscar: {ex.Message}";
        }
    }

    private static string GetField(SearchDocument doc, string name) =>
        doc.ContainsKey(name) && doc[name] != null ? doc[name].ToString() ?? "N/A" : "N/A";
}
