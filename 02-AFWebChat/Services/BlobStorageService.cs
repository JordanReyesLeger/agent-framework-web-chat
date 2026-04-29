using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Identity;

namespace AFWebChat.Services;

public interface IBlobStorageService
{
    Task<string> UploadDocumentAsync(Stream fileStream, string fileName, Dictionary<string, string> metadata, CancellationToken cancellationToken = default);
    Task<List<string>> ListDocumentsAsync(CancellationToken cancellationToken = default);
    Task<List<BlobDocumentInfo>> ListDocumentsWithMetadataAsync(CancellationToken cancellationToken = default);
    Task DeleteDocumentAsync(string fileName, CancellationToken cancellationToken = default);
    string GetClientId();
}

public class BlobDocumentInfo
{
    public string Name { get; set; } = "";
    public long? Size { get; set; }
    public DateTimeOffset? LastModified { get; set; }
    public string? ContentType { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<BlobStorageService> _logger;
    private readonly string _clientId;

    public BlobStorageService(IConfiguration configuration, ILogger<BlobStorageService> logger)
    {
        _logger = logger;
        _clientId = configuration["Tenant:ClientId"] ?? "default";

        var storageAccountName = configuration["AzureStorage:AccountName"];
        var containerName = configuration["AzureStorage:ContainerName"];
        var useDefaultCredential = configuration.GetValue<bool>("AzureStorage:UseDefaultCredential", true);

        if (string.IsNullOrEmpty(storageAccountName) || string.IsNullOrEmpty(containerName))
        {
            throw new InvalidOperationException("Azure Storage configuration is missing. Please configure AzureStorage:AccountName and AzureStorage:ContainerName");
        }

        var blobUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");

        if (useDefaultCredential)
        {
            var tenantId = configuration["Azure:TenantId"];
            var credentialOptions = string.IsNullOrEmpty(tenantId)
                ? new DefaultAzureCredentialOptions()
                : new DefaultAzureCredentialOptions { TenantId = tenantId };
            var blobServiceClient = new BlobServiceClient(blobUri, new DefaultAzureCredential(credentialOptions));
            _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        }
        else
        {
            var connectionString = configuration["AzureStorage:ConnectionString"];
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("AzureStorage:ConnectionString is required when UseDefaultCredential is false");
            }
            var blobServiceClient = new BlobServiceClient(connectionString);
            _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        }
    }

    public async Task<string> UploadDocumentAsync(Stream fileStream, string fileName, Dictionary<string, string> metadata, CancellationToken cancellationToken = default)
    {
        try
        {
            var blobPath = $"{_clientId}/{fileName}";
            var blobClient = _containerClient.GetBlobClient(blobPath);

            var blobUploadOptions = new BlobUploadOptions
            {
                Metadata = metadata
            };

            await blobClient.UploadAsync(fileStream, blobUploadOptions, cancellationToken);

            _logger.LogInformation("Document {FileName} uploaded successfully with metadata: {Metadata}",
                fileName, string.Join(", ", metadata.Select(m => $"{m.Key}={m.Value}")));

            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document {FileName}", fileName);
            throw;
        }
    }

    public string GetClientId() => _clientId;

    public async Task<List<string>> ListDocumentsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var documents = new List<string>();
            var prefix = $"{_clientId}/";

            await foreach (var blobItem in _containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken))
            {
                var name = blobItem.Name.StartsWith(prefix) ? blobItem.Name[prefix.Length..] : blobItem.Name;
                documents.Add(name);
            }

            return documents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents");
            throw;
        }
    }

    public async Task<List<BlobDocumentInfo>> ListDocumentsWithMetadataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var documents = new List<BlobDocumentInfo>();
            var prefix = $"{_clientId}/";

            await foreach (var blobItem in _containerClient.GetBlobsAsync(BlobTraits.Metadata, BlobStates.None, prefix, cancellationToken))
            {
                var name = blobItem.Name.StartsWith(prefix) ? blobItem.Name[prefix.Length..] : blobItem.Name;
                documents.Add(new BlobDocumentInfo
                {
                    Name = name,
                    Size = blobItem.Properties?.ContentLength,
                    LastModified = blobItem.Properties?.LastModified,
                    ContentType = blobItem.Properties?.ContentType,
                    Metadata = blobItem.Metadata?.ToDictionary(m => m.Key, m => m.Value) ?? new()
                });
            }

            return documents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents with metadata");
            throw;
        }
    }

    public async Task DeleteDocumentAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var blobPath = $"{_clientId}/{fileName}";
            var blobClient = _containerClient.GetBlobClient(blobPath);
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

            _logger.LogInformation("Document {FileName} deleted successfully", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {FileName}", fileName);
            throw;
        }
    }
}
