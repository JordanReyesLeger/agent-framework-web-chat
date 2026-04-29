using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using AFWebChat.Models;

namespace AFWebChat.Services;

public interface IDocumentIndexingService
{
    Task<bool> CreateOrUpdateIndexAsync(IndexConfiguration config, CancellationToken cancellationToken = default);
    Task<bool> CreateOrUpdateSkillsetAsync(string indexName, CancellationToken cancellationToken = default);
    Task<bool> CreateOrUpdateIndexerAsync(string indexName, CancellationToken cancellationToken = default);
    Task<bool> RunIndexerAsync(string indexName, CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentFromIndexAsync(string fileName, string indexName, CancellationToken cancellationToken = default);
}

public class DocumentIndexingService : IDocumentIndexingService
{
    private readonly SearchIndexClient _indexClient;
    private readonly SearchIndexerClient _indexerClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentIndexingService> _logger;
    private readonly string _clientId;

    public DocumentIndexingService(IConfiguration configuration, ILogger<DocumentIndexingService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _clientId = configuration["Tenant:ClientId"] ?? "default";

        var searchEndpoint = configuration["AzureSearch:Endpoint"];
        if (string.IsNullOrEmpty(searchEndpoint))
        {
            throw new InvalidOperationException("AzureSearch:Endpoint is not configured");
        }

        var credential = new DefaultAzureCredential();
        _indexClient = new SearchIndexClient(new Uri(searchEndpoint), credential);
        _indexerClient = new SearchIndexerClient(new Uri(searchEndpoint), credential);
    }

