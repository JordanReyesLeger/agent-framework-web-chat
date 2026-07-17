using Microsoft.Extensions.AI;
using OpenAI.Responses;

#pragma warning disable OPENAI001 // Responses reasoning APIs are experimental in this SDK.

namespace AFWebChat.Services;

/// <summary>
/// Envuelve un <see cref="IChatClient"/> basado en la Responses API de OpenAI para
/// solicitar el resumen de razonamiento ("chain-of-thought summary") de los modelos
/// de razonamiento (serie GPT-5, o-series). El resumen llega como
/// <see cref="TextReasoningContent"/> en el streaming y se muestra en la UI.
///
/// Nota importante: en Chat Completions los tokens de razonamiento están ocultos.
/// El resumen SOLO se expone a través de la Responses API con
/// <c>reasoning.summary</c>. Además, gpt-5.1 usa <c>reasoning_effort = none</c> por
/// defecto, por lo que hay que fijar un nivel de esfuerzo explícito para que razone.
/// </summary>
public sealed class ReasoningChatClient : DelegatingChatClient
{
    private readonly ResponseReasoningEffortLevel _effort;
    private readonly ResponseReasoningSummaryVerbosity _summary;

    public ReasoningChatClient(
        IChatClient innerClient,
        ResponseReasoningEffortLevel effort,
        ResponseReasoningSummaryVerbosity summary)
        : base(innerClient)
    {
        _effort = effort;
        _summary = summary;
    }

    private ChatOptions ApplyReasoning(ChatOptions? options)
    {
        options = options?.Clone() ?? new ChatOptions();

        var previousFactory = options.RawRepresentationFactory;
        options.RawRepresentationFactory = client =>
        {
            // Respeta cualquier configuración nativa previa; solo añade razonamiento.
            var raw = previousFactory?.Invoke(client);
            if (raw is not CreateResponseOptions createOptions)
            {
                createOptions = new CreateResponseOptions();
            }

            createOptions.ReasoningOptions ??= new ResponseReasoningOptions();
            createOptions.ReasoningOptions.ReasoningEffortLevel ??= _effort;
            createOptions.ReasoningOptions.ReasoningSummaryVerbosity ??= _summary;

            return createOptions;
        };

        return options;
    }

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => base.GetResponseAsync(messages, ApplyReasoning(options), cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => base.GetStreamingResponseAsync(messages, ApplyReasoning(options), cancellationToken);
}
