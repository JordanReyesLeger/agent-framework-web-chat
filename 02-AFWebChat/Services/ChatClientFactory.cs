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
    private readonly ReasoningSettings _reasoning;
    private readonly Lazy<DefaultAzureCredential> _credential;
    private readonly Lazy<AzureOpenAIClient> _azureClient;
    private readonly bool _usesApiKey;

    public ChatClientFactory(IConfiguration config, ILogger<ChatClientFactory> logger, ReasoningSettings reasoning)
    {
        _config = config;
        _logger = logger;
        _reasoning = reasoning;

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
    /// Pre-warm the credential, token cache, client, HTTP/2 connection pool y el pipeline
    /// completo de streaming (JIT + serialización de la Responses API) para que la PRIMERA
    /// conversación real no pague el arranque en frío (~20 s). Se ejecuta en segundo plano
    /// al iniciar la aplicación.
    /// </summary>
    public async Task WarmUpAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Warming up Azure OpenAI client...");
        try
        {
            if (!_usesApiKey)
            {
                var tokenRequest = new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]);
                await _credential.Value.GetTokenAsync(tokenRequest);
            }
            _ = _azureClient.Value;
            _logger.LogInformation("Azure OpenAI credential/client ready ({Elapsed} ms). Warming inference pipeline...", sw.ElapsedMilliseconds);

            // Ejecuta una inferencia mínima REAL por el mismo pipeline de streaming que usa la UI.
            // Esto compila (JIT) el pipeline de Agent Framework + Responses, abre la conexión
            // HTTP/2 al endpoint y calienta el modelo de razonamiento, moviendo ese costo del
            // primer usuario al arranque en background.
            await WarmUpInferenceAsync();
            _logger.LogInformation("Azure OpenAI warmup complete ({Elapsed} ms).", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Warmup failed (non-fatal). First request may be slower.");
        }
    }

    /// <summary>
    /// Dispara una inferencia trivial por el pipeline de streaming por defecto.
    /// Usa esfuerzo de razonamiento mínimo para que el calentamiento sea barato,
    /// pero recorre exactamente las mismas rutas de código que las peticiones reales.
    /// </summary>
    private async Task WarmUpInferenceAsync()
    {
        IChatClient client = UseResponsesApi
            ? new ReasoningChatClient(
                _azureClient.Value.GetResponsesClient().AsIChatClient(
                    _config["AzureOpenAI:ChatDeployment"] ?? "gpt-4o"),
                ResponseReasoningEffortLevel.Low,
                ResponseReasoningSummaryVerbosity.Auto)
            : CreateChatCompletionsClient();

        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.User, "ping")
        };
        var options = new ChatOptions { MaxOutputTokens = 16 };

        await foreach (var _ in client.GetStreamingResponseAsync(messages, options))
        {
            // Descartamos la salida; solo nos interesa calentar el pipeline.
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

        _logger.LogInformation(
            "Created Azure OpenAI ResponsesClient (reasoning) for deployment: {Deployment} (effort={Effort}, summary={Summary}, global)",
            deployment, _reasoning.Effort, _reasoning.Summary);

        var responsesClient = _azureClient.Value.GetResponsesClient();
        var innerClient = responsesClient.AsIChatClient(deployment);
        // Lee el nivel de razonamiento GLOBAL en cada llamada → cambiar ReasoningSettings
        // desde la UI afecta al instante a todos los agentes.
        return new ReasoningChatClient(innerClient, _reasoning);
    }

    public OpenAI.Embeddings.EmbeddingClient CreateEmbeddingClient()
    {
        var deployment = _config["AzureOpenAI:EmbeddingDeployment"] ?? "text-embedding-3-large";
        return _azureClient.Value.GetEmbeddingClient(deployment);
    }
}
