using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;

#pragma warning disable OPENAI001 // Responses reasoning APIs are experimental in this SDK.

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

    /// <summary>
    /// Cliente por defecto para TODOS los agentes, orquestaciones y workflows.
    /// Por defecto usa la Responses API con razonamiento habilitado (nivel global
    /// configurable en <c>AzureOpenAI:ReasoningEffort</c>). Se puede desactivar
    /// globalmente con <c>AzureOpenAI:UseResponsesApi = false</c>.
    ///
    /// Para forzar el Chat Completions clásico en un agente concreto (demo), usa
    /// <see cref="CreateChatCompletionsClient"/> directamente.
    /// </summary>
    public IChatClient CreateChatClient(string? deployment = null)
    {
        return UseResponsesApi
            ? CreateReasoningChatClient(deployment)
            : CreateChatCompletionsClient(deployment);
    }

    /// <summary>
    /// Cliente clásico basado en la <b>Chat Completions API</b> (sin razonamiento expuesto).
    /// Reservado para el agente de demostración que debe seguir en Chat Completions.
    /// </summary>
    public IChatClient CreateChatCompletionsClient(string? deployment = null)
    {
        return CreateAzureOpenAIChatClient(deployment).AsIChatClient();
    }

    /// <summary>Interruptor global: true = Responses+razonamiento (default); false = Chat Completions.</summary>
    private bool UseResponsesApi =>
        !bool.TryParse(_config["AzureOpenAI:UseResponsesApi"], out var value) || value;


    /// <summary>
    /// Crea un <see cref="IChatClient"/> basado en la Responses API con el resumen de
    /// razonamiento habilitado. Úsalo con modelos de razonamiento (serie GPT-5, o-series)
    /// para que la UI muestre el bloque "Pensando…" al estilo GitHub Copilot.
    ///
    /// El resumen de razonamiento NO está disponible vía Chat Completions (los tokens
    /// de razonamiento están ocultos); solo se expone por la Responses API.
    /// </summary>
    public IChatClient CreateReasoningChatClient(string? deployment = null)
    {
        deployment ??= _config["AzureOpenAI:ChatDeployment"] ?? "gpt-4o";

        var effort = ParseEffort(_config["AzureOpenAI:ReasoningEffort"]);
        var summary = ParseSummary(_config["AzureOpenAI:ReasoningSummary"]);

        _logger.LogInformation(
            "Created Azure OpenAI ResponsesClient (reasoning) for deployment: {Deployment} (effort={Effort}, summary={Summary})",
            deployment, effort, summary);

        var responsesClient = _azureClient.Value.GetResponsesClient();
        var innerClient = responsesClient.AsIChatClient(deployment);
        return new ReasoningChatClient(innerClient, effort, summary);
    }

    private static ResponseReasoningEffortLevel ParseEffort(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "none" => ResponseReasoningEffortLevel.None,
        "minimal" => ResponseReasoningEffortLevel.Minimal,
        "low" => ResponseReasoningEffortLevel.Low,
        "high" => ResponseReasoningEffortLevel.High,
        _ => ResponseReasoningEffortLevel.Medium,
    };

    private static ResponseReasoningSummaryVerbosity ParseSummary(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "concise" => ResponseReasoningSummaryVerbosity.Concise,
        "detailed" => ResponseReasoningSummaryVerbosity.Detailed,
        _ => ResponseReasoningSummaryVerbosity.Auto,
    };

    public OpenAI.Embeddings.EmbeddingClient CreateEmbeddingClient()
    {
        var deployment = _config["AzureOpenAI:EmbeddingDeployment"] ?? "text-embedding-3-large";
        return _azureClient.Value.GetEmbeddingClient(deployment);
    }
}
