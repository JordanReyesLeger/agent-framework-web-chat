namespace AFWebChat.Models;

/// <summary>
/// Request payload posted by the Live Avatar UI to drive the conversation:
/// the user's transcribed text plus an optional system prompt and session id
/// used to keep per-conversation transcript context server-side.
/// </summary>
public class VoiceMessageRequest
{
    public string Text { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
    public string? SessionId { get; set; }
}
