using AFWebChat.Services;
using Microsoft.AspNetCore.Mvc;

namespace AFWebChat.Controllers;

/// <summary>
/// API for the Live Voice + Live Avatar experience (Azure Speech real-time avatar via WebRTC).
/// Reuses VoiceConversationService for LLM responses and speech token issuance,
/// adds the ICE relay token required to negotiate the WebRTC peer connection to
/// the Azure TTS Avatar service.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LiveAvatarController : ControllerBase
{
    private readonly VoiceConversationService _voiceService;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LiveAvatarController> _logger;

    public LiveAvatarController(
        VoiceConversationService voiceService,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<LiveAvatarController> logger)
    {
        _voiceService = voiceService;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Issues a short-lived Azure Speech authorization token for the browser SDK.
    /// </summary>
    [HttpGet("speech-token")]
    public async Task<IActionResult> GetSpeechToken()
    {
        var result = await _voiceService.GetSpeechTokenAsync();
        if (result.Error != null)
            return StatusCode(500, new { error = result.Error });

        return Ok(new { token = result.Token, region = result.Region });
    }

    /// <summary>
    /// Returns the Live Avatar runtime configuration (region, recognition language,
    /// voice, default avatar character & style).
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var speech = _voiceService.GetSpeechConfig();
        return Ok(new
        {
            region = speech.Region,
            recognitionLanguage = speech.RecognitionLanguage,
            synthesisVoiceName = speech.SynthesisVoiceName,
            avatarCharacter = _config["AzureSpeech:AvatarCharacter"] ?? "lisa",
            avatarStyle = _config["AzureSpeech:AvatarStyle"] ?? "casual-sitting",
            avatarVideoCodec = _config["AzureSpeech:AvatarVideoCodec"] ?? "H264",
            transparentBackground = bool.TryParse(_config["AzureSpeech:AvatarTransparentBackground"], out var tb) && tb
        });
    }

    /// <summary>
    /// Fetches the WebRTC ICE relay token from the Azure Speech avatar relay endpoint.
    /// The browser uses these ICE servers to establish a peer connection with the
    /// avatar synthesis service.
    /// </summary>
    [HttpGet("ice-token")]
    public async Task<IActionResult> GetIceToken()
    {
        var subscriptionKey = _config["AzureSpeech:SubscriptionKey"];
        var region = _config["AzureSpeech:Region"] ?? "eastus2";

        if (string.IsNullOrEmpty(subscriptionKey))
            return StatusCode(500, new { error = "AzureSpeech:SubscriptionKey not configured." });

        var url = $"https://{region}.tts.speech.microsoft.com/cognitiveservices/avatar/relay/token/v1";
        try
        {
            var http = _httpClientFactory.CreateClient();
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            var resp = await http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Avatar relay token request failed: {Status} {Body}", resp.StatusCode, body);
                return StatusCode((int)resp.StatusCode, new { error = "Failed to obtain ICE relay token.", details = body });
            }
            return Content(body, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching avatar ICE relay token.");
            return StatusCode(500, new { error = "Error fetching ICE relay token." });
        }
    }

    /// <summary>
    /// Sends a user message to the LLM and returns the assistant text response.
    /// The browser then drives the avatar to speak this text via the Speech SDK.
    /// </summary>
    [HttpPost("message")]
    public async Task<IActionResult> PostMessage([FromBody] VoiceMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "Message text is required." });

        if (request.Text.Length > 2048)
            return BadRequest(new { error = "Message too long. Maximum 2KB." });

        var sessionId = request.SessionId ?? Guid.NewGuid().ToString();
        var response = await _voiceService.ProcessMessageAsync(sessionId, request.Text, request.SystemPrompt);

        if (response.Error != null)
            return StatusCode(500, new { error = response.Error, sessionId });

        return Ok(new { text = response.Text, sessionId });
    }

    [HttpGet("transcript/{sessionId}")]
    public IActionResult GetTranscript(string sessionId)
    {
        var transcript = _voiceService.GetTranscript(sessionId);
        return Ok(new { sessionId, entries = transcript });
    }

    [HttpDelete("session/{sessionId}")]
    public IActionResult DeleteSession(string sessionId)
    {
        var deleted = _voiceService.DeleteSession(sessionId);
        return Ok(new { deleted });
    }
}
