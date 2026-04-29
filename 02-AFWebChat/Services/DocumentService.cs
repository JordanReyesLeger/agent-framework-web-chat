using System.ClientModel;
using System.Text;
using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using OpenAI.Embeddings;

namespace AFWebChat.Services;

public class DocumentService
{
    private readonly ILogger<DocumentService> _logger;
    private readonly BlobContainerClient? _blobContainerClient;
    private readonly SearchIndexClient? _searchIndexClient;
    private readonly SearchClient? _searchClient;
    private readonly EmbeddingClient? _embeddingClient;
    private readonly IConfiguration _config;
    private readonly string _indexName;

    private const int ChunkSize = 1000;
    private const int ChunkOverlap = 200;

    public DocumentService(IConfiguration config, ILogger<DocumentService> logger)
    {
        _config = config;
        _logger = logger;
        _indexName = config["AzureSearch:IndexName"] ?? "skill";

        // Azure Blob Storage - Use DefaultAzureCredential (key-based auth may be disabled)
        var blobConnectionString = config["BlobStorage:ConnectionString"];
        var containerName = config["BlobStorage:ContainerName"] ?? "documents";
        if (!string.IsNullOrEmpty(blobConnectionString))
        {
            // Extract account name from connection string to build URI for managed identity auth
            var accountName = ExtractAccountName(blobConnectionString);
            if (!string.IsNullOrEmpty(accountName))
            {
                var blobUri = new Uri($"https://{accountName}.blob.core.windows.net");
                var blobServiceClient = new BlobServiceClient(blobUri, new DefaultAzureCredential());
                _blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
                _logger.LogInformation("DocumentService: Blob Storage configurado con DefaultAzureCredential (account={Account}, container={Container})", accountName, containerName);
            }
            else
            {
                _logger.LogWarning("DocumentService: No se pudo extraer el AccountName del connection string.");
            }
        }
        else
        {
            _logger.LogWarning("DocumentService: BlobStorage:ConnectionString no configurado.");
        }

        // Azure AI Search
        var searchEndpoint = config["AzureSearch:Endpoint"];
        if (!string.IsNullOrEmpty(searchEndpoint))
        {
            var searchUri = new Uri(searchEndpoint);
            var credential = new DefaultAzureCredential();
            _searchIndexClient = new SearchIndexClient(searchUri, credential);
            _searchClient = new SearchClient(searchUri, _indexName, credential);
            _logger.LogInformation("DocumentService: Azure Search configurado (index={Index})", _indexName);
        }
        else
        {
            _logger.LogWarning("DocumentService: AzureSearch:Endpoint no configurado.");
        }

        // Azure OpenAI Embeddings
        var openAiEndpoint = config["AzureOpenAI:Endpoint"];
        var embeddingDeployment = config["AzureOpenAI:EmbeddingDeployment"] ?? "text-embedding-3-large";
        var apiKey = config["AzureOpenAI:ApiKey"];
        if (!string.IsNullOrEmpty(openAiEndpoint))
        {
            Azure.AI.OpenAI.AzureOpenAIClient aoaiClient;
            if (!string.IsNullOrEmpty(apiKey))
                aoaiClient = new Azure.AI.OpenAI.AzureOpenAIClient(new Uri(openAiEndpoint), new ApiKeyCredential(apiKey));
            else
                aoaiClient = new Azure.AI.OpenAI.AzureOpenAIClient(new Uri(openAiEndpoint), new DefaultAzureCredential());

            _embeddingClient = aoaiClient.GetEmbeddingClient(embeddingDeployment);
            _logger.LogInformation("DocumentService: Embedding client configurado (deployment={Deployment})", embeddingDeployment);
        }
        else
        {
            _logger.LogWarning("DocumentService: AzureOpenAI:Endpoint no configurado para embeddings.");
        }
    }

