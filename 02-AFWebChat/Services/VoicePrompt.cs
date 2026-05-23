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

    public static string Load()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Prompts", "voice-system-prompt.txt");
            if (!File.Exists(path))
            {
                // Cuando se corre con `dotnet run`, BaseDirectory apunta a bin/Debug/...
                // Probemos también la raíz del proyecto (Directory.GetCurrentDirectory).
                path = Path.Combine(Directory.GetCurrentDirectory(), "Prompts", "voice-system-prompt.txt");
            }
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path).Trim();
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
        }
        catch
        {
            // silencioso — caemos al fallback
        }
        return Fallback;
    }
}
