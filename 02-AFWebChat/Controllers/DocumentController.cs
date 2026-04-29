using AFWebChat.Models;
using AFWebChat.Services;
using Microsoft.AspNetCore.Mvc;

namespace AFWebChat.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly IDocumentIndexingService _indexingService;
    private readonly ILogger<DocumentController> _logger;
    private readonly string _clientId;
    private readonly string _indexName;

    public DocumentController(
        IBlobStorageService blobStorageService,
        IDocumentIndexingService indexingService,
        ILogger<DocumentController> logger,
        IConfiguration configuration)
    {
        _blobStorageService = blobStorageService;
        _indexingService = indexingService;
        _logger = logger;
        _clientId = configuration["Tenant:ClientId"] ?? "default";
        _indexName = configuration["AzureSearch:SkillIndex:IndexName"]
            ?? configuration["AzureSearch:IndexName"]
            ?? "skill";
    }

    [HttpGet("client-info")]
    public IActionResult GetClientInfo()
    {
        return Ok(new { clientId = _clientId, indexName = _indexName });
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadDocument(
        [FromForm] IFormFile file,
        [FromForm] string? expedienteId,
        [FromForm] string? documentoId,
        [FromForm] string? tipoDocumento,
        CancellationToken cancellationToken)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded" });

            const long maxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
            if (file.Length > maxFileSizeBytes)
                return BadRequest(new { error = $"File size ({file.Length / (1024 * 1024.0):F1} MB) exceeds the 50 MB limit" });

            var allowedExtensions = new[] { ".pdf", ".docx", ".txt" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { error = $"File type '{extension}' not allowed. Allowed types: {string.Join(", ", allowedExtensions)}" });

            var finalDocumentoId = string.IsNullOrWhiteSpace(documentoId)
                ? $"DOC_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}"
                : documentoId;

            var metadata = new Dictionary<string, string>
            {
                { "ExpedienteId", expedienteId ?? "UNKNOWN" },
                { "DocumentoId", finalDocumentoId },
                { "TipoDocumento", tipoDocumento ?? "general" },
                { "ClientId", _clientId }
            };

            using var stream = file.OpenReadStream();
            var blobUri = await _blobStorageService.UploadDocumentAsync(stream, file.FileName, metadata, cancellationToken);

            var indexerTriggered = await _indexingService.RunIndexerAsync(_indexName, cancellationToken);

            _logger.LogInformation("Document {FileName} uploaded successfully with metadata: {Metadata}",
                file.FileName, string.Join(", ", metadata.Select(m => $"{m.Key}={m.Value}")));

            return Ok(new
            {
                success = true,
                fileName = file.FileName,
                blobUri,
                metadata,
                indexerTriggered,
                message = "Documento subido exitosamente. La indexación se iniciará automáticamente."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("list")]
    public async Task<IActionResult> ListDocuments(CancellationToken cancellationToken)
    {
        try
        {
            var documents = await _blobStorageService.ListDocumentsWithMetadataAsync(cancellationToken);
            return Ok(new { success = true, clientId = _clientId, documents });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("{fileName}")]
    public async Task<IActionResult> DeleteDocument(string fileName, CancellationToken cancellationToken)
    {
        try
        {
            var deletedFromIndex = await _indexingService.DeleteDocumentFromIndexAsync(fileName, _indexName, cancellationToken);
            await _blobStorageService.DeleteDocumentAsync(fileName, cancellationToken);

            _logger.LogInformation("Document {FileName} deleted from blob storage and index", fileName);
            return Ok(new { success = true, message = $"Document {fileName} deleted successfully", deletedFromIndex });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {FileName}", fileName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("setup-index")]
    public async Task<IActionResult> SetupIndex(CancellationToken cancellationToken)
    {
        try
        {
            var indexConfig = new IndexConfiguration
            {
                IndexName = _indexName,
                SemanticConfigName = $"{_indexName}-semantic-config",
                VectorProfileName = $"{_indexName}-vector-profile",
                VectorDimensions = 1024,
                Fields = new List<IndexFieldDefinition>
                {
                    new() { Name = "Id", Type = "string", IsKey = true, IsFilterable = true },
                    new() { Name = "ClientId", Type = "string", IsFilterable = true },
                    new() { Name = "ExpedienteId", Type = "string", IsFilterable = true },
                    new() { Name = "DocumentoId", Type = "string", IsFilterable = true },
                    new() { Name = "ChunkId", Type = "string", IsFilterable = true },
                    new() { Name = "ChunkIndex", Type = "int32", IsFilterable = true, IsSortable = true },
                    new() { Name = "TipoDocumento", Type = "string", IsFilterable = true },
                    new() { Name = "Titulo", Type = "string", IsFilterable = true, IsSearchable = true },
                    new() { Name = "FechaCreacion", Type = "datetimeoffset", IsFilterable = true, IsSortable = true },
                    new() { Name = "Contenido", Type = "string", IsSearchable = true },
                    new() { Name = "Vector", Type = "vector", IsVector = true }
                }
            };

            var indexCreated = await _indexingService.CreateOrUpdateIndexAsync(indexConfig, cancellationToken);
            if (!indexCreated)
                return StatusCode(500, new { error = "Failed to create index" });

            var skillsetCreated = await _indexingService.CreateOrUpdateSkillsetAsync(indexConfig.IndexName, cancellationToken);
            if (!skillsetCreated)
                return StatusCode(500, new { error = "Failed to create skillset" });

            var indexerCreated = await _indexingService.CreateOrUpdateIndexerAsync(indexConfig.IndexName, cancellationToken);
            if (!indexerCreated)
                return StatusCode(500, new { error = "Failed to create indexer" });

            _logger.LogInformation("Index setup completed successfully");
            return Ok(new { success = true, message = "Índice configurado exitosamente con skillset e indexer" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up index");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("run-indexer")]
    public async Task<IActionResult> RunIndexer(CancellationToken cancellationToken)
    {
        try
        {
            var indexerTriggered = await _indexingService.RunIndexerAsync(_indexName, cancellationToken);
            if (!indexerTriggered)
                return StatusCode(500, new { error = "Failed to trigger indexer" });

            _logger.LogInformation("Indexer for index '{IndexName}' triggered manually", _indexName);
            return Ok(new { success = true, message = "Indexer ejecutado manualmente. Los documentos serán procesados en breve." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running indexer manually");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
