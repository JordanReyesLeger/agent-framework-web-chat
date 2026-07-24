using OpenAI.Responses;

#pragma warning disable OPENAI001 // Responses reasoning APIs are experimental in this SDK.

namespace AFWebChat.Services;

/// <summary>
/// Ajuste GLOBAL y mutable en tiempo de ejecución del nivel de razonamiento.
/// Lo lee <see cref="ReasoningChatClient"/> en CADA llamada, por lo que cambiarlo
/// (p. ej. desde la UI del chat) aplica al instante a todos los agentes cacheados,
/// sin reiniciar la app ni recrear los clientes.
/// </summary>
public sealed class ReasoningSettings
{
    // Valores de esfuerzo soportados por gpt-5.1 (NO incluye "minimal").
    public static readonly string[] AllowedEfforts = ["none", "low", "medium", "high"];
    public static readonly string[] AllowedSummaries = ["auto", "concise", "detailed"];

    private volatile string _effort;
    private volatile string _summary;

    public ReasoningSettings(IConfiguration config)
    {
        _effort = Normalize(config["AzureOpenAI:ReasoningEffort"], AllowedEfforts, "medium");
        _summary = Normalize(config["AzureOpenAI:ReasoningSummary"], AllowedSummaries, "auto");
    }

    /// <summary>Nivel de esfuerzo actual (none/low/medium/high).</summary>
    public string Effort
    {
        get => _effort;
        set => _effort = Normalize(value, AllowedEfforts, _effort);
    }

    /// <summary>Verbosidad del resumen actual (auto/concise/detailed).</summary>
    public string Summary
    {
        get => _summary;
        set => _summary = Normalize(value, AllowedSummaries, _summary);
    }

    public ResponseReasoningEffortLevel EffortLevel => ParseEffort(_effort);
    public ResponseReasoningSummaryVerbosity SummaryVerbosity => ParseSummary(_summary);

    private static string Normalize(string? value, string[] allowed, string fallback)
    {
        var v = value?.Trim().ToLowerInvariant();
        return !string.IsNullOrEmpty(v) && allowed.Contains(v) ? v : fallback;
    }

    public static ResponseReasoningEffortLevel ParseEffort(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "none" => ResponseReasoningEffortLevel.None,
        "low" => ResponseReasoningEffortLevel.Low,
        "high" => ResponseReasoningEffortLevel.High,
        _ => ResponseReasoningEffortLevel.Medium,
    };

    public static ResponseReasoningSummaryVerbosity ParseSummary(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "concise" => ResponseReasoningSummaryVerbosity.Concise,
        "detailed" => ResponseReasoningSummaryVerbosity.Detailed,
        _ => ResponseReasoningSummaryVerbosity.Auto,
    };
}