    public async Task<bool> CreateOrUpdateIndexAsync(IndexConfiguration config, CancellationToken cancellationToken = default)
    {
        try
        {
            var fields = new List<SearchField>();

            foreach (var fieldDef in config.Fields)
            {
                if (fieldDef.IsVector)
                {
                    fields.Add(new SearchField(fieldDef.Name, SearchFieldDataType.Collection(SearchFieldDataType.Single))
                    {
                        VectorSearchDimensions = config.VectorDimensions,
                        VectorSearchProfileName = config.VectorProfileName
                    });
                }
                else if (fieldDef.IsKey)
                {
                    fields.Add(new SearchField(fieldDef.Name, SearchFieldDataType.String)
                    {
                        IsKey = true,
                        IsFilterable = fieldDef.IsFilterable,
                        IsSortable = fieldDef.IsSortable,
                        IsFacetable = fieldDef.IsFacetable,
                        AnalyzerName = LexicalAnalyzerName.Keyword
                    });
                }
                else
                {
                    var field = fieldDef.IsSearchable
                        ? new SearchableField(fieldDef.Name)
                        {
                            IsKey = fieldDef.IsKey,
                            IsFilterable = fieldDef.IsFilterable,
                            IsSortable = fieldDef.IsSortable,
                            IsFacetable = fieldDef.IsFacetable
                        }
                        : new SimpleField(fieldDef.Name, GetSearchFieldDataType(fieldDef.Type))
                        {
                            IsKey = fieldDef.IsKey,
                            IsFilterable = fieldDef.IsFilterable,
                            IsSortable = fieldDef.IsSortable,
                            IsFacetable = fieldDef.IsFacetable
                        };
                    fields.Add(field);
                }
            }

            if (!fields.Any(f => f.Name == "ChunkId"))
            {
                fields.Add(new SimpleField("ChunkId", SearchFieldDataType.String)
                {
                    IsKey = false,
                    IsFilterable = true,
                    IsSortable = false,
                    IsFacetable = false
                });
            }

            var openAiEndpoint = _configuration["AzureOpenAI:Endpoint"];
            var embeddingsDeployment = _configuration["AzureOpenAI:EmbeddingsDeploymentName"]
                ?? _configuration["AzureOpenAI:EmbeddingDeployment"]
                ?? "text-embedding-3-large";
            var openAiApiKey = _configuration["AzureOpenAI:ApiKey"];

            var vectorizerParams = new AzureOpenAIVectorizerParameters
            {
                ResourceUri = new Uri(openAiEndpoint!),
                DeploymentName = embeddingsDeployment,
                ModelName = AzureOpenAIModelName.TextEmbedding3Large
            };

            if (!string.IsNullOrEmpty(openAiApiKey))
            {
                vectorizerParams.ApiKey = openAiApiKey;
            }

            var vectorSearch = new VectorSearch
            {
                Profiles =
                {
                    new VectorSearchProfile(config.VectorProfileName, "myHnsw")
                    {
                        VectorizerName = "myOpenAI"
                    }
                },
                Algorithms =
                {
                    new HnswAlgorithmConfiguration("myHnsw")
                },
                Vectorizers =
                {
                    new AzureOpenAIVectorizer("myOpenAI")
                    {
                        Parameters = vectorizerParams
                    }
                }
            };

            var contentField = config.Fields.FirstOrDefault(f =>
                f.Name.Equals("Contenido", StringComparison.OrdinalIgnoreCase) ||
                f.Name.Equals("Content", StringComparison.OrdinalIgnoreCase));

            contentField ??= config.Fields.FirstOrDefault(f =>
                f.IsSearchable && !f.IsKey &&
                !f.Name.Contains("Titulo", StringComparison.OrdinalIgnoreCase) &&
                !f.Name.Contains("Title", StringComparison.OrdinalIgnoreCase));

            var titleField = config.Fields.FirstOrDefault(f =>
                f.Name.Contains("Titulo", StringComparison.OrdinalIgnoreCase) ||
                f.Name.Contains("Title", StringComparison.OrdinalIgnoreCase));

            var prioritizedFields = new SemanticPrioritizedFields();
            if (titleField != null)
            {
                prioritizedFields.TitleField = new SemanticField(titleField.Name);
            }
            if (contentField != null)
            {
                prioritizedFields.ContentFields.Add(new SemanticField(contentField.Name));
            }

            var semanticConfig = new SemanticConfiguration(
                name: config.SemanticConfigName,
                prioritizedFields: prioritizedFields
            );

            var semanticSearch = new SemanticSearch();
            semanticSearch.Configurations.Add(semanticConfig);

            var index = new SearchIndex(config.IndexName)
            {
                Fields = fields,
                VectorSearch = vectorSearch,
                SemanticSearch = semanticSearch
            };

            // Try to get the existing index first to preserve field definitions
            try
            {
                var existingIndex = await _indexClient.GetIndexAsync(config.IndexName, cancellationToken);
                if (existingIndex?.Value != null)
                {
                    // Preserve existing fields, only update vectorSearch and semanticSearch
                    existingIndex.Value.VectorSearch = vectorSearch;
                    existingIndex.Value.SemanticSearch = semanticSearch;
                    await _indexClient.CreateOrUpdateIndexAsync(existingIndex.Value, allowIndexDowntime: true, cancellationToken: cancellationToken);
                    _logger.LogInformation("Index '{IndexName}' updated successfully (preserved existing fields)", config.IndexName);
                    return true;
                }
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                // Index doesn't exist, create it fresh
                _logger.LogInformation("Index '{IndexName}' not found, creating new index", config.IndexName);
            }

            await _indexClient.CreateOrUpdateIndexAsync(index, allowIndexDowntime: true, cancellationToken: cancellationToken);
            _logger.LogInformation("Index '{IndexName}' created successfully", config.IndexName);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating or updating index {IndexName}", config.IndexName);
            return false;
        }
    }

