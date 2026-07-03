namespace AFWebChat;

/// <summary>
/// Carga el prompt de sistema compartido por Voice Live y Live Avatar
/// desde <c>Prompts/voice-system-prompt.txt</c>. Si el archivo no existe,
/// devuelve un fallback en español. Se relee en cada llamada para que los
/// cambios en el .txt se vean sin reiniciar el servidor.
/// </summary>
public static class VoicePrompt
{
    private const string Fallback =
        "Eres un asistente de IA útil. Responde de forma natural y conversacional en español.";

    private const string FileName = "voice-system-prompt.txt";

    // Candidate locations, in priority order. When running with `dotnet run`,
    // BaseDirectory points at bin/Debug/... (read first); the project source
    // dir (CurrentDirectory) keeps the change across rebuilds.
    private static IEnumerable<string> PromptDirs()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Prompts");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "Prompts");
    }

    public static string Load()
    {
        try
        {
            foreach (var dir in PromptDirs())
            {
                var path = Path.Combine(dir, FileName);
                if (File.Exists(path))
                {
                    var text = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
            }
        }
        catch
        {
            // silencioso — caemos al fallback
        }
        return Fallback;
    }

    /// <summary>
    /// Persiste el prompt editado desde la UI. Escribe en todas las ubicaciones
    /// candidatas para que el cambio sea inmediato (BaseDirectory, que Load lee
    /// primero) y sobreviva a recompilaciones (código fuente del proyecto).
    /// </summary>
    public static void Save(string text)
    {
        text = (text ?? string.Empty).Trim();
        foreach (var dir in PromptDirs())
        {
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, FileName), text);
            }
            catch
            {
                // best-effort por ubicación; seguimos con la siguiente
            }
        }
    }
}
