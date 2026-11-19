using AgentFrameworkTests.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace AgentFrameworkTests.Tests;

// ---------- Modelo y plugin de luces para la demo ----------

/// <summary>
/// Representa una luz inteligente con su estado actual.
/// </summary>
internal sealed class Light
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsOn { get; set; }

    public override string ToString() => $"[{Id}] {Name} — {(IsOn ? "Encendida" : "Apagada")}";
}

/// <summary>
/// Plugin de luces inteligentes.
/// Contiene herramientas que el agente puede invocar para gestionar luces.
/// </summary>
internal static class LightsPlugin
{
    // Estado simulado de las luces (mutable para que ToggleLight pueda modificarlo)
    private static readonly List<Light> _lights =
    [
        new() { Id = 1, Name = "Sala de estar", IsOn = true },
        new() { Id = 2, Name = "Cocina", IsOn = false },
        new() { Id = 3, Name = "Dormitorio", IsOn = false },
        new() { Id = 4, Name = "Oficina", IsOn = true }
    ];

    /// <summary>
    /// Lista todas las luces disponibles con su estado actual.
    /// </summary>
    [Description("Lista todas las luces inteligentes disponibles con su estado actual (encendida/apagada)")]
    public static string ListLights()
    {
        return JsonSerializer.Serialize(_lights,
            new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Enciende o apaga una luz específica por su identificador.
    /// </summary>
    [Description("Enciende o apaga una luz inteligente específica")]
    public static string ToggleLight(
        [Description("El identificador numérico de la luz")] int id,
        [Description("true para encender la luz, false para apagarla")] bool isOn)
    {
        var light = _lights.FirstOrDefault(l => l.Id == id);
        if (light is null)
            return $"No se encontró una luz con Id {id}";

        light.IsOn = isOn;
        return $"Luz '{light.Name}' (Id: {id}) ahora está {(isOn ? "encendida" : "apagada")}";
    }

    /// <summary>
    /// Resetea las luces a su estado inicial (para evitar efectos cruzados entre tests).
    /// </summary>
    public static void Reset()
    {
        _lights[0].IsOn = true;
        _lights[1].IsOn = false;
        _lights[2].IsOn = false;
        _lights[3].IsOn = true;
    }
}

/// <summary>
/// Módulo 03: Herramientas de función (Function Tools).
/// Demuestra cómo crear funciones que el agente puede invocar automáticamente
/// para obtener información externa o ejecutar acciones.
/// </summary>
public class _03_FunctionTools
{
    private readonly ITestOutputHelper _output;

    public _03_FunctionTools(ITestOutputHelper output)
    {
        _output = output;
    }

    // ---------- Funciones auxiliares para las herramientas ----------

    /// <summary>
    /// Simula obtener el clima de una ciudad (herramienta del agente).
    /// </summary>
    [Description("Obtiene el clima actual de una ciudad especificada")]
    private static string GetWeather([Description("El nombre de la ciudad para obtener el clima")] string city)
    {
        // Datos simulados para la demostración
        var weatherData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Madrid"] = "Soleado, 28°C",
            ["London"] = "Nublado, 15°C",
            ["Tokyo"] = "Lluvioso, 22°C",
            ["New York"] = "Parcialmente nublado, 20°C"
        };

        return weatherData.TryGetValue(city, out var weather)
            ? $"Clima en {city}: {weather}"
            : $"Datos del clima no disponibles para {city}";
    }

    /// <summary>
    /// Simula obtener la hora actual en una zona horaria (segunda herramienta).
    /// </summary>
    [Description("Obtiene la hora actual en una zona horaria especificada")]
    private static string GetCurrentTime([Description("La zona horaria, por ejemplo: 'UTC', 'EST', 'CET'")] string timezone)
    {
        return $"Hora actual en {timezone}: {DateTime.UtcNow:HH:mm:ss} UTC (simulada)";
    }

    /// <summary>
    /// Simula buscar información sobre un producto (tercera herramienta).
    /// </summary>
    [Description("Busca información de productos por nombre")]
    private static string SearchProduct(
        [Description("El nombre del producto a buscar")] string productName,
        [Description("Número máximo de resultados a devolver")] int maxResults = 3)
    {
        return $"Se encontraron {maxResults} resultados para '{productName}': Producto A ($29.99), Producto B ($49.99), Producto C ($19.99)";
    }

    // ---------- Pruebas ----------

    /// <summary>
    /// Crea una herramienta de función simple y la asigna al agente.
    /// El agente decide cuándo invocar la herramienta según el contexto del mensaje.
    /// </summary>
    [Fact]
    public async Task Should_Use_Single_Function_Tool()
    {
        // Crear la herramienta usando AIFunctionFactory
        // El factory inspecciona los atributos [Description] para generar el esquema
        AIFunction weatherTool = AIFunctionFactory.Create(GetWeather);

        // Crear el agente con la herramienta disponible
        AIAgent agent = TestConfiguration.CreateAgent(
            instructions: "Eres un asistente de clima. Usa la herramienta GetWeather para responder preguntas sobre el clima. Responde en una oración.",
            tools: new List<AITool> { weatherTool });

        // Crear sesión (requerida para RunAsync)
        AgentSession session = await agent.CreateSessionAsync();

        // El agente decidirá automáticamente usar la herramienta GetWeather
        AgentResponse response = await agent.RunAsync("¿Cómo está el clima en Madrid?", session);

        Assert.NotNull(response);
        Assert.NotNull(response.Text);

        _output.WriteLine("✅ Respuesta con herramienta de clima:");
        _output.WriteLine($"   {response.Text}");
    }

    /// <summary>
    /// Asigna múltiples herramientas al agente y deja que elija cuál usar.
    /// El modelo selecciona la herramienta apropiada según el contenido del mensaje.
    /// </summary>
    [Fact]
    public async Task Should_Select_Appropriate_Tool_From_Multiple()
    {
        // Crear múltiples herramientas
        AIFunction weatherTool = AIFunctionFactory.Create(GetWeather);
        AIFunction timeTool = AIFunctionFactory.Create(GetCurrentTime);
        AIFunction productTool = AIFunctionFactory.Create(SearchProduct);

        // Asignar las tres herramientas al agente
        AIAgent agent = TestConfiguration.CreateAgent(
            instructions: "Eres un asistente multi-propósito con acceso a herramientas de clima, hora y búsqueda de productos. Usa la herramienta apropiada según la pregunta del usuario. Responde de forma concisa.",
            tools: new List<AITool> { weatherTool, timeTool, productTool });

        // Crear sesión (requerida para RunAsync)
        AgentSession session = await agent.CreateSessionAsync();

        // Pregunta sobre el clima → debería usar GetWeather
        AgentResponse weatherResponse = await agent.RunAsync("¿Cómo está el clima en Tokio?", session);
        Assert.NotNull(weatherResponse.Text);
        _output.WriteLine($"✅ Pregunta de clima: {weatherResponse.Text}");

        // Pregunta sobre la hora → debería usar GetCurrentTime
        AgentResponse timeResponse = await agent.RunAsync("¿Qué hora es en CET?", session);
        Assert.NotNull(timeResponse.Text);
        _output.WriteLine($"✅ Pregunta de hora: {timeResponse.Text}");

        // Pregunta sobre productos → debería usar SearchProduct
        AgentResponse productResponse = await agent.RunAsync("Busca laptops", session);
        Assert.NotNull(productResponse.Text);
        _output.WriteLine($"✅ Pregunta de productos: {productResponse.Text}");
    }

    /// <summary>
    /// Crea herramientas usando funciones lambda en línea.
    /// Alternativa rápida cuando no necesitas un método separado.
    /// </summary>
    [Fact]
    public async Task Should_Use_Inline_Lambda_Tools()
    {
        // Crear herramientas directamente con lambdas
        AIFunction calculatorTool = AIFunctionFactory.Create(
            ([Description("Primer número")] double a, [Description("Segundo número")] double b) =>
                $"Resultado: {a + b}",
            "Add",
            "Suma dos números");

        AIFunction greetTool = AIFunctionFactory.Create(
            ([Description("Nombre de la persona")] string name) =>
                $"¡Hola, {name}! ¡Bienvenido al taller de Agent Framework!",
            "Greet",
            "Genera un saludo personalizado");

        AIAgent agent = TestConfiguration.CreateAgent(
            instructions: "Eres un asistente útil con herramientas de calculadora y saludo. Úsalas cuando sea apropiado.",
            tools: new List<AITool> { calculatorTool, greetTool });

        // Crear sesión (requerida para RunAsync)
        AgentSession session = await agent.CreateSessionAsync();

        AgentResponse response = await agent.RunAsync("¿Cuánto es 42 más 58?", session);

        Assert.NotNull(response.Text);
        _output.WriteLine($"✅ Resultado con lambda tool: {response.Text}");
    }

    /// <summary>
    /// Demuestra un plugin de luces inteligentes con dos herramientas:
    /// ListLights (listar luces) y ToggleLight (encender/apagar).
    /// El agente usa ambas herramientas para responder consultas sobre las luces.
    /// </summary>
    [Fact]
    public async Task Should_Use_Lights_Plugin_Tools()
    {
        // Resetear estado de las luces para evitar efectos de otros tests
        LightsPlugin.Reset();

        // Crear herramientas desde los métodos del plugin
        AIFunction listLightsTool = AIFunctionFactory.Create(LightsPlugin.ListLights);
        AIFunction toggleLightTool = AIFunctionFactory.Create(LightsPlugin.ToggleLight);

        AIAgent agent = TestConfiguration.CreateAgent(
            instructions: "Eres un asistente de hogar inteligente. Gestiona las luces usando las herramientas ListLights y ToggleLight. Responde siempre en español y de forma concisa.",
            tools: new List<AITool> { listLightsTool, toggleLightTool });

        AgentSession session = await agent.CreateSessionAsync();

        // 1. Listar luces — el agente debería usar ListLights
        AgentResponse listResponse = await agent.RunAsync("¿Cuáles luces tengo disponibles?", session);
        Assert.NotNull(listResponse.Text);
        _output.WriteLine($"✅ Listar luces: {listResponse.Text}");

        // 2. Encender una luz apagada — el agente debería usar ToggleLight
        AgentResponse toggleResponse = await agent.RunAsync("Enciende la luz de la cocina", session);
        Assert.NotNull(toggleResponse.Text);
        _output.WriteLine($"✅ Encender luz: {toggleResponse.Text}");

        // 3. Verificar el estado actualizado — el agente debería usar ListLights nuevamente
        AgentResponse statusResponse = await agent.RunAsync("Muéstrame el estado actual de todas las luces", session);
        Assert.NotNull(statusResponse.Text);
        _output.WriteLine($"✅ Estado actualizado: {statusResponse.Text}");
    }
}
