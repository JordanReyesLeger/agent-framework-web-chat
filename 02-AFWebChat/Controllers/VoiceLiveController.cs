using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.VoiceLive;
using Azure.Identity;
using AFWebChat.Tools.Plugins;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

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
    private static readonly string[] SupportedVoiceLiveModels =
        ["gpt-realtime-mini", "gpt-realtime"];

    private const string DragonHdOmniCatalogUrl =
        "https://raw.githubusercontent.com/Azure-Samples/Cognitive-Speech-TTS/master/Blog-Samples/Introducing-Dragon-HD-Omni/dragonhdomni_voice_list.json";
    private const string DragonHdOmniCacheKey = "voicelive:catalog:dragon-hd-omni";

    // The published sample currently contains one truncated pt-BR object. Parse
    // complete records independently so one malformed entry does not hide the
    // other 509 valid voices.
    private static readonly Regex DragonHdOmniVoiceRegex = new(
        """\{\s*"Voice Name"\s*:\s*"(?<id>(?:\\.|[^"\\])*)"\s*,\s*"Locale"\s*:\s*"(?<locale>(?:\\.|[^"\\])*)"\s*,\s*"Description"\s*:\s*"(?<description>(?:\\.|[^"\\])*)"\s*,\s*"Gender"\s*:\s*"(?<gender>(?:\\.|[^"\\])*)"\s*,\s*"Age Group"\s*:\s*"(?<age>(?:\\.|[^"\\])*)"\s*\}""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex Bcp47LocaleRegex = new(
        "^[a-zA-Z]{2,3}(?:-[a-zA-Z0-9]{2,8}){1,2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyDictionary<string, string[]> FullBodyAvatarStyles =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["lisa"] = ["casual-sitting"],
            ["meg"] = ["formal", "casual", "business"],
            ["lori"] = ["casual", "formal", "graceful"],
            ["max"] = ["formal", "casual", "business"],
            ["harry"] = ["business", "casual", "youthful"],
            ["jeff"] = ["business", "formal"],
            ["rowan"] = [],
            ["celine"] = [],
            ["nia"] = [],
            ["malik"] = []
        };

    private readonly IConfiguration _config;
    private readonly ILogger<VoiceLiveController> _logger;
    private readonly AzureSearchPlugin _searchPlugin;
    private readonly IMemoryCache _memoryCache;
    private readonly IHttpClientFactory _httpClientFactory;

    public VoiceLiveController(
        IConfiguration config,
        ILogger<VoiceLiveController> logger,
        AzureSearchPlugin searchPlugin,
        IMemoryCache memoryCache,
        IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _searchPlugin = searchPlugin;
        _memoryCache = memoryCache;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var availableModels = GetAvailableVoiceLiveModels();
        var defaultModel = NormalizeVoiceLiveModel(_config["VoiceLive:Model"]);
        return Ok(new
        {
        model = defaultModel,
        models = availableModels.Select(model => model switch
        {
            "gpt-realtime" => new { id = model, label = "GPT Realtime", tier = "Pro", description = "Modelo premium para máxima calidad y razonamiento de audio." },
            _ => new { id = model, label = "GPT Realtime Mini", tier = "Basic", description = "Menor costo y latencia; recomendado para la mayoría de conversaciones." }
        }),
        voice = _config["VoiceLive:Voice"] ?? _config["AzureSpeech:SynthesisVoiceName"] ?? "es-MX-DaliaNeural",
        inputLanguage = _config["AzureSpeech:RecognitionLanguage"] ?? "es-MX",
        // Prompt compartido por Voice Live y Live Avatar (Prompts/voice-system-prompt.txt).
        instructions = VoicePrompt.Load(),
        sampleRate = 24000
        });
    }

    [HttpGet("voices")]
    public async Task<IActionResult> GetVoices(CancellationToken ct)
    {
        var omniCatalog = await GetDragonHdOmniCatalogAsync(ct);
        var outputLocales = omniCatalog.Voices
            .Select(voice => voice.lang)
            .Append("es-ES")
            .Append("es-MX")
            .Append("en-US")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(locale => locale, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(new
        {
        defaultVoice = _config["VoiceLive:Voice"] ?? "en-US-Andrew3:DragonHDLatestNeural",
        outputLocales,
        omniCatalog = new
        {
            source = omniCatalog.Source,
            count = omniCatalog.Voices.Count,
            isFallback = omniCatalog.IsFallback
        },
        groups = new object[]
        {
            new
            {
                name = "Speech-to-Speech (OpenAI)",
                description = "Voces nativas del modelo realtime — latencia más baja, audio generado directamente por la IA.",
                type = "openai",
                voices = new[]
                {
                    new { id = "alloy",   label = "Alloy (S2S)",   lang = "multi", gender = "neutral", quality = "realtime", locales = Array.Empty<string>(), styles = Array.Empty<string>() },
                    new { id = "ash",     label = "Ash (S2S)",     lang = "multi", gender = "male",    quality = "realtime", locales = Array.Empty<string>(), styles = Array.Empty<string>() },
                    new { id = "ballad",  label = "Ballad (S2S)",  lang = "multi", gender = "male",    quality = "realtime", locales = Array.Empty<string>(), styles = Array.Empty<string>() },
                    new { id = "coral",   label = "Coral (S2S)",   lang = "multi", gender = "female",  quality = "realtime", locales = Array.Empty<string>(), styles = Array.Empty<string>() },
                    new { id = "echo",    label = "Echo (S2S)",    lang = "multi", gender = "male",    quality = "realtime", locales = Array.Empty<string>(), styles = Array.Empty<string>() },
                    new { id = "sage",    label = "Sage (S2S)",    lang = "multi", gender = "female",  quality = "realtime", locales = Array.Empty<string>(), styles = Array.Empty<string>() },
                    new { id = "shimmer", label = "Shimmer (S2S)", lang = "multi", gender = "female",  quality = "realtime", locales = Array.Empty<string>(), styles = Array.Empty<string>() },
                    new { id = "verse",   label = "Verse (S2S)",   lang = "multi", gender = "male",    quality = "realtime", locales = Array.Empty<string>(), styles = Array.Empty<string>() }
                }
            },
            new
            {
                name = "Dragon HD Omni · Multilingüe",
                description = "Voces Neural HD Omni en Preview. Todas detectan el idioma automáticamente; el locale indica la persona y acento originales.",
                type = "azure",
                voices = omniCatalog.Voices
            },
            new
            {
                name = "Azure HD (Dragon HD Latest)",
                description = "Catálogo Dragon HD de alta definición: voces GA y Preview en varios idiomas.",
                type = "azure",
                voices = new[]
                {
                    new { id = "de-DE-Florian:DragonHDLatestNeural", label = "Florian HD", lang = "de-DE", gender = "male", quality = "hd", locales = new[] { "de-DE", "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "de-DE-Seraphina:DragonHDLatestNeural", label = "Seraphina HD", lang = "de-DE", gender = "female", quality = "hd", locales = new[] { "de-DE", "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "en-US-Adam:DragonHDLatestNeural", label = "Adam HD", lang = "en-US", gender = "male", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "en-US-Alloy:DragonHDLatestNeural", label = "Alloy HD", lang = "en-US", gender = "male", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "en-US-Andrew:DragonHDLatestNeural", label = "Andrew HD", lang = "en-US", gender = "male", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "en-US-Andrew2:DragonHDLatestNeural", label = "Andrew 2 HD · Conversacional", lang = "en-US", gender = "male", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "en-US-Andrew3:DragonHDLatestNeural", label = "Andrew 3 HD · Podcast", lang = "en-US", gender = "male", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "Preview", styles = Array.Empty<string>() },
                    new { id = "en-US-Aria:DragonHDLatestNeural", label = "Aria HD", lang = "en-US", gender = "female", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "en-US-Ava:DragonHDLatestNeural", label = "Ava HD", lang = "en-US", gender = "female", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "en-US-Ava3:DragonHDLatestNeural", label = "Ava 3 HD · Podcast", lang = "en-US", gender = "female", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "Preview", styles = Array.Empty<string>() },
                    new { id = "en-US-Brian:DragonHDLatestNeural", label = "Brian HD", lang = "en-US", gender = "male", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "en-US-Davis:DragonHDLatestNeural", label = "Davis HD", lang = "en-US", gender = "male", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "en-US-Emma:DragonHDLatestNeural", label = "Emma HD", lang = "en-US", gender = "female", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "en-US-Emma2:DragonHDLatestNeural", label = "Emma 2 HD · Conversacional", lang = "en-US", gender = "female", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "en-US-Jenny:DragonHDLatestNeural", label = "Jenny HD", lang = "en-US", gender = "female", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "en-US-MultiTalker-Ava-Andrew:DragonHDLatestNeural", label = "Ava + Andrew · MultiTalker HD", lang = "en-US", gender = "neutral", quality = "hd", locales = new[] { "en-US" }, status = "Preview", styles = Array.Empty<string>() },
                    new { id = "en-US-Nova:DragonHDLatestNeural", label = "Nova HD", lang = "en-US", gender = "female", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "en-US-Phoebe:DragonHDLatestNeural", label = "Phoebe HD", lang = "en-US", gender = "female", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "en-US-Serena:DragonHDLatestNeural", label = "Serena HD", lang = "en-US", gender = "female", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "en-US-Steffan:DragonHDLatestNeural", label = "Steffan HD", lang = "en-US", gender = "male", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "es-ES-Tristan:DragonHDLatestNeural", label = "Tristan HD · España", lang = "es-ES", gender = "male", quality = "hd", locales = new[] { "es-ES", "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "es-ES-Ximena:DragonHDLatestNeural", label = "Ximena HD · España", lang = "es-ES", gender = "female", quality = "hd", locales = new[] { "es-ES", "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "es-MX-Tristan:DragonHDLatestNeural", label = "Tristan HD · México", lang = "es-MX", gender = "male", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "es-MX-Ximena:DragonHDLatestNeural", label = "Ximena HD · México", lang = "es-MX", gender = "female", quality = "hd", locales = new[] { "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "fr-FR-Remy:DragonHDLatestNeural", label = "Remy HD", lang = "fr-FR", gender = "male", quality = "hd", locales = new[] { "fr-FR", "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "fr-FR-Vivienne:DragonHDLatestNeural", label = "Vivienne HD", lang = "fr-FR", gender = "female", quality = "hd", locales = new[] { "fr-FR", "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "ja-JP-Masaru:DragonHDLatestNeural", label = "Masaru HD", lang = "ja-JP", gender = "male", quality = "hd", locales = new[] { "ja-JP", "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "ja-JP-Nanami:DragonHDLatestNeural", label = "Nanami HD", lang = "ja-JP", gender = "female", quality = "hd", locales = new[] { "ja-JP", "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "zh-CN-Xiaochen:DragonHDLatestNeural", label = "Xiaochen HD", lang = "zh-CN", gender = "female", quality = "hd", locales = new[] { "zh-CN", "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() },
                    new { id = "zh-CN-Yunfan:DragonHDLatestNeural", label = "Yunfan HD", lang = "zh-CN", gender = "male", quality = "hd", locales = new[] { "zh-CN", "es-MX", "en-US" }, status = "GA", styles = Array.Empty<string>() }
                }
            },
            new
            {
                name = "Multilingüe",
                description = "Voces neurales con soporte multi-idioma incluyendo español.",
                type = "azure",
                voices = new[]
                {
                    new { id = "en-US-AvaMultilingualNeural",     label = "Ava Multilingüe",     lang = "multi", gender = "female", quality = "multilingual", locales = new[] { "es-MX", "en-US" }, styles = new[] { "chat", "friendly", "cheerful", "empathetic" } },
                    new { id = "en-US-AndrewMultilingualNeural",  label = "Andrew Multilingüe",  lang = "multi", gender = "male",   quality = "multilingual", locales = new[] { "es-MX", "en-US" }, styles = new[] { "chat", "friendly", "professional" } },
                    new { id = "en-US-EmmaMultilingualNeural",    label = "Emma Multilingüe",    lang = "multi", gender = "female", quality = "multilingual", locales = new[] { "es-MX", "en-US" }, styles = new[] { "chat", "friendly", "cheerful" } },
                    new { id = "en-US-BrianMultilingualNeural",   label = "Brian Multilingüe",   lang = "multi", gender = "male",   quality = "multilingual", locales = new[] { "es-MX", "en-US" }, styles = new[] { "chat", "friendly", "professional" } }
                }
            },
            new
            {
                name = "Español",
                description = "Voces neurales nativas en español.",
                type = "azure",
                voices = new[]
                {
                    new { id = "es-MX-DaliaNeural",   label = "Dalia (México)",      lang = "es-MX", gender = "female", quality = "standard", locales = new[] { "es-MX", "en-US" }, styles = new[] { "cheerful", "chat", "friendly", "customerservice" } },
                    new { id = "es-MX-JorgeNeural",   label = "Jorge (México)",      lang = "es-MX", gender = "male",   quality = "standard", locales = new[] { "es-MX", "en-US" }, styles = new[] { "chat", "cheerful" } },
                    new { id = "es-ES-ElviraNeural",  label = "Elvira (España)",     lang = "es-ES", gender = "female", quality = "standard", locales = new[] { "es-ES", "en-US" }, styles = new[] { "cheerful", "chat", "friendly" } },
                    new { id = "es-ES-AlvaroNeural",  label = "Álvaro (España)",     lang = "es-ES", gender = "male",   quality = "standard", locales = new[] { "es-ES", "en-US" }, styles = new[] { "chat" } },
                    new { id = "es-AR-ElenaNeural",   label = "Elena (Argentina)",   lang = "es-AR", gender = "female", quality = "standard", locales = new[] { "es-AR", "en-US" }, styles = Array.Empty<string>() },
                    new { id = "es-CO-SalomeNeural",  label = "Salomé (Colombia)",   lang = "es-CO", gender = "female", quality = "standard", locales = new[] { "es-CO", "en-US" }, styles = Array.Empty<string>() }
                }
            }
        },
        // Avatar characters supported by Voice Live. Full-body avatars use a
        // character/style pair; Talking Heads use photo-avatar + vasa-1.
        avatar = new
        {
            defaultCharacter = _config["AzureSpeech:AvatarCharacter"] ?? "lisa",
            defaultStyle = _config["AzureSpeech:AvatarStyle"] ?? "casual-sitting",
            characters = new object[]
            {
                // The remaining Lisa styles are batch-only according to the standard avatar catalog.
                new { id = "lisa",   label = "Lisa",   gender = "female", type = "fullbody", styles = new[] { "casual-sitting" } },
                new { id = "meg",    label = "Meg",    gender = "female", type = "fullbody", styles = new[] { "formal", "casual", "business" } },
                new { id = "lori",   label = "Lori",   gender = "female", type = "fullbody", styles = new[] { "casual", "formal", "graceful" } },
                new { id = "max",    label = "Max",    gender = "male",   type = "fullbody", styles = new[] { "formal", "casual", "business" } },
                new { id = "harry",  label = "Harry",  gender = "male",   type = "fullbody", styles = new[] { "business", "casual", "youthful" } },
                new { id = "jeff",   label = "Jeff (retiro Dic 2026)", gender = "male", type = "fullbody", styles = new[] { "business", "formal" } },
                new { id = "rowan",  label = "Rowan",  gender = "male",   type = "fullbody", styles = Array.Empty<string>() },
                new { id = "celine", label = "Celine", gender = "female", type = "fullbody", styles = Array.Empty<string>() },
                new { id = "nia",    label = "Nia",    gender = "female", type = "fullbody", styles = Array.Empty<string>() },
                new { id = "malik",  label = "Malik",  gender = "male",   type = "fullbody", styles = Array.Empty<string>() },
                // Talking Heads (photo avatars, preview) supported through vasa-1.
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
    }

    // Names recognized as OpenAI S2S voices (from the realtime model).
    private static readonly HashSet<string> OpenAIVoiceNames =
        new(StringComparer.OrdinalIgnoreCase) { "alloy", "ash", "ballad", "coral", "echo", "sage", "shimmer", "verse" };

    private static bool IsOpenAIVoice(string voiceName, string? voiceType)
        => string.Equals(voiceType, "openai", StringComparison.OrdinalIgnoreCase)
            || (string.IsNullOrEmpty(voiceType) && OpenAIVoiceNames.Contains(voiceName));

    [HttpPost("prompt-session")]
    public IActionResult CreatePromptSession([FromBody] VoiceLivePromptSessionRequest request)
    {
        var instructions = string.IsNullOrWhiteSpace(request.Instructions)
            ? VoicePrompt.Load()
            : request.Instructions.Trim();

        if (instructions.Length > 32_000)
        {
            return BadRequest(new { error = "Prompt demasiado largo. Máximo 32,000 caracteres." });
        }

        var id = Guid.NewGuid().ToString("N");
        _memoryCache.Set(GetPromptCacheKey(id), instructions, TimeSpan.FromMinutes(15));
        _logger.LogInformation("VoiceLive: prompt session created id={PromptId} length={Length} preview={Preview}",
            id, instructions.Length, instructions[..Math.Min(instructions.Length, 80)].ReplaceLineEndings(" "));

        return Ok(new { promptId = id });
    }

    // Use a method-agnostic route so WebSocket handshakes work both over
    // HTTP/1.1 (GET + Upgrade) and HTTP/2 (CONNECT + :protocol=websocket).
    [Route("ws")]
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

            var defaultModel = NormalizeVoiceLiveModel(_config["VoiceLive:Model"]);
            var model = NormalizeVoiceLiveModel(Request.Query["model"].FirstOrDefault(), defaultModel);
            var defaultVoice = _config["VoiceLive:Voice"] ?? _config["AzureSpeech:SynthesisVoiceName"] ?? "es-MX-DaliaNeural";
            // Lee Prompts/voice-system-prompt.txt en cada conexión — sin reiniciar el server.
            var defaultInstructions = VoicePrompt.Load();

            // Allow per-session overrides via query string
            // ?voice=...&voiceType=openai|azure&style=...&instructions=...
            // &avatar=1&avatarCharacter=lisa&avatarStyle=casual-sitting
            var voiceName = Request.Query["voice"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(voiceName)) voiceName = defaultVoice;
            voiceName = voiceName.ReplaceLineEndings(" ");

            var voiceType = Request.Query["voiceType"].FirstOrDefault();
            var style = Request.Query["style"].FirstOrDefault();
            var outputLocale = Request.Query["outputLocale"].FirstOrDefault();
            if (!IsSupportedOutputLocale(outputLocale)) outputLocale = null;
            var inputLanguage = Request.Query["inputLanguage"].FirstOrDefault();
            if (inputLanguage is null) inputLanguage = _config["AzureSpeech:RecognitionLanguage"] ?? "es-MX";
            if (!IsSupportedInputLanguage(inputLanguage)) inputLanguage = "es-MX";

            var promptId = Request.Query["promptId"].FirstOrDefault();
            var instructions = ResolveInstructions(promptId, Request.Query["instructions"].FirstOrDefault(), defaultInstructions);

            var avatarEnabled = Request.Query["avatar"].FirstOrDefault() is string a
                && (a == "1" || string.Equals(a, "true", StringComparison.OrdinalIgnoreCase));
            var avatarCharacter = Request.Query["avatarCharacter"].FirstOrDefault()
                ?? _config["AzureSpeech:AvatarCharacter"] ?? "lisa";
            var requestedAvatarStyle = Request.Query["avatarStyle"].FirstOrDefault();
            var defaultAvatarCharacter = _config["AzureSpeech:AvatarCharacter"] ?? "lisa";
            if (string.IsNullOrWhiteSpace(requestedAvatarStyle)
                && string.Equals(avatarCharacter, defaultAvatarCharacter, StringComparison.OrdinalIgnoreCase))
            {
                requestedAvatarStyle = _config["AzureSpeech:AvatarStyle"];
            }
            var avatarPhoto = Request.Query["avatarPhoto"].FirstOrDefault() is string ap
                && (ap == "1" || string.Equals(ap, "true", StringComparison.OrdinalIgnoreCase));
            var avatarStyle = avatarPhoto ? null : NormalizeAvatarStyle(avatarCharacter, requestedAvatarStyle);

            _logger.LogInformation(
                "VoiceLive: starting session model={Model} voice={Voice} type={Type} style={Style} outputLocale={OutputLocale} inputLanguage={InputLanguage} avatar={Avatar} promptLength={PromptLength} promptPreview={PromptPreview}",
                model, voiceName, voiceType ?? "auto", style ?? "-", outputLocale ?? "auto", string.IsNullOrEmpty(inputLanguage) ? "auto" : inputLanguage, avatarEnabled,
                instructions.Length, instructions[..Math.Min(instructions.Length, 80)].ReplaceLineEndings(" "));

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
                if (!string.IsNullOrWhiteSpace(outputLocale)) azureVoice.Locale = outputLocale;
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
                InputAudioTranscription = new AudioInputTranscriptionOptions(AudioInputTranscriptionOptionsModel.Whisper1)
                {
                    Language = inputLanguage
                },
                TurnDetection = new AzureSemanticVadTurnDetection
                {
                    Threshold = 0.5f,
                    PrefixPadding = TimeSpan.FromMilliseconds(300),
                    SilenceDuration = TimeSpan.FromMilliseconds(500),
                    RemoveFillerWords = true
                }
            };
            // RAG tool: Azure Search (solo si el usuario activó el checkbox)
            var ragEnabled = Request.Query["rag"].FirstOrDefault() is string ragVal
                && (ragVal == "1" || string.Equals(ragVal, "true", StringComparison.OrdinalIgnoreCase));
            if (ragEnabled)
            {
                sessionOptions.Tools.Add(new VoiceLiveFunctionDefinition("search_documents")
                {
                    Description = "Busca información en documentos indexados usando Azure Search. Utiliza búsqueda híbrida semántica para encontrar contenido relevante. Usa esta herramienta cuando el usuario pregunte sobre documentos, expedientes o información específica.",
                    Parameters = BinaryData.FromObjectAsJson(new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new
                            {
                                type = "string",
                                description = "La consulta de búsqueda en lenguaje natural"
                            },
                            max_results = new
                            {
                                type = "integer",
                                description = "Número máximo de resultados (default: 5, máximo: 15)"
                            }
                        },
                        required = new[] { "query" }
                    })
                });
                _logger.LogInformation("VoiceLive: RAG tool (search_documents) habilitado");
            }

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
                        if (!string.IsNullOrWhiteSpace(avatarStyle)) avatarCfg.Style = avatarStyle;
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
                outputLocale,
                inputLanguage,
                style,
                avatar = avatarEnabled
                    ? new { enabled = true, character = (string?)avatarCharacter, style = (string?)avatarStyle }
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
                                if (upd.Session?.Avatar?.IceServers is { Count: > 0 } iceServers)
                                {
                                    await SendJsonAsync(socket, new
                                    {
                                        type = "avatar_ice",
                                        iceServers = ConvertIceServers(iceServers)
                                    }, pumpCt);
                                    _logger.LogInformation("VoiceLive: avatar ICE servers received from Azure ({Count})", iceServers.Count);
                                }
                                break;

                            case SessionUpdateAvatarConnecting avatarConnecting:
                                // Azure responded with the WebRTC ANSWER to our client offer.
                                // Forward the server_sdp so the browser can setRemoteDescription(answer).
                                await SendJsonAsync(socket, new
                                {
                                    type = "avatar_answer",
                                    sdp = avatarConnecting.ServerSdp
                                }, pumpCt);
                                _logger.LogInformation("VoiceLive: server SDP answer received from Azure ({Len} chars)", avatarConnecting.ServerSdp?.Length ?? 0);
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

                            case SessionUpdateResponseFunctionCallArgumentsDone funcCall:
                                {
                                    _logger.LogInformation("VoiceLive: function call received name={Name} callId={CallId}", funcCall.Name, funcCall.CallId);
                                    await SendJsonAsync(socket, new { type = "function_call", name = funcCall.Name, callId = funcCall.CallId }, pumpCt);

                                    string toolOutput;
                                    try
                                    {
                                        toolOutput = await HandleFunctionCallAsync(funcCall.Name, funcCall.Arguments, pumpCt);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Tool execution failed for {Name}", funcCall.Name);
                                        toolOutput = $"Error ejecutando herramienta: {ex.Message}";
                                    }

                                    // Return the tool output to the model and let it generate a spoken response.
                                    await session.AddItemAsync(new FunctionCallOutputItem(funcCall.CallId, toolOutput), pumpCt);
                                    await session.StartResponseAsync(pumpCt);
                                    await SendJsonAsync(socket, new { type = "function_call_done", name = funcCall.Name, callId = funcCall.CallId }, pumpCt);
                                    break;
                                }

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

    private string ResolveInstructions(string? promptId, string? queryInstructions, string defaultInstructions)
    {
        if (!string.IsNullOrWhiteSpace(promptId)
            && _memoryCache.TryGetValue(GetPromptCacheKey(promptId), out string? cachedInstructions)
            && !string.IsNullOrWhiteSpace(cachedInstructions))
        {
            return cachedInstructions;
        }

        if (!string.IsNullOrWhiteSpace(queryInstructions))
        {
            return queryInstructions;
        }

        return defaultInstructions;
    }

    private static string GetPromptCacheKey(string promptId) => $"voicelive:prompt:{promptId}";

    private static bool IsSupportedInputLanguage(string? language)
        => language is "" or "es-MX" or "es-ES" or "en-US";

    private string[] GetAvailableVoiceLiveModels()
    {
        var configuredModels = _config.GetSection("VoiceLive:Models").Get<string[]>() ?? [];
        var availableModels = configuredModels
            .Where(model => SupportedVoiceLiveModels.Contains(model, StringComparer.OrdinalIgnoreCase))
            .Select(model => SupportedVoiceLiveModels.First(candidate => string.Equals(candidate, model, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return availableModels.Length > 0 ? availableModels : SupportedVoiceLiveModels;
    }

    private string NormalizeVoiceLiveModel(string? model, string fallback = "gpt-realtime-mini")
    {
        var availableModels = GetAvailableVoiceLiveModels();
        return availableModels.FirstOrDefault(candidate => string.Equals(candidate, model, StringComparison.OrdinalIgnoreCase))
            ?? availableModels.FirstOrDefault(candidate => string.Equals(candidate, fallback, StringComparison.OrdinalIgnoreCase))
            ?? availableModels[0];
    }

    private static bool IsSupportedOutputLocale(string? locale)
        => string.IsNullOrEmpty(locale)
            || (locale.Length <= 32 && Bcp47LocaleRegex.IsMatch(locale));

    private async Task<OmniCatalogResult> GetDragonHdOmniCatalogAsync(CancellationToken ct)
    {
        if (_memoryCache.TryGetValue(DragonHdOmniCacheKey, out OmniCatalogResult? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));
            var rawCatalog = await client.GetStringAsync(DragonHdOmniCatalogUrl, timeoutCts.Token);
            var voices = ParseDragonHdOmniVoices(rawCatalog);

            if (voices.Count < 100)
            {
                throw new InvalidOperationException($"Dragon HD Omni catalog returned only {voices.Count} valid records.");
            }

            var result = new OmniCatalogResult(voices, DragonHdOmniCatalogUrl, false);
            _memoryCache.Set(DragonHdOmniCacheKey, result, TimeSpan.FromHours(12));
            _logger.LogInformation("VoiceLive: loaded {Count} Dragon HD Omni voices from Azure Samples", voices.Count);
            return result;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested
            && ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "VoiceLive: Dragon HD Omni catalog unavailable; using Spanish fallback");
            var fallback = new OmniCatalogResult(CreateSpanishOmniFallback(), "built-in-es-ES-es-MX", true);
            _memoryCache.Set(DragonHdOmniCacheKey, fallback, TimeSpan.FromMinutes(15));
            return fallback;
        }
    }

    private static IReadOnlyList<OmniVoiceCatalogItem> ParseDragonHdOmniVoices(string rawCatalog)
    {
        var voices = new List<OmniVoiceCatalogItem>(600);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in DragonHdOmniVoiceRegex.Matches(rawCatalog))
        {
            if (voices.Count >= 1_000) break;

            var id = JsonUnescape(match.Groups["id"].Value);
            var rawLocale = JsonUnescape(match.Groups["locale"].Value);
            var description = JsonUnescape(match.Groups["description"].Value);
            var gender = JsonUnescape(match.Groups["gender"].Value).ToLowerInvariant();
            var ageGroup = JsonUnescape(match.Groups["age"].Value);

            if (id.Length > 160
                || !id.EndsWith(":DragonHDOmniLatestNeural", StringComparison.OrdinalIgnoreCase)
                || !Bcp47LocaleRegex.IsMatch(rawLocale)
                || !id.StartsWith(rawLocale + "-", StringComparison.OrdinalIgnoreCase)
                || gender is not ("female" or "male" or "neutral")
                || !seen.Add(id))
            {
                continue;
            }

            var locale = NormalizeLocale(rawLocale);
            var persona = id[(rawLocale.Length + 1)..id.IndexOf(':')];
            voices.Add(new OmniVoiceCatalogItem(
                id,
                $"{UppercaseFirst(persona)} Omni HD",
                locale,
                gender,
                "omni-hd",
                [],
                [],
                "Preview",
                description.Length > 500 ? description[..500] : description,
                ageGroup,
                true));
        }

        return voices
            .OrderBy(voice => voice.lang, StringComparer.OrdinalIgnoreCase)
            .ThenBy(voice => voice.label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<OmniVoiceCatalogItem> CreateSpanishOmniFallback()
    {
        const string fallbackData = """
es-es-abril|female|Adult
es-es-alvaro|male|Adult
es-es-arabella|female|Adult
es-es-argentatitlan|female|Adult
es-es-arnau|male|Adult
es-es-dario|male|Young Adult
es-es-elias|male|Young Adult
es-es-elvira|female|Adult
es-es-estrella|female|Young Adult
es-es-irene|female|Child
es-es-isidora|female|Senior
es-es-jadesunflare|female|Young Adult
es-es-javier|male|Young Adult
es-es-laia|female|Adult
es-es-lia|female|Adult
es-es-nil|male|Adult
es-es-saul|male|Adult
es-es-sepiasinfonia|male|Young Adult
es-es-teo|male|Young Adult
es-es-triana|female|Young Adult
es-es-tristan|male|Young Adult
es-es-vera|female|Young Adult
es-es-ximena|female|Adult
es-mx-beatriz|female|Young Adult
es-mx-candela|female|Young Adult
es-mx-carlota|female|Young Adult
es-mx-cecilio|male|Adult
es-mx-dalia|female|Adult
es-mx-gerardo|male|Young Adult
es-mx-jorge|male|Adult
es-mx-larissa|female|Young Adult
es-mx-lemonrocio|male|Senior
es-mx-liberto|male|Young Adult
es-mx-luciano|male|Adult
es-mx-marina|female|Child
es-mx-nuria|female|Young Adult
es-mx-pelayo|male|Young Adult
es-mx-renata|female|Young Adult
es-mx-yago|male|Young Adult
""";

        return fallbackData.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('|'))
            .Select(parts =>
            {
                var baseName = parts[0];
                var rawLocale = baseName[..5];
                var persona = baseName[6..];
                return new OmniVoiceCatalogItem(
                    $"{baseName}:DragonHDOmniLatestNeural",
                    $"{UppercaseFirst(persona)} Omni HD",
                    NormalizeLocale(rawLocale),
                    parts[1],
                    "omni-hd",
                    [],
                    [],
                    "Preview",
                    "Dragon HD Omni multilingüe.",
                    parts[2],
                    true);
            })
            .ToArray();
    }

    private static string JsonUnescape(string value)
        => JsonSerializer.Deserialize<string>($"\"{value}\"") ?? string.Empty;

    private static string NormalizeLocale(string locale)
    {
        var parts = locale.Split('-');
        parts[0] = parts[0].ToLowerInvariant();
        if (parts.Length > 1 && parts[1].Length == 2) parts[1] = parts[1].ToUpperInvariant();
        for (var index = 2; index < parts.Length; index++) parts[index] = parts[index].ToLowerInvariant();
        return string.Join('-', parts);
    }

    private static string UppercaseFirst(string value)
        => string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private sealed record OmniCatalogResult(
        IReadOnlyList<OmniVoiceCatalogItem> Voices,
        string Source,
        bool IsFallback);

    private sealed record OmniVoiceCatalogItem(
        string id,
        string label,
        string lang,
        string gender,
        string quality,
        string[] locales,
        string[] styles,
        string status,
        string description,
        string ageGroup,
        bool multilingual);

    private static string? NormalizeAvatarStyle(string character, string? requestedStyle)
    {
        if (!FullBodyAvatarStyles.TryGetValue(character, out var supportedStyles) || supportedStyles.Length == 0)
        {
            return null;
        }

        return supportedStyles.FirstOrDefault(style => string.Equals(style, requestedStyle, StringComparison.OrdinalIgnoreCase))
            ?? supportedStyles[0];
    }

    private async Task<string> HandleFunctionCallAsync(string functionName, string argumentsJson, CancellationToken ct)
    {
        switch (functionName)
        {
            case "search_documents":
                using (var doc = JsonDocument.Parse(argumentsJson))
                {
                    var root = doc.RootElement;
                    var query = root.GetProperty("query").GetString() ?? string.Empty;
                    var maxResults = root.TryGetProperty("max_results", out var mr) ? mr.GetInt32() : 5;
                    _logger.LogInformation("VoiceLive tool: search_documents query={Query} maxResults={Max}", query, maxResults);
                    return await _searchPlugin.SearchDocuments(query, maxResults, ct);
                }

            default:
                _logger.LogWarning("VoiceLive: unknown function call '{Name}'", functionName);
                return $"Función '{functionName}' no reconocida.";
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

    private static object[] ConvertIceServers(IEnumerable<IceServer> iceServers)
        => iceServers.Select(server => new
        {
            urls = server.Uris.Select(uri => uri.ToString()).ToArray(),
            username = server.Username,
            credential = server.Credential
        }).ToArray();
}

public record VoiceLivePromptSessionRequest(string? Instructions);
