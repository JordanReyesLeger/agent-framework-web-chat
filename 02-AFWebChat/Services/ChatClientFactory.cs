using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace AFWebChat.Services;

public class ChatClientFactory
{
    private readonly IConfiguration _config;
    private readonly ILogger<ChatClientFactory> _logger;
    private readonly Lazy<DefaultAzureCredential> _credential;
    private readonly Lazy<AzureOpenAIClient> _azureClient;
    private readonly bool _usesApiKey;

    public ChatClientFactory(IConfiguration config, ILogger<ChatClientFactory> logger)
    {
        _config = config;
        _logger = logger;

        var apiKey = config["AzureOpenAI:ApiKey"];
        _usesApiKey = !string.IsNullOrEmpty(apiKey);

        _credential = new Lazy<DefaultAzureCredential>(() => new DefaultAzureCredential());
        _azureClient = new Lazy<AzureOpenAIClient>(() =>
        {
            var endpoint = _config["AzureOpenAI:Endpoint"]
                ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is not configured.");

            if (_usesApiKey)
            {
                logger.LogInformation("ChatClientFactory: Usando API Key para Azure OpenAI.");
                return new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey!));
            }

            logger.LogInformation("ChatClientFactory: Usando DefaultAzureCredential para Azure OpenAI.");
            return new AzureOpenAIClient(new Uri(endpoint), _credential.Value);
        });
    }

    /// <summary>
    /// Pre-warm the credential, token cache, and client so the first real request is fast.
    /// </summary>
    public async Task WarmUpAsync()
    {
        _logger.LogInformation("Warming up Azure OpenAI client...");
        try
        {
            if (!_usesApiKey)
            {
                var tokenRequest = new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]);
                await _credential.Value.GetTokenAsync(tokenRequest);
            }
            _ = _azureClient.Value;
            _logger.LogInformation("Azure OpenAI client warmed up successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Warmup failed (non-fatal). First request may be slower.");
        }
    }

    public ChatClient CreateAzureOpenAIChatClient(string? deployment = null)
    {
        deployment ??= _config["AzureOpenAI:ChatDeployment"] ?? "gpt-4o";
        _logger.LogInformation("Created Azure OpenAI ChatClient for deployment: {Deployment}", deployment);
        return _azureClient.Value.GetChatClient(deployment);
    }

    public IChatClient CreateChatClient(string? deployment = null)
    {
        return CreateAzureOpenAIChatClient(deployment).AsIChatClient();
    }

    public OpenAI.Embeddings.EmbeddingClient CreateEmbeddingClient()
    {
        var deployment = _config["AzureOpenAI:EmbeddingDeployment"] ?? "text-embedding-3-large";
        return _azureClient.Value.GetEmbeddingClient(deployment);
    }
}
