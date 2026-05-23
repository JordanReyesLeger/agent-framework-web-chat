using System.Text.Json;
using AFWebChat.Services;
using Microsoft.AspNetCore.Mvc;

namespace AFWebChat.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VoiceConversationController : ControllerBase
{
    private readonly VoiceConversationService _voiceService;
    private readonly ILogger<VoiceConversationController> _logger;

    public VoiceConversationController(
        VoiceConversationService voiceService,
        ILogger<VoiceConversationController> logger)
    {
        _voiceService = voiceService;
        _logger = logger;
    }

    /// <summary>
    /// Returns a temporary speech token + region for the browser SDK.
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
    /// Returns the speech configuration (language, voice, region).
    /// </summary>
    [HttpGet("speech-config")]
    public IActionResult GetSpeechConfig()
    {
        var config = _voiceService.GetSpeechConfig();
        return Ok(new
        {
            region = config.Region,
            recognitionLanguage = config.RecognitionLanguage,
            synthesisVoiceName = config.SynthesisVoiceName
        });
    }

    /// <summary>
    /// Process a user message and return the AI response.
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

    /// <summary>
    /// Get the full transcript for a session.
    /// </summary>
    [HttpGet("transcript/{sessionId}")]
    public IActionResult GetTranscript(string sessionId)
    {
        var transcript = _voiceService.GetTranscript(sessionId);
        return Ok(new { sessionId, entries = transcript });
    }

    /// <summary>
    /// Delete a voice session.
    /// </summary>
    [HttpDelete("session/{sessionId}")]
    public IActionResult DeleteSession(string sessionId)
    {
        var deleted = _voiceService.DeleteSession(sessionId);
        return Ok(new { deleted });
    }
}

public class VoiceMessageRequest
{
    public string? SessionId { get; set; }
    public string Text { get; set; } = "";
    public string? SystemPrompt { get; set; }
}
