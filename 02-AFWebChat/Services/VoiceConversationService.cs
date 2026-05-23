using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OpenAI.Chat;

namespace AFWebChat.Services;

public class VoiceConversationService
{
    private readonly ChatClientFactory _chatClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<VoiceConversationService> _logger;
    private readonly ConcurrentDictionary<string, VoiceSession> _sessions = new();

    public VoiceConversationService(
        ChatClientFactory chatClientFactory,
        IConfiguration config,
        ILogger<VoiceConversationService> logger)
    {
        _chatClientFactory = chatClientFactory;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Gets a temporary authorization token for the Azure Speech SDK in the browser.
    /// This avoids exposing the subscription key in the frontend.
    /// </summary>
    public async Task<SpeechTokenResult> GetSpeechTokenAsync()
    {
        var subscriptionKey = _config["AzureSpeech:SubscriptionKey"];
        var region = _config["AzureSpeech:Region"] ?? "eastus2";

        if (string.IsNullOrEmpty(subscriptionKey))
        {
            _logger.LogWarning("AzureSpeech:SubscriptionKey not configured.");
            return new SpeechTokenResult(null, region, "Speech key not configured.");
        }

        var tokenUrl = $"https://{region}.api.cognitive.microsoft.com/sts/v1.0/issueToken";

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

        try
        {
            var response = await httpClient.PostAsync(tokenUrl, new StringContent(""));
            response.EnsureSuccessStatusCode();
            var token = await response.Content.ReadAsStringAsync();
            return new SpeechTokenResult(token, region, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to obtain speech token.");
            return new SpeechTokenResult(null, region, "Failed to obtain speech token.");
        }
    }

    /// <summary>
    /// Process user message through Azure OpenAI and return the agent's text response.
    /// </summary>
    public async Task<VoiceChatResponse> ProcessMessageAsync(string sessionId, string userText, string? systemPrompt)
    {
        var session = _sessions.GetOrAdd(sessionId, _ => new VoiceSession());

        // Add user message to transcript
        session.Transcript.Add(new TranscriptEntry("user", userText, DateTime.UtcNow));

        // Build chat messages
        var messages = new List<ChatMessage>();

        if (!string.IsNullOrEmpty(systemPrompt) && !session.SystemPromptSet)
        {
            session.SystemPrompt = systemPrompt;
            session.SystemPromptSet = true;
        }

        if (!string.IsNullOrEmpty(session.SystemPrompt))
        {
            messages.Add(ChatMessage.CreateSystemMessage(session.SystemPrompt));
        }

        // Add conversation history (last 20 turns to control context size)
        var history = session.Transcript.TakeLast(20).ToList();
        foreach (var entry in history)
        {
            if (entry.Role == "user")
                messages.Add(ChatMessage.CreateUserMessage(entry.Text));
            else if (entry.Role == "assistant")
                messages.Add(ChatMessage.CreateAssistantMessage(entry.Text));
        }

        try
        {
            var chatClient = _chatClientFactory.CreateAzureOpenAIChatClient();
            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 300, // Keep responses short for voice
                Temperature = 0.7f,
            };

            var completion = await chatClient.CompleteChatAsync(messages, options);
            var responseText = completion.Value.Content[0].Text;

            // Add assistant message to transcript
            session.Transcript.Add(new TranscriptEntry("assistant", responseText, DateTime.UtcNow));

            return new VoiceChatResponse(responseText, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing voice message for session {SessionId}", sessionId);
            return new VoiceChatResponse(null, "Error processing your message. Please try again.");
        }
    }

    /// <summary>
    /// Get the full transcript for a session.
    /// </summary>
    public List<TranscriptEntry> GetTranscript(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
            return session.Transcript.ToList();
        return new List<TranscriptEntry>();
    }

    /// <summary>
    /// Delete a voice session.
    /// </summary>
    public bool DeleteSession(string sessionId)
    {
        return _sessions.TryRemove(sessionId, out _);
    }

    /// <summary>
    /// Get speech configuration from appsettings.
    /// </summary>
    public SpeechConfigResult GetSpeechConfig()
    {
        return new SpeechConfigResult(
            _config["AzureSpeech:Region"] ?? "eastus2",
            _config["AzureSpeech:RecognitionLanguage"] ?? "es-MX",
            _config["AzureSpeech:SynthesisVoiceName"] ?? "es-MX-DaliaNeural"
        );
    }

    private class VoiceSession
    {
        public List<TranscriptEntry> Transcript { get; } = new();
        public string? SystemPrompt { get; set; }
        public bool SystemPromptSet { get; set; }
    }
}

public record SpeechTokenResult(string? Token, string Region, string? Error);
public record SpeechConfigResult(string Region, string RecognitionLanguage, string SynthesisVoiceName);
public record VoiceChatResponse(string? Text, string? Error);
public record TranscriptEntry(string Role, string Text, DateTime Timestamp);