    public async Task<bool> CreateOrUpdateSkillsetAsync(string indexName, CancellationToken cancellationToken = default)
    {
        try
        {
            var skillsetName = $"{indexName}-skillset";
            var openAiEndpoint = _configuration["AzureOpenAI:Endpoint"];
            var embeddingsDeployment = _configuration["AzureOpenAI:EmbeddingsDeploymentName"]
                ?? _configuration["AzureOpenAI:EmbeddingDeployment"]
                ?? "text-embedding-3-large";
            var aiServicesKey = _configuration["AzureAI:ServicesKey"];

            // OCR Skill
            var ocrSkill = new OcrSkill(
                inputs: new List<InputFieldMappingEntry> { new InputFieldMappingEntry("image") { Source = "/document/normalized_images/*" } },
                outputs: new List<OutputFieldMappingEntry> { new OutputFieldMappingEntry("text") { TargetName = "ocr_text" } }
            )
            {
                Description = "Extract text from scanned images",
                Context = "/document/normalized_images/*",
                DefaultLanguageCode = OcrSkillLanguage.Es
            };

            // Merge Skill
            var mergeSkill = new MergeSkill(
                inputs: new List<InputFieldMappingEntry>
                {
                    new InputFieldMappingEntry("text") { Source = "/document/content" },
                    new InputFieldMappingEntry("itemsToInsert") { Source = "/document/normalized_images/*/ocr_text" }
                },
                outputs: new List<OutputFieldMappingEntry> { new OutputFieldMappingEntry("mergedText") { TargetName = "merged_text" } }
            )
            {
                Description = "Merge native text with OCR text",
                Context = "/document",
                InsertPreTag = "\n",
                InsertPostTag = "\n"
            };

            // Split Skill
            var splitSkill = new SplitSkill(
                inputs: new List<InputFieldMappingEntry> { new InputFieldMappingEntry("text") { Source = "/document/merged_text" } },
                outputs: new List<OutputFieldMappingEntry> { new OutputFieldMappingEntry("textItems") { TargetName = "pages" } }
            )
            {
                Description = "Split merged text into chunks",
                TextSplitMode = TextSplitMode.Pages,
                Context = "/document",
                MaximumPageLength = 2000,
                PageOverlapLength = 500
            };

            // Embedding Skill
            var embeddingSkill = new AzureOpenAIEmbeddingSkill(
                inputs: new List<InputFieldMappingEntry> { new InputFieldMappingEntry("text") { Source = "/document/pages/*" } },
                outputs: new List<OutputFieldMappingEntry> { new OutputFieldMappingEntry("embedding") { TargetName = "vector" } }
            )
            {
                Description = "Generate embeddings via Azure OpenAI",
                Context = "/document/pages/*",
                ResourceUri = new Uri(openAiEndpoint!),
                DeploymentName = embeddingsDeployment,
                ModelName = embeddingsDeployment,
                Dimensions = 1024
            };

            // Index projections
            var selectors = new List<SearchIndexerIndexProjectionSelector>
            {
                new SearchIndexerIndexProjectionSelector(
                    targetIndexName: indexName,
                    parentKeyFieldName: "ChunkId",
                    sourceContext: "/document/pages/*",
                    mappings: new List<InputFieldMappingEntry>
                    {
                        new InputFieldMappingEntry("Contenido") { Source = "/document/pages/*" },
                        new InputFieldMappingEntry("Vector") { Source = "/document/pages/*/vector" },
                        new InputFieldMappingEntry("ChunkIndex") { Source = "/document/pages/*/@index" },
                        new InputFieldMappingEntry("Titulo") { Source = "/document/metadata_storage_name" },
                        new InputFieldMappingEntry("FechaCreacion") { Source = "/document/metadata_storage_last_modified" },
                        new InputFieldMappingEntry("ClientId") { Source = "/document/ClientId" },
                        new InputFieldMappingEntry("ExpedienteId") { Source = "/document/ExpedienteId" },
                        new InputFieldMappingEntry("DocumentoId") { Source = "/document/DocumentoId" },
                        new InputFieldMappingEntry("TipoDocumento") { Source = "/document/TipoDocumento" }
                    }
                )
            };

            var projectionParameters = new SearchIndexerIndexProjectionsParameters
            {
                ProjectionMode = IndexProjectionMode.SkipIndexingParentDocuments
            };

            var indexProjection = new SearchIndexerIndexProjection(selectors)
            {
                Parameters = projectionParameters
            };

            var cognitiveServicesAccount = !string.IsNullOrEmpty(aiServicesKey)
                ? new CognitiveServicesAccountKey(aiServicesKey)
                : null;

            var skills = new List<SearchIndexerSkill> { ocrSkill, mergeSkill, splitSkill, embeddingSkill };
            var skillset = new SearchIndexerSkillset(
                name: skillsetName,
                skills: skills
            )
            {
                Description = "Skillset for document processing with OCR, chunking, and embeddings",
                CognitiveServicesAccount = cognitiveServicesAccount,
                IndexProjection = indexProjection
            };

            await _indexerClient.CreateOrUpdateSkillsetAsync(skillset, cancellationToken: cancellationToken);
            _logger.LogInformation("Skillset '{SkillsetName}' created or updated successfully", skillsetName);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating or updating skillset for index {IndexName}", indexName);
            return false;
        }
    }

