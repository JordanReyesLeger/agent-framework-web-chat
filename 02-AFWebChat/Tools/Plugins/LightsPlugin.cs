using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AFWebChat.Tools.Plugins;

public class LightsPlugin
{
    private readonly List<LightModel> _lights;

    public LightsPlugin()
    {
        _lights = new List<LightModel>
        {
            new() { Id = 1, Name = "Sala", IsOn = false, Brightness = Brightness.Medium, Color = "#FFFFFF" },
            new() { Id = 2, Name = "Cocina", IsOn = true, Brightness = Brightness.High, Color = "#FFD700" },
            new() { Id = 3, Name = "Recámara", IsOn = false, Brightness = Brightness.Low, Color = "#87CEEB" },
        };
    }

    [Description("Obtiene la lista de luces y su estado actual")]
    public List<LightModel> GetLights() => _lights;

    [Description("Cambia el estado de una luz (encender/apagar, brillo, color)")]
    public LightModel? ChangeState(
        [Description("ID de la luz")] int id,
        [Description("Encendida o apagada")] bool? isOn = null,
        [Description("Nivel de brillo: Low, Medium, High")] string? brightness = null,
        [Description("Color en hex (ej: #FF0000)")] string? color = null)
    {
        var light = _lights.FirstOrDefault(l => l.Id == id);
        if (light == null) return null;
        if (isOn.HasValue) light.IsOn = isOn.Value;
        if (!string.IsNullOrEmpty(brightness) && Enum.TryParse<Brightness>(brightness, true, out var b)) light.Brightness = b;
        if (!string.IsNullOrEmpty(color)) light.Color = color;
        return light;
    }
}

public class LightModel
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("is_on")] public bool? IsOn { get; set; }
    [JsonPropertyName("brightness")] public Brightness? Brightness { get; set; }
    [JsonPropertyName("color")]
    [Description("Color de la luz en hex (incluye #)")]
    public string? Color { get; set; }
}

public enum Brightness { Low, Medium, High }