    // ─── Upload document to Blob + Index in Azure Search ──────────────────
    public async Task<(bool Success, string Message)> UploadAsync(
        string fileName, Stream stream, string expedienteId, string tipoDocumento, string? documentoId = null)
    {
        if (_blobContainerClient is null)
            return (false, "Blob Storage no está configurado.");

        try
        {
            // Ensure container exists
            await _blobContainerClient.CreateIfNotExistsAsync(PublicAccessType.None);

            // Read file content into memory for both upload and processing
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            // Upload to Blob Storage with metadata
            var blobName = $"{expedienteId}/{fileName}";
            var blobClient = _blobContainerClient.GetBlobClient(blobName);

            var metadata = new Dictionary<string, string>
            {
                ["expedienteId"] = expedienteId,
                ["tipoDocumento"] = tipoDocumento,
                ["documentoId"] = documentoId ?? Guid.NewGuid().ToString("N"),
                ["uploadedAt"] = DateTime.UtcNow.ToString("o")
            };

            var uploadOptions = new BlobUploadOptions
            {
                Metadata = metadata,
                HttpHeaders = new BlobHttpHeaders { ContentType = GetContentType(fileName) }
            };

            using var uploadStream = new MemoryStream(fileBytes);
            await blobClient.UploadAsync(uploadStream, uploadOptions);
            _logger.LogInformation("Blob uploaded: {BlobName}", blobName);

            // Extract text and index
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var textContent = await ExtractTextAsync(fileBytes, extension, fileName);

            if (!string.IsNullOrWhiteSpace(textContent) && _searchClient is not null && _embeddingClient is not null)
            {
                var docId = metadata["documentoId"];
                await IndexDocumentAsync(textContent, fileName, expedienteId, docId, tipoDocumento);
                return (true, $"Documento '{fileName}' subido e indexado correctamente ({textContent.Length} caracteres extraídos).");
            }

            if (string.IsNullOrWhiteSpace(textContent))
                return (true, $"Documento '{fileName}' subido a Blob Storage. No se pudo extraer texto para indexar (tipo: {extension}).");

            return (true, $"Documento '{fileName}' subido a Blob Storage. Azure Search no configurado para indexar.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document {FileName}", fileName);
            return (false, $"Error al subir documento: {ex.Message}");
        }
    }