    public async Task<bool> CreateOrUpdateIndexerAsync(string indexName, CancellationToken cancellationToken = default)
    {
        try
        {
            var indexerName = $"{indexName}-indexer";
            var skillsetName = $"{indexName}-skillset";
            var dataSourceName = $"{indexName}-datasource";
            var storageAccountName = _configuration["AzureStorage:AccountName"];
            var containerName = _configuration["AzureStorage:ContainerName"];
            var subscriptionId = _configuration["Azure:SubscriptionId"];
            var resourceGroup = _configuration["Azure:ResourceGroup"];

            var dataSourceConnectionString = $"ResourceId=/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Storage/storageAccounts/{storageAccountName}";

            // Create data source
            var dataSource = new SearchIndexerDataSourceConnection(
                name: dataSourceName,
                type: SearchIndexerDataSourceType.AzureBlob,
                connectionString: dataSourceConnectionString,
                container: new SearchIndexerDataContainer(containerName!)
            );

            await _indexerClient.CreateOrUpdateDataSourceConnectionAsync(dataSource, cancellationToken: cancellationToken);
            _logger.LogInformation("Data source '{DataSourceName}' created or updated successfully", dataSourceName);

            // Create indexer
            var schedule = new IndexingSchedule(TimeSpan.FromDays(1));
            var parameters = new IndexingParameters
            {
                Configuration =
                {
                    { "dataToExtract", "contentAndMetadata" },
                    { "parsingMode", "default" },
                    { "imageAction", "generateNormalizedImagePerPage" }
                }
            };

            var indexer = new SearchIndexer(
                name: indexerName,
                dataSourceName: dataSourceName,
                targetIndexName: indexName)
            {
                Description = "Indexer to process documents with OCR and embeddings",
                SkillsetName = skillsetName,
                Schedule = schedule,
                Parameters = parameters,
                FieldMappings =
                {
                    new FieldMapping("ClientId") { TargetFieldName = "ClientId" },
                    new FieldMapping("ExpedienteId") { TargetFieldName = "ExpedienteId" },
                    new FieldMapping("DocumentoId") { TargetFieldName = "DocumentoId" },
                    new FieldMapping("TipoDocumento") { TargetFieldName = "TipoDocumento" }
                }
            };

            await _indexerClient.CreateOrUpdateIndexerAsync(indexer, cancellationToken: cancellationToken);
            _logger.LogInformation("Indexer '{IndexerName}' created or updated successfully", indexerName);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating or updating indexer for index {IndexName}", indexName);
            return false;
        }
    }

    public async Task<bool> RunIndexerAsync(string indexName, CancellationToken cancellationToken = default)
    {
        try
        {
            var indexerName = $"{indexName}-indexer";
            await _indexerClient.RunIndexerAsync(indexerName, cancellationToken: cancellationToken);
            _logger.LogInformation("Indexer '{IndexerName}' run triggered successfully", indexerName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running indexer for index {IndexName}", indexName);
            return false;
        }
    }

    public async Task<bool> DeleteDocumentFromIndexAsync(string fileName, string indexName, CancellationToken cancellationToken = default)
    {
        try
        {
            var searchClient = _indexClient.GetSearchClient(indexName);

            var searchOptions = new SearchOptions
            {
                Filter = $"Titulo eq '{fileName.Replace("'", "''")}' and ClientId eq '{_clientId}'",
                Select = { "Id" },
                Size = 1000
            };

            var searchResults = await searchClient.SearchAsync<SearchDocument>("*", searchOptions, cancellationToken);
            var documentsToDelete = new List<string>();

            await foreach (var result in searchResults.Value.GetResultsAsync())
            {
                if (result.Document.TryGetValue("Id", out var id))
                {
                    documentsToDelete.Add(id.ToString()!);
                }
            }

            if (documentsToDelete.Count == 0)
            {
                _logger.LogWarning("No documents found in index '{IndexName}' for file '{FileName}'", indexName, fileName);
                return true;
            }

            var batch = IndexDocumentsBatch.Delete("Id", documentsToDelete);
            await searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);

            _logger.LogInformation("Deleted {Count} chunks from index '{IndexName}' for file '{FileName}'",
                documentsToDelete.Count, indexName, fileName);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document '{FileName}' from index '{IndexName}'", fileName, indexName);
            return false;
        }
    }

    private static SearchFieldDataType GetSearchFieldDataType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "string" => SearchFieldDataType.String,
            "int32" => SearchFieldDataType.Int32,
            "int64" => SearchFieldDataType.Int64,
            "double" => SearchFieldDataType.Double,
            "boolean" => SearchFieldDataType.Boolean,
            "datetimeoffset" => SearchFieldDataType.DateTimeOffset,
            _ => SearchFieldDataType.String
        };
    }
}
