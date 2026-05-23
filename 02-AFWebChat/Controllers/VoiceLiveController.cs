using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.VoiceLive;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AFWebChat.Controllers;

/// <summary>
/// Bridge controller that proxies a browser WebSocket connection to the
/// Azure VoiceLive service. The browser sends:
///   - Binary frames: raw PCM16 mono 24kHz mic audio
///   - Text frames (JSON): control messages like {"type":"configure","instructions":"...","voice":"..."}
///                         or {"type":"stop"} to cancel the current response.
///
/// The server sends back:
///   - Binary frames: raw PCM16 mono 24kHz assistant audio to play
///   - Text frames (JSON): events such as
///       {"type":"speech_started"} | {"type":"speech_stopped"}
///       {"type":"response_created"} | {"type":"response_done"}
///       {"type":"transcript_delta","text":"..."}
///       {"type":"user_transcript","text":"..."}
///       {"type":"error","message":"..."}
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VoiceLiveController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<VoiceLiveController> _logger;

    public VoiceLiveController(IConfiguration config, ILogger<VoiceLiveController> logger)
    {
        _config = config;
        _logger = logger;
    }

    [HttpGet("config")]
    public IActionResult GetConfig() => Ok(new
    {
        model = _config["VoiceLive:Model"] ?? "gpt-4o-mini-realtime-preview",
        voice = _config["VoiceLive:Voice"] ?? _config["AzureSpeech:SynthesisVoiceName"] ?? "es-MX-DaliaNeural",
        // Prompt compartido por Voice Live y Live Avatar (Prompts/voice-system-prompt.txt).
        instructions = VoicePrompt.Load(),
        sampleRate = 24000
    });

    [HttpGet("voices")]
    public IActionResult GetVoices() => Ok(new
    {
        defaultVoice = _config["VoiceLive:Voice"] ?? "en-US-Andrew3:DragonHDLatestNeural",
        groups = new object[]
        {
            new
            {
                name = "Speech-to-Speech (OpenAI)",
                description = "Voces nativas del modelo realtime — latencia más baja, audio generado directamente por la IA.",
                type = "openai",
                voices = new[]
                {
                    new { id = "alloy",   label = "Alloy (S2S)",   lang = "multi", gender = "neutral", styles = Array.Empty<string>() },
                    new { id = "ash",     label = "Ash (S2S)",     lang = "multi", gender = "male",    styles = Array.Empty<string>() },
                    new { id = "ballad",  label = "Ballad (S2S)",  lang = "multi", gender = "male",    styles = Array.Empty<string>() },
                    new { id = "coral",   label = "Coral (S2S)",   lang = "multi", gender = "female",  styles = Array.Empty<string>() },
                    new { id = "echo",    label = "Echo (S2S)",    lang = "multi", gender = "male",    styles = Array.Empty<string>() },
                    new { id = "sage",    label = "Sage (S2S)",    lang = "multi", gender = "female",  styles = Array.Empty<string>() },
                    new { id = "shimmer", label = "Shimmer (S2S)", lang = "multi", gender = "female",  styles = Array.Empty<string>() },
                    new { id = "verse",   label = "Verse (S2S)",   lang = "multi", gender = "male",    styles = Array.Empty<string>() }
                }
            },
            new
            {
                name = "Azure HD (Dragon HD Latest)",
                description = "Voces premium de alta definición. Inglés nativo, más expresivas.",
                type = "azure",
                voices = new[]
                {
                    new { id = "en-US-Andrew3:DragonHDLatestNeural", label = "Andrew 3 (HD) — más nueva", lang = "en-US", gender = "male",   styles = new[] { "chat", "professional", "friendly" } },
                    new { id = "en-US-Andrew:DragonHDLatestNeural",  label = "Andrew (HD)",            lang = "en-US", gender = "male",   styles = new[] { "chat", "professional" } },
                    new { id = "en-US-Ava3:DragonHDLatestNeural",    label = "Ava 3 (HD)",             lang = "en-US", gender = "female", styles = new[] { "chat", "professional", "friendly" } },
                    new { id = "en-US-Aria:DragonHDLatestNeural",    label = "Aria (HD)",              lang = "en-US", gender = "female", styles = new[] { "chat", "professional" } },
                    new { id = "en-US-Brian:DragonHDLatestNeural",   label = "Brian (HD)",             lang = "en-US", gender = "male",   styles = new[] { "chat", "professional" } },
                    new { id = "en-US-Emma:DragonHDLatestNeural",    label = "Emma (HD)",              lang = "en-US", gender = "female", styles = new[] { "chat", "professional" } },
                    new { id = "en-US-Jenny:DragonHDLatestNeural",   label = "Jenny (HD)",             lang = "en-US", gender = "female", styles = new[] { "chat", "customerservice", "assistant" } },
                    new { id = "en-US-Steffan:DragonHDLatestNeural", label = "Steffan (HD)",           lang = "en-US", gender = "male",   styles = new[] { "chat", "professional" } }
                }
            },
            new
            {
                name = "Multilingüe",
                description = "Voces neurales con soporte multi-idioma incluyendo español.",
                type = "azure",
                voices = new[]
                {
                    new { id = "en-US-AvaMultilingualNeural",     label = "Ava Multilingüe",     lang = "multi", gender = "female", styles = new[] { "chat", "friendly", "cheerful", "empathetic" } },
                    new { id = "en-US-AndrewMultilingualNeural",  label = "Andrew Multilingüe",  lang = "multi", gender = "male",   styles = new[] { "chat", "friendly", "professional" } },
                    new { id = "en-US-EmmaMultilingualNeural",    label = "Emma Multilingüe",    lang = "multi", gender = "female", styles = new[] { "chat", "friendly", "cheerful" } },
                    new { id = "en-US-BrianMultilingualNeural",   label = "Brian Multilingüe",   lang = "multi", gender = "male",   styles = new[] { "chat", "friendly", "professional" } }
                }
            },
            new
            {
                name = "Español",
                description = "Voces neurales nativas en español.",
                type = "azure",
                voices = new[]
                {
                    new { id = "es-MX-DaliaNeural",   label = "Dalia (México)",      lang = "es-MX", gender = "female", styles = new[] { "cheerful", "chat", "friendly", "customerservice" } },
                    new { id = "es-MX-JorgeNeural",   label = "Jorge (México)",      lang = "es-MX", gender = "male",   styles = new[] { "chat", "cheerful" } },
                    new { id = "es-ES-ElviraNeural",  label = "Elvira (España)",     lang = "es-ES", gender = "female", styles = new[] { "cheerful", "chat", "friendly" } },
                    new { id = "es-ES-AlvaroNeural",  label = "Álvaro (España)",     lang = "es-ES", gender = "male",   styles = new[] { "chat" } },
                    new { id = "es-AR-ElenaNeural",   label = "Elena (Argentina)",   lang = "es-AR", gender = "female", styles = Array.Empty<string>() },
                    new { id = "es-CO-SalomeNeural",  label = "Salomé (Colombia)",   lang = "es-CO", gender = "female", styles = Array.Empty<string>() }
                }
            }
        },
        // Avatar characters (Azure Live Avatar) supported by Voice Live.
        // Full-body 3D avatars work end-to-end via Azure.AI.VoiceLive SDK.
        // Talking Heads (photo avatars, vasa-1) are shown for UI parity with the
        // Live Avatar page, but they are NOT supported by the Voice Live SDK 1.0
        // (no PhotoAvatarBaseModel property). The client blocks Connect when a
        // photo avatar is selected and asks the user to switch to Live Avatar.
        avatar = new
        {
            defaultCharacter = _config["AzureSpeech:AvatarCharacter"] ?? "lisa",
            defaultStyle = _config["AzureSpeech:AvatarStyle"] ?? "casual-sitting",
            characters = new object[]
            {
                new { id = "lisa",   label = "Lisa",   gender = "female", type = "fullbody", styles = new[] { "casual-sitting", "graceful-sitting", "graceful-standing", "technical-sitting", "technical-standing" } },
                new { id = "meg",    label = "Meg",    gender = "female", type = "fullbody", styles = new[] { "formal", "casual", "business" } },
                new { id = "lori",   label = "Lori",   gender = "female", type = "fullbody", styles = new[] { "casual", "formal", "graceful" } },
                new { id = "max",    label = "Max",    gender = "male",   type = "fullbody", styles = new[] { "formal", "casual", "business" } },
                new { id = "harry",  label = "Harry",  gender = "male",   type = "fullbody", styles = new[] { "business", "casual", "youthful" } },
                new { id = "jeff",   label = "Jeff (retiro Dic 2026)", gender = "male", type = "fullbody", styles = new[] { "business", "formal" } },
                new { id = "rowan",  label = "Rowan",  gender = "male",   type = "fullbody", styles = Array.Empty<string>() },
                new { id = "celine", label = "Celine", gender = "female", type = "fullbody", styles = Array.Empty<string>() },
                new { id = "nia",    label = "Nia",    gender = "female", type = "fullbody", styles = Array.Empty<string>() },
                new { id = "malik",  label = "Malik",  gender = "male",   type = "fullbody", styles = Array.Empty<string>() },
                // Talking Heads (photo avatars, preview) — shown for parity, not supported by Voice Live SDK 1.0.
                new { id = "adrian",    label = "Adrian (Preview)",    gender = "male",   type = "photo", styles = Array.Empty<string>() },
                new { id = "amara",     label = "Amara (Preview)",     gender = "female", type = "photo", styles = Array.Empty<string>() },
                new { id = "amira",     label = "Amira (Preview)",     gender = "female", type = "photo", styles = Array.Empty<string>() },
                new { id = "anika",     label = "Anika (Preview)",     gender = "female", type = "photo", styles = Array.Empty<string>() },
                new { id = "bianca",    label = "Bianca (Preview)",    gender = "female", type = "photo", styles = Array.Empty<string>() },
                new { id = "camila",    label = "Camila (Preview)",    gender = "female", type = "photo", styles = Array.Empty<string>() },
                new { id = "carlos",    label = "Carlos (Preview)",    gender = "male",   type = "photo", styles = Array.Empty<string>() },
                new { id = "clara",     label = "Clara (Preview)",     gender = "female", type = "photo", styles = Array.Empty<string>() },
                new { id = "darius",    label = "Darius (Preview)",    gender = "male",   type = "photo", styles = Array.Empty<string>() },
                new { id = "diego",     label = "Diego (Preview)",     gender = "male",   type = "photo", styles = Array.Empty<string>() },
                new { id = "elise",     label = "Elise (Preview)",     gender = "female", type = "photo", styles = Array.Empty<string>() },
                new { id = "farhan",    label = "Farhan (Preview)",    gender = "male",   type = "photo", styles = Array.Empty<string>() },
                new { id = "faris",     label = "Faris (Preview)",     gender = "male",   type = "photo", styles = Array.Empty<string>() },
                new { id = "gabrielle", label = "Gabrielle (Preview)", gender = "female", type = "photo", styles = Array.Empty<string>() },
                new { id = "hyejin",    label = "Hyejin (Preview)",    gender = "female", type = "photo", styles = Array.Empty<string>() },
                new { id = "imran",     label = "Imran (Preview)",     gender = "male",   type = "photo", styles = Array.Empty<string>() },
                new { id = "isabella",  label = "Isabella (Preview)",  gender = "female", type = "photo", styles = Array.Empty<string>() },
                new { id = "layla",     label = "Layla (Preview)",     gender = "female", type = "photo", styles = Array.Empty<string>() },
                new { id = "liwei",     label = "Liwei (Preview)",     gender = "male",   type = "photo", styles = Array.Empty<string>() },
                new { id = "ling",      label = "Ling (Preview)",      gender = "female", type = "photo", styles = Array.Empty<string>() },
                new { id = "marcus",    label = "Marcus (Preview)",    gender = "male",   type = "photo", styles = Array.Empty<string>() },
                new { id = "matteo",    label = "Matteo (Preview)",    gender = "male",   type = "photo", styles = Array.Empty<string>() },
                new { id = "rahul",     label = "Rahul (Preview)",     gender = "male",   type = "photo", styles = Array.Empty<string>() },
                new { id = "rana",      label = "Rana (Preview)",      gender = "female", type = "photo", styles = Array.Empty<string>() },
                new { id = "ren",       label = "Ren (Preview)",       gender = "male",   type = "photo", styles = Array.Empty<string>() },
                new { id = "riya",      label = "Riya (Preview)",      gender = "female", type = "photo", styles = Array.Empty<string>() },
                new { id = "sakura",    label = "Sakura (Preview)",    gender = "female", type = "photo", styles = Array.Empty<string>() },
                new { id = "simone",    label = "Simone (Preview)",    gender = "female", type = "photo", styles = Array.Empty<string>() },
                new { id = "zayd",      label = "Zayd (Preview)",      gender = "male",   type = "photo", styles = Array.Empty<string>() },
                new { id = "zoe",       label = "Zoe (Preview)",       gender = "female", type = "photo", styles = Array.Empty<string>() }
            }
        }
    });

    // Names recognized as OpenAI S2S voices (from the realtime model).
    private static readonly HashSet<string> OpenAIVoiceNames =
        new(StringComparer.OrdinalIgnoreCase) { "alloy", "ash", "ballad", "coral", "echo", "sage", "shimmer", "verse" };

    private static bool IsOpenAIVoice(string voiceName, string? voiceType)
        => string.Equals(voiceType, "openai", StringComparison.OrdinalIgnoreCase)
            || (string.IsNullOrEmpty(voiceType) && OpenAIVoiceNames.Contains(voiceName));

    [HttpGet("ws")]
    public async Task ConnectWebSocket()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsync("WebSocket request expected");
            return;
        }

        using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        _logger.LogInformation("VoiceLive WS client connected from {Ip}", HttpContext.Connection.RemoteIpAddress);

        var ct = HttpContext.RequestAborted;

        VoiceLiveClient? client = null;
        VoiceLiveSession? session = null;

        // Linked CTS so we can cancel the server->client pump as soon as the client closes
        // (or the request aborts) without waiting for the next VoiceLive event.
        var pumpCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var pumpCt = pumpCts.Token;

        try
        {
            // Build VoiceLive client from config
            var endpointStr = _config["VoiceLive:Endpoint"];
            if (string.IsNullOrWhiteSpace(endpointStr))
            {
                await SendJsonAsync(socket, new { type = "error", message = "VoiceLive:Endpoint not configured in appsettings.json" }, ct);
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "no endpoint", ct);
                return;
            }

            var apiKey = _config["VoiceLive:ApiKey"];
            var endpoint = new Uri(endpointStr);

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                client = new VoiceLiveClient(endpoint, new AzureKeyCredential(apiKey), new VoiceLiveClientOptions());
                _logger.LogInformation("VoiceLive: using API key auth (endpoint={Endpoint})", endpoint);
            }
            else
            {
                client = new VoiceLiveClient(endpoint, new DefaultAzureCredential(), new VoiceLiveClientOptions());
                _logger.LogInformation("VoiceLive: using DefaultAzureCredential (endpoint={Endpoint})", endpoint);
            }

            var model = _config["VoiceLive:Model"] ?? "gpt-4o-mini-realtime-preview";
            var defaultVoice = _config["VoiceLive:Voice"] ?? _config["AzureSpeech:SynthesisVoiceName"] ?? "es-MX-DaliaNeural";
            // Lee Prompts/voice-system-prompt.txt en cada conexión — sin reiniciar el server.
            var defaultInstructions = VoicePrompt.Load();

            // Allow per-session overrides via query string
            // ?voice=...&voiceType=openai|azure&style=...&instructions=...
            // &avatar=1&avatarCharacter=lisa&avatarStyle=casual-sitting
            var voiceName = Request.Query["voice"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(voiceName)) voiceName = defaultVoice;

            var voiceType = Request.Query["voiceType"].FirstOrDefault();
            var style = Request.Query["style"].FirstOrDefault();

            var instructions = Request.Query["instructions"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(instructions)) instructions = defaultInstructions;

            var avatarEnabled = Request.Query["avatar"].FirstOrDefault() is string a
                && (a == "1" || string.Equals(a, "true", StringComparison.OrdinalIgnoreCase));
            var avatarCharacter = Request.Query["avatarCharacter"].FirstOrDefault()
                ?? _config["AzureSpeech:AvatarCharacter"] ?? "lisa";
            var avatarStyle = Request.Query["avatarStyle"].FirstOrDefault()
                ?? _config["AzureSpeech:AvatarStyle"] ?? "casual-sitting";
            var avatarPhoto = Request.Query["avatarPhoto"].FirstOrDefault() is string ap
                && (ap == "1" || string.Equals(ap, "true", StringComparison.OrdinalIgnoreCase));

            _logger.LogInformation(
                "VoiceLive: starting session model={Model} voice={Voice} type={Type} style={Style} avatar={Avatar}",
                model, voiceName, voiceType ?? "auto", style ?? "-", avatarEnabled);

            try
            {
                session = await client.StartSessionAsync(model, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VoiceLive: StartSessionAsync failed (endpoint={Endpoint}, model={Model})", endpoint, model);
                await SendJsonAsync(socket, new
                {
                    type = "error",
                    message = $"No se pudo conectar a Azure VoiceLive. Revisa VoiceLive:Endpoint, VoiceLive:ApiKey y VoiceLive:Model en appsettings. Detalle: {ex.Message}"
                }, CancellationToken.None);
                try { await socket.CloseAsync(WebSocketCloseStatus.InternalServerError, "voicelive connect failed", CancellationToken.None); } catch { }
                return;
            }

            // Build the Voice (S2S openai or Azure standard with optional style).
            VoiceProvider voiceConfig;
            if (IsOpenAIVoice(voiceName, voiceType))
            {
                voiceConfig = new OpenAIVoice(voiceName.ToLowerInvariant());
            }
            else
            {
                var azureVoice = new AzureStandardVoice(voiceName);
                if (!string.IsNullOrWhiteSpace(style)) azureVoice.Style = style;
                voiceConfig = azureVoice;
            }

            var sessionOptions = new VoiceLiveSessionOptions
            {
                Model = model,
                Instructions = instructions,
                Voice = voiceConfig,
                InputAudioFormat = InputAudioFormat.Pcm16,
                OutputAudioFormat = OutputAudioFormat.Pcm16,
                InputAudioEchoCancellation = new AudioEchoCancellation(),
                // Activa transcripción del audio del usuario para que la UI muestre lo que dijo.
                InputAudioTranscription = new AudioInputTranscriptionOptions(AudioInputTranscriptionOptionsModel.Whisper1),
                TurnDetection = new AzureSemanticVadTurnDetection
                {
                    Threshold = 0.5f,
                    PrefixPadding = TimeSpan.FromMilliseconds(300),
                    SilenceDuration = TimeSpan.FromMilliseconds(500),
                    RemoveFillerWords = true
                }
            };
            sessionOptions.Modalities.Clear();
            sessionOptions.Modalities.Add(InteractionModality.Text);
            sessionOptions.Modalities.Add(InteractionModality.Audio);

            if (avatarEnabled)
            {
                // Voice Live avatar: enable the avatar modality (mandatory) and attach
                // the requested character/style + animation outputs (visemes/blendshapes).
                sessionOptions.Modalities.Add(InteractionModality.Avatar);
                try
                {
                    // AvatarConfiguration(character, bypassAvatarTalkingOptimization)
                    var avatarCfg = new AvatarConfiguration(avatarCharacter, false);

                    if (avatarPhoto)
                    {
                        // Photo avatar (Talking Heads, vasa-1) — official support in Azure.AI.VoiceLive 1.1.0-beta.3.
                        // Photo avatars do NOT accept a style — passing one yields avatar_verification_failed.
                        // https://learn.microsoft.com/dotnet/api/azure.ai.voicelive.avatarconfiguration.model?view=azure-dotnet
                        avatarCfg.Type = AvatarConfigTypes.PhotoAvatar;
                        avatarCfg.Model = PhotoAvatarBaseModes.Vasa1;
                        _logger.LogInformation("VoiceLive: configured photo avatar (vasa-1) for character={Character} (no style)", avatarCharacter);
                    }
                    else
                    {
                        avatarCfg.Style = avatarStyle;
                    }

                    sessionOptions.Avatar = avatarCfg;
                    sessionOptions.Animation = new AnimationOptions();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Avatar configuration failed; continuing without avatar.");
                }
            }

            try
            {
                await session.ConfigureSessionAsync(sessionOptions, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VoiceLive: ConfigureSessionAsync failed (model={Model}, voice={Voice})", model, voiceName);
                await SendJsonAsync(socket, new
                {
                    type = "error",
                    message = $"Azure VoiceLive cerró la conexión durante la configuración. Verifica que el modelo '{model}' y la voz '{voiceName}' sean válidos para ese endpoint. Detalle: {ex.Message}"
                }, CancellationToken.None);
                try { await socket.CloseAsync(WebSocketCloseStatus.InternalServerError, "voicelive configure failed", CancellationToken.None); } catch { }
                return;
            }

            await SendJsonAsync(socket, new
            {
                type = "ready",
                model,
                voice = voiceName,
                style,
                avatar = avatarEnabled
                    ? new { enabled = true, character = avatarCharacter, style = avatarStyle }
                    : new { enabled = false, character = (string?)null, style = (string?)null }
            }, ct);

            // Track whether a response is active so we know when barge-in cancel is allowed.
            var responseActive = false;

            // Server -> Client: pump VoiceLive updates
            var pumpTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var serverEvent in session.GetUpdatesAsync(pumpCt))
                    {
                        if (!IsSocketWritable(socket)) break;

                        switch (serverEvent)
                        {
                            case SessionUpdateSessionCreated created:
                                _logger.LogInformation("VoiceLive: session created id={Id} modalities={Modalities}",
                                    created.Session?.Id,
                                    string.Join(",", created.Session?.Modalities?.Select(m => m.ToString()) ?? Array.Empty<string>()));
                                await SendJsonAsync(socket, new { type = "session_created", id = created.Session?.Id }, pumpCt);
                                break;

                            case SessionUpdateSessionUpdated upd:
                                _logger.LogInformation("VoiceLive: session updated modalities={Modalities}",
                                    string.Join(",", upd.Session?.Modalities?.Select(m => m.ToString()) ?? Array.Empty<string>()));
                                await SendJsonAsync(socket, new { type = "session_updated" }, pumpCt);
                                break;

                            case SessionUpdateAvatarConnecting avatarConnecting:
                                // Azure responded with the WebRTC ANSWER to our client offer.
                                // Forward the server_sdp so the browser can setRemoteDescription(answer).
                                var (iceList, serverSdp) = ExtractAvatarOffer(avatarConnecting);
                                await SendJsonAsync(socket, new
                                {
                                    type = "avatar_answer",
                                    iceServers = iceList,
                                    sdp = serverSdp
                                }, pumpCt);
                                _logger.LogInformation("VoiceLive: server SDP answer received from Azure ({Len} chars, {Ice} ICE servers)", serverSdp?.Length ?? 0, iceList.Length);
                                break;

                            case SessionUpdateInputAudioBufferSpeechStarted:
                                await SendJsonAsync(socket, new { type = "speech_started" }, pumpCt);
                                // Barge-in: cancel current response if any
                                if (responseActive)
                                {
                                    try { await session.CancelResponseAsync(pumpCt); } catch (Exception ex) { _logger.LogDebug(ex, "Cancel failed"); }
                                    try { await session.ClearStreamingAudioAsync(pumpCt); } catch (Exception ex) { _logger.LogDebug(ex, "ClearStreaming failed"); }
                                }
                                break;

                            case SessionUpdateInputAudioBufferSpeechStopped:
                                await SendJsonAsync(socket, new { type = "speech_stopped" }, pumpCt);
                                break;

                            case SessionUpdateResponseCreated:
                                responseActive = true;
                                await SendJsonAsync(socket, new { type = "response_created" }, pumpCt);
                                break;

                            case SessionUpdateResponseAudioDelta audioDelta:
                                if (audioDelta.Delta != null)
                                {
                                    var bytes = audioDelta.Delta.ToArray();
                                    if (bytes.Length > 0 && IsSocketWritable(socket))
                                    {
                                        try
                                        {
                                            await socket.SendAsync(bytes, WebSocketMessageType.Binary, true, pumpCt);
                                        }
                                        catch (Exception ex) when (ex is InvalidOperationException or WebSocketException or OperationCanceledException)
                                        {
                                            _logger.LogDebug(ex, "Audio send aborted (socket closing)");
                                            return;
                                        }
                                    }
                                }
                                break;

                            case SessionUpdateResponseAudioDone:
                                await SendJsonAsync(socket, new { type = "audio_done" }, pumpCt);
                                break;

                            case SessionUpdateResponseDone:
                                responseActive = false;
                                await SendJsonAsync(socket, new { type = "response_done" }, pumpCt);
                                break;

                            case SessionUpdateError err:
                                {
                                    var emsg = err.Error?.Message ?? "unknown";
                                    var ecode = err.Error?.Code ?? "-";
                                    var etype = err.Error?.Type ?? "-";
                                    var eparam = err.Error?.Param ?? "-";
                                    _logger.LogError("VoiceLive server error: code={Code} type={Type} param={Param} message={Message}", ecode, etype, eparam, emsg);
                                    await SendJsonAsync(socket, new { type = "error", code = ecode, errorType = etype, param = eparam, message = emsg }, pumpCt);
                                    responseActive = false;
                                    break;
                                }

                            // Transcripción del audio del USUARIO (lo que el micrófono captura).
                            case SessionUpdateConversationItemInputAudioTranscriptionDelta userDelta:
                                {
                                    var text = userDelta.Delta;
                                    if (!string.IsNullOrEmpty(text))
                                        await SendJsonAsync(socket, new { type = "user_transcript_delta", text }, pumpCt);
                                    break;
                                }
                            case SessionUpdateConversationItemInputAudioTranscriptionCompleted userDone:
                                {
                                    var text = userDone.Transcript;
                                    if (!string.IsNullOrEmpty(text))
                                        await SendJsonAsync(socket, new { type = "user_transcript", text }, pumpCt);
                                    break;
                                }
                            case SessionUpdateConversationItemInputAudioTranscriptionFailed userFailed:
                                _logger.LogWarning("VoiceLive: user transcription failed: {Msg}", userFailed.Error?.Message);
                                break;

                            // Transcripción del audio del ASISTENTE (lo que la IA está diciendo).
                            case SessionUpdateResponseAudioTranscriptDelta asstDelta:
                                {
                                    var text = asstDelta.Delta;
                                    if (!string.IsNullOrEmpty(text))
                                        await SendJsonAsync(socket, new { type = "transcript_delta", text }, pumpCt);
                                    break;
                                }
                            case SessionUpdateResponseAudioTranscriptDone asstDone:
                                {
                                    var text = asstDone.Transcript;
                                    if (!string.IsNullOrEmpty(text))
                                        await SendJsonAsync(socket, new { type = "transcript_done", text }, pumpCt);
                                    break;
                                }

                            default:
                                // Try to extract transcript deltas / animation events via reflection for forward-compat
                                var typeName = serverEvent.GetType().Name;
                                if (typeName.Contains("Transcript", StringComparison.OrdinalIgnoreCase))
                                {
                                    var deltaProp = serverEvent.GetType().GetProperty("Delta");
                                    var text = deltaProp?.GetValue(serverEvent)?.ToString();
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        var kind = typeName.Contains("Input", StringComparison.OrdinalIgnoreCase)
                                            ? "user_transcript" : "transcript_delta";
                                        await SendJsonAsync(socket, new { type = kind, text }, pumpCt);
                                    }
                                }
                                else if (typeName.Contains("Viseme", StringComparison.OrdinalIgnoreCase)
                                      || typeName.Contains("Blendshape", StringComparison.OrdinalIgnoreCase)
                                      || typeName.Contains("Animation", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Surface avatar lipsync data so the client can render activity.
                                    await SendJsonAsync(socket, new { type = "animation", source = typeName }, pumpCt);
                                }
                                else
                                {
                                    _logger.LogInformation("VoiceLive event passthrough: {Type}", typeName);
                                }
                                break;
                        }
                    }
                }
                catch (OperationCanceledException) { /* shutdown */ }
                catch (Exception ex) when (ex is InvalidOperationException or WebSocketException)
                {
                    // Socket transitioned to a non-writable state (Aborted/CloseReceived/Closed) while we were sending. Treat as a clean shutdown.
                    _logger.LogDebug(ex, "VoiceLive pump stopped (socket no longer writable)");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "VoiceLive pump error");
                    try { await SendJsonAsync(socket, new { type = "error", message = ex.Message }, CancellationToken.None); } catch { }
                }
                finally
                {
                    // Make sure the receive loop wakes up if the pump exited first (e.g. session error).
                    try { pumpCts.Cancel(); } catch { }
                }
            }, pumpCt);

            // Client -> Server: pump browser frames
            var buffer = new byte[16 * 1024];
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                try
                {
                    do
                    {
                        result = await socket.ReceiveAsync(buffer, ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            // Stop the server->client pump immediately so it doesn't try to
                            // write to a socket that is now in CloseReceived state.
                            try { pumpCts.Cancel(); } catch { }
                            try
                            {
                                if (socket.State == WebSocketState.CloseReceived)
                                {
                                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client closed", CancellationToken.None);
                                }
                            }
                            catch (Exception ex) when (ex is InvalidOperationException or WebSocketException) { /* already closing */ }
                            break;
                        }
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);
                }
                catch (Exception ex) when (ex is WebSocketException or InvalidOperationException or OperationCanceledException)
                {
                    // Aborted / disposed / cancelled socket — treat as a clean disconnect.
                    _logger.LogDebug(ex, "Receive aborted; client likely disconnected");
                    try { pumpCts.Cancel(); } catch { }
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close) break;

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var data = ms.ToArray();
                    if (data.Length > 0)
                    {
                        try { await session.SendInputAudioAsync(data, ct); }
                        catch (Exception ex) { _logger.LogWarning(ex, "Send audio failed"); }
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    var msg = Encoding.UTF8.GetString(ms.ToArray());
                    await HandleClientMessageAsync(session, msg, ct);
                }
            }

            // Ensure the pump exits before we dispose the session.
            try { pumpCts.Cancel(); } catch { }
            try { await pumpTask; } catch { }
        }
        catch (OperationCanceledException) { /* normal */ }
        catch (Exception ex) when (ex is InvalidOperationException or WebSocketException)
        {
            // Underlying socket was aborted or already closed — not a true error.
            _logger.LogDebug(ex, "VoiceLive WS aborted by client");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VoiceLive WS error");
            try { await SendJsonAsync(socket, new { type = "error", message = ex.Message }, CancellationToken.None); } catch { }
        }
        finally
        {
            try { pumpCts.Cancel(); } catch { }
            try { pumpCts.Dispose(); } catch { }
            session?.Dispose();
            if (socket.State == WebSocketState.Open)
            {
                try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); } catch { }
            }
            _logger.LogInformation("VoiceLive WS client disconnected");
        }
    }

    private async Task HandleClientMessageAsync(VoiceLiveSession session, string json, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("type", out var typeEl)) return;
            var type = typeEl.GetString();

            switch (type)
            {
                case "stop":
                    try { await session.CancelResponseAsync(ct); } catch { }
                    try { await session.ClearStreamingAudioAsync(ct); } catch { }
                    break;

                case "text":
                    if (doc.RootElement.TryGetProperty("text", out var textEl))
                    {
                        var text = textEl.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            await session.AddItemAsync(new UserMessageItem(text), ct);
                            await session.StartResponseAsync(ct);
                        }
                    }
                    break;

                case "avatar_offer":
                    // Browser built a WebRTC offer; relay it to Azure VoiceLive so it can
                    // produce the SDP answer (delivered later as SessionUpdateAvatarConnecting).
                    if (doc.RootElement.TryGetProperty("sdp", out var sdpEl))
                    {
                        var sdp = sdpEl.GetString();
                        if (!string.IsNullOrWhiteSpace(sdp))
                        {
                            try
                            {
                                await session.ConnectAvatarAsync(sdp, ct);
                                _logger.LogInformation("VoiceLive: client SDP offer relayed to Azure ({Len} chars)", sdp.Length);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "VoiceLive: ConnectAvatarAsync failed");
                            }
                        }
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bad client control message");
        }
    }

    private static Task SendJsonAsync(WebSocket socket, object payload, CancellationToken ct)
    {
        if (!IsSocketWritable(socket)) return Task.CompletedTask;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        try
        {
            return socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or WebSocketException or OperationCanceledException)
        {
            return Task.CompletedTask;
        }
    }

    private static bool IsSocketWritable(WebSocket socket)
        => socket.State == WebSocketState.Open;

    // ---------- Avatar WebRTC payload extraction (reflective, version-tolerant) ----------
    private static (object[] iceServers, string? serverSdp) ExtractAvatarOffer(SessionUpdateAvatarConnecting evt)
    {
        var payload = FindFirstNonNullProperty(evt, new[] { "Avatar", "SessionAvatarConnecting", "Connecting", "Value" }) ?? (object)evt;
        var iceProp = payload.GetType().GetProperty("IceServers");
        var sdpProp = payload.GetType().GetProperty("ServerSdp");

        var list = new List<object>();
        if (iceProp?.GetValue(payload) is System.Collections.IEnumerable iceEnumerable)
        {
            foreach (var server in iceEnumerable)
            {
                if (server is null) continue;
                var t = server.GetType();
                var urls = t.GetProperty("Urls")?.GetValue(server) as System.Collections.IEnumerable;
                var urlsArr = urls is null
                    ? Array.Empty<string>()
                    : urls.Cast<object>().Select(o => o?.ToString() ?? string.Empty).ToArray();
                list.Add(new
                {
                    urls = urlsArr,
                    username = t.GetProperty("Username")?.GetValue(server)?.ToString(),
                    credential = t.GetProperty("Credential")?.GetValue(server)?.ToString()
                });
            }
        }
        return (list.ToArray(), sdpProp?.GetValue(payload)?.ToString());
    }

    private static object? FindFirstNonNullProperty(object source, string[] names)
    {
        var type = source.GetType();
        foreach (var n in names)
        {
            var p = type.GetProperty(n);
            if (p is null) continue;
            var v = p.GetValue(source);
            if (v is not null) return v;
        }
        return null;
    }
}