    // ─── List documents from Blob Storage ─────────────────────────────────
    public async Task<List<string>> ListDocumentsAsync()
    {
        if (_blobContainerClient is null) return [];

        var documents = new List<string>();
        try
        {
            await foreach (var blob in _blobContainerClient.GetBlobsAsync(BlobTraits.Metadata))
            {
                documents.Add(blob.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents");
        }
        return documents;
    }

    // ─── Delete document from Blob + Search index ─────────────────────────
    public async Task<(bool Success, string Message)> DeleteDocumentAsync(string blobName)
    {
        if (_blobContainerClient is null)
            return (false, "Blob Storage no está configurado.");

        try
        {
            var blobClient = _blobContainerClient.GetBlobClient(blobName);
            var exists = await blobClient.ExistsAsync();
            if (!exists.Value)
                return (false, $"Documento '{blobName}' no encontrado.");

            // Get metadata to find documentoId for search index cleanup
            var properties = await blobClient.GetPropertiesAsync();
            var docId = properties.Value.Metadata.TryGetValue("documentoId", out var id) ? id : null;

            // Delete blob
            await blobClient.DeleteAsync();
            _logger.LogInformation("Blob deleted: {BlobName}", blobName);

            // Delete associated chunks from search index
            if (_searchClient is not null && !string.IsNullOrEmpty(docId))
            {
                await DeleteChunksFromIndexAsync(docId);
            }

            return (true, $"Documento '{blobName}' eliminado correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {BlobName}", blobName);
            return (false, $"Error al eliminar: {ex.Message}");
        }
    }

    // ─── Setup Azure Search Index ─────────────────────────────────────────
    public async Task<(bool Success, string Message)> SetupIndexAsync()
    {
        if (_searchIndexClient is null)
            return (false, "Azure Search no está configurado.");

        try
        {
            var openAiEndpoint = _config["AzureOpenAI:Endpoint"] ?? "";
            var embeddingDeployment = _config["AzureOpenAI:EmbeddingDeployment"] ?? "text-embedding-3-large";
            var apiKey = _config["AzureOpenAI:ApiKey"];

            var vectorSearch = new VectorSearch();

            // Algorithm configuration
            var algorithmConfig = new HnswAlgorithmConfiguration("hnsw-config")
            {
                Parameters = new HnswParameters
                {
                    Metric = VectorSearchAlgorithmMetric.Cosine,
                    M = 4,
                    EfConstruction = 400,
                    EfSearch = 500
                }
            };
            vectorSearch.Algorithms.Add(algorithmConfig);

            // Vectorizer for query-time vectorization (used by VectorizableTextQuery)
            if (!string.IsNullOrEmpty(openAiEndpoint))
            {
                var vectorizer = new AzureOpenAIVectorizer("openai-vectorizer")
                {
                    Parameters = new AzureOpenAIVectorizerParameters
                    {
                        ResourceUri = new Uri(openAiEndpoint),
                        DeploymentName = embeddingDeployment,
                        ModelName = embeddingDeployment
                    }
                };

                // Use API key if available, otherwise managed identity is used by default
                if (!string.IsNullOrEmpty(apiKey))
                {
                    vectorizer.Parameters.ApiKey = apiKey;
                }

                vectorSearch.Vectorizers.Add(vectorizer);

                vectorSearch.Profiles.Add(new VectorSearchProfile("vector-profile", "hnsw-config")
                {
                    VectorizerName = "openai-vectorizer"
                });
            }
            else
            {
                vectorSearch.Profiles.Add(new VectorSearchProfile("vector-profile", "hnsw-config"));
            }

            var index = new SearchIndex(_indexName)
            {
                Fields =
                {
                    new SimpleField("Id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                    new SimpleField("ClientId", SearchFieldDataType.String) { IsFilterable = true },
                    new SearchableField("ExpedienteId") { IsFilterable = true, IsFacetable = true },
                    new SearchableField("DocumentoId") { IsFilterable = true },
                    new SimpleField("ChunkId", SearchFieldDataType.String) { IsFilterable = true },
                    new SimpleField("ChunkIndex", SearchFieldDataType.Int32) { IsSortable = true },
                    new SearchableField("TipoDocumento") { IsFilterable = true, IsFacetable = true },
                    new SearchableField("Titulo") { IsFilterable = true },
                    new SimpleField("FechaCreacion", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
                    new SearchableField("Contenido") { AnalyzerName = LexicalAnalyzerName.EsLucene },
                    new SearchField("Vector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                    {
                        IsSearchable = true,
                        VectorSearchDimensions = 3072, // text-embedding-3-large
                        VectorSearchProfileName = "vector-profile"
                    }
                },
                VectorSearch = vectorSearch,
                SemanticSearch = new SemanticSearch
                {
                    Configurations =
                    {
                        new SemanticConfiguration("skill-semantic-config", new SemanticPrioritizedFields
                        {
                            TitleField = new SemanticField("Titulo"),
                            ContentFields = { new SemanticField("Contenido") },
                            KeywordsFields = { new SemanticField("TipoDocumento"), new SemanticField("ExpedienteId") }
                        })
                    },
                    DefaultConfigurationName = "skill-semantic-config"
                }
            };

            await _searchIndexClient.CreateOrUpdateIndexAsync(index);
            _logger.LogInformation("Search index '{IndexName}' created/updated", _indexName);

            return (true, $"Índice '{_indexName}' creado/actualizado correctamente con {index.Fields.Count} campos, búsqueda vectorial y configuración semántica.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up search index");
            return (false, $"Error al configurar índice: {ex.Message}");
        }
    }

    // ─── Re-index all blobs ───────────────────────────────────────────────
    public async Task<(bool Success, string Message)> RunIndexerAsync()
    {
        if (_blobContainerClient is null)
            return (false, "Blob Storage no está configurado.");
        if (_searchClient is null || _embeddingClient is null)
            return (false, "Azure Search o Embeddings no están configurados.");

        try
        {
            int successCount = 0, errorCount = 0;

            await foreach (var blob in _blobContainerClient.GetBlobsAsync(BlobTraits.Metadata))
            {
                try
                {
                    var blobClient = _blobContainerClient.GetBlobClient(blob.Name);
                    using var downloadStream = new MemoryStream();
                    await blobClient.DownloadToAsync(downloadStream);
                    var fileBytes = downloadStream.ToArray();

                    var extension = Path.GetExtension(blob.Name).ToLowerInvariant();
                    var fileName = Path.GetFileName(blob.Name);
                    var textContent = await ExtractTextAsync(fileBytes, extension, fileName);

                    if (string.IsNullOrWhiteSpace(textContent))
                    {
                        _logger.LogWarning("No text extracted from blob: {BlobName}", blob.Name);
                        errorCount++;
                        continue;
                    }

                    var metadata = blob.Metadata ?? new Dictionary<string, string>();
                    metadata.TryGetValue("expedienteId", out var expedienteId);
                    metadata.TryGetValue("documentoId", out var documentoId);
                    metadata.TryGetValue("tipoDocumento", out var tipoDocumento);
                    expedienteId ??= "unknown";
                    documentoId ??= Guid.NewGuid().ToString("N");
                    tipoDocumento ??= "general";

                    // Delete existing chunks for this document
                    await DeleteChunksFromIndexAsync(documentoId);

                    await IndexDocumentAsync(textContent, fileName, expedienteId, documentoId, tipoDocumento);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error indexing blob: {BlobName}", blob.Name);
                    errorCount++;
                }
            }

            return (true, $"Indexación completada: {successCount} documento(s) indexados, {errorCount} error(es).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running indexer");
            return (false, $"Error al ejecutar indexador: {ex.Message}");
        }
    }

    // ─── Private: Text extraction ─────────────────────────────────────────
    private Task<string> ExtractTextAsync(byte[] fileBytes, string extension, string fileName)
    {
        return extension switch
        {
            ".txt" or ".md" => Task.FromResult(Encoding.UTF8.GetString(fileBytes)),
            ".pdf" or ".docx" or ".doc" => Task.FromResult(
                $"[Contenido del archivo {fileName} - Se requiere Azure Document Intelligence para extraer texto de archivos {extension}. " +
                $"Tamaño: {fileBytes.Length} bytes]"),
            _ => Task.FromResult(string.Empty)
        };
    }

    // ─── Private: Chunk text ──────────────────────────────────────────────
    private static List<string> ChunkText(string text)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;

        int start = 0;
        while (start < text.Length)
        {
            int end = Math.Min(start + ChunkSize, text.Length);

            // Try to break at a sentence boundary
            if (end < text.Length)
            {
                var breakPoint = text.LastIndexOfAny(['.', '\n', '!', '?'], end - 1, Math.Min(200, end - start));
                if (breakPoint > start)
                    end = breakPoint + 1;
            }

            chunks.Add(text[start..end].Trim());
            start = Math.Max(start + 1, end - ChunkOverlap);
        }

        return chunks.Where(c => c.Length > 10).ToList();
    }

    // ─── Private: Index document chunks ───────────────────────────────────
    private async Task IndexDocumentAsync(
        string textContent, string fileName, string expedienteId, string documentoId, string tipoDocumento)
    {
        var chunks = ChunkText(textContent);
        if (chunks.Count == 0) return;

        _logger.LogInformation("Indexing {ChunkCount} chunks for document {FileName}", chunks.Count, fileName);

        var documents = new List<SearchDocument>();
        var now = DateTimeOffset.UtcNow;

        // Generate embeddings in batches
        var embeddings = await GenerateEmbeddingsAsync(chunks);

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunkId = $"{documentoId}-chunk-{i:D4}";
            var doc = new SearchDocument
            {
                ["Id"] = chunkId,
                ["ClientId"] = _config["Tenant:ClientId"] ?? "default",
                ["ExpedienteId"] = expedienteId,
                ["DocumentoId"] = documentoId,
                ["ChunkId"] = chunkId,
                ["ChunkIndex"] = i,
                ["TipoDocumento"] = tipoDocumento,
                ["Titulo"] = fileName,
                ["FechaCreacion"] = now,
                ["Contenido"] = chunks[i],
                ["Vector"] = embeddings.Count > i ? embeddings[i] : Array.Empty<float>()
            };
            documents.Add(doc);
        }

        // Upload in batches of 100
        for (int i = 0; i < documents.Count; i += 100)
        {
            var batch = IndexDocumentsBatch.MergeOrUpload(documents.Skip(i).Take(100));
            var response = await _searchClient!.IndexDocumentsAsync(batch);
            _logger.LogInformation("Indexed batch {Batch}: {Count} documents", i / 100 + 1, response.Value.Results.Count);
        }
    }

    // ─── Private: Generate embeddings ─────────────────────────────────────
    private async Task<List<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(List<string> texts)
    {
        if (_embeddingClient is null) return [];

        var result = new List<ReadOnlyMemory<float>>();
        try
        {
            // Process in batches of 16 (API limit for some models)
            for (int i = 0; i < texts.Count; i += 16)
            {
                var batch = texts.Skip(i).Take(16).ToList();
                var response = await _embeddingClient.GenerateEmbeddingsAsync(batch);

                foreach (var embedding in response.Value)
                {
                    result.Add(embedding.ToFloats());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embeddings");
        }
        return result;
    }

    // ─── Private: Delete chunks from search index ─────────────────────────
    private async Task DeleteChunksFromIndexAsync(string documentoId)
    {
        if (_searchClient is null) return;

        try
        {
            var options = new SearchOptions
            {
                Filter = $"DocumentoId eq '{documentoId}'",
                Size = 1000
            };
            options.Select.Add("Id");

            var searchResults = await _searchClient.SearchAsync<SearchDocument>("*", options);
            var idsToDelete = new List<SearchDocument>();

            await foreach (var result in searchResults.Value.GetResultsAsync())
            {
                idsToDelete.Add(new SearchDocument { ["Id"] = result.Document["Id"]?.ToString() });
            }

            if (idsToDelete.Count > 0)
            {
                var batch = IndexDocumentsBatch.Delete(idsToDelete);
                await _searchClient.IndexDocumentsAsync(batch);
                _logger.LogInformation("Deleted {Count} chunks for document {DocumentoId}", idsToDelete.Count, documentoId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting chunks for document {DocumentoId}", documentoId);
        }
    }

    // ─── Private: Content type mapping ────────────────────────────────────
    private static string GetContentType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            _ => "application/octet-stream"
        };

    // ─── Private: Extract account name from connection string ─────────────
    private static string? ExtractAccountName(string connectionString)
    {
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("AccountName=", StringComparison.OrdinalIgnoreCase))
                return trimmed["AccountName=".Length..];
        }
        return null;
    }
}
