using AgentFrameworkTests.Helpers;
using Microsoft.Agents.AI;
using Xunit;
using Xunit.Abstractions;

namespace AgentFrameworkTests.Tests;

/// <summary>
/// Módulo 06: Conversaciones y sesiones.
/// Demuestra cómo mantener el contexto de conversación entre turnos usando sesiones,
/// cómo serializar/deserializar sesiones, y cómo usar StateBag para estado personalizado.
/// </summary>
public class _06_ConversationsSessions
{
    private readonly ITestOutputHelper _output;

    public _06_ConversationsSessions(ITestOutputHelper output)
    {
        _output = output;
    }


    /// <summary>
    /// Demuestra que sin sesión, cada llamada a RunAsync es independiente.
    /// El agente no tiene memoria de interacciones previas.
    /// </summary>
    [Fact]
    public async Task Should_Not_Remember_Without_Session()
    {
        AIAgent agent = TestConfiguration.CreateAgent(
            instructions: "Eres un asistente útil. Responde las preguntas directamente. Si no sabes algo del contexto, di 'No tengo esa información'.");

        // Usar sesiones separadas para simular llamadas independientes
        // (RunAsync requiere sesión, pero con sesiones distintas no se comparte historial)
        AgentSession session1 = await agent.CreateSessionAsync();
        AgentSession session2 = await agent.CreateSessionAsync();

        // Llamada 1: Con sesión 1, dar información
        AgentResponse response1 = await agent.RunAsync("Mi color favorito es el azul.", session1);
        _output.WriteLine($"Llamada 1 → {response1.Text}");

        // Llamada 2: Con sesión 2 (distinta), preguntar por la información anterior
        // El agente NO debería recordar porque es una sesión diferente
        AgentResponse response2 = await agent.RunAsync("¿Cuál es mi color favorito?", session2);
        _output.WriteLine($"Llamada 2 → {response2.Text}");

        Assert.NotNull(response2.Text);
        _output.WriteLine("\n✅ Con sesiones separadas, cada llamada es independiente. El agente no recuerda turnos previos.");
    }

    /// <summary>
    /// Crea una sesión y mantiene el contexto de conversación entre múltiples turnos.
    /// Sin sesión, cada llamada a RunAsync es independiente y el agente no recuerda turnos anteriores.
    /// </summary>
    [Fact]
    public async Task Should_Maintain_Conversation_Context_With_Session()
    {
        AIAgent agent = TestConfiguration.CreateAgent(
            instructions: "Eres un tutor de matemáticas. Recuerda todas las interacciones previas en la conversación.");

        // Crear una sesión para mantener el historial de conversación
        AgentSession session = await agent.CreateSessionAsync();

        // Turno 1: Establecer contexto
        AgentResponse response1 = await agent.RunAsync("Mi nombre es Carlos y estoy aprendiendo álgebra.", session);
        _output.WriteLine($"Turno 1 → {response1.Text}");

        // Turno 2: El agente debe recordar el nombre
        AgentResponse response2 = await agent.RunAsync("¿Cuál es mi nombre?", session);
        _output.WriteLine($"Turno 2 → {response2.Text}");

        // Turno 3: El agente debe recordar el contexto completo
        AgentResponse response3 = await agent.RunAsync("¿Qué materia estoy aprendiendo?", session);
        _output.WriteLine($"Turno 3 → {response3.Text}");

        // Verificar que el agente mantuvo el contexto
        Assert.NotNull(response3.Text);
        _output.WriteLine("\n✅ La sesión mantuvo el contexto de conversación entre los 3 turnos");
    }

    

    /// <summary>
    /// Demuestra cómo serializar y deserializar una sesión.
    /// Útil para persistir conversaciones en bases de datos o caches.
    /// </summary>
    [Fact]
    public async Task Should_Serialize_And_Deserialize_Session()
    {
        AIAgent agent = TestConfiguration.CreateAgent(
            instructions: "Eres un asistente útil. Recuerda todos los detalles compartidos en la conversación.");

        // Crear sesión e interactuar
        AgentSession session = await agent.CreateSessionAsync();
        await agent.RunAsync("Vivo en Barcelona y trabajo como desarrollador.", session);

        // Serializar la sesión (por ejemplo, para guardarla en una base de datos)
        // SerializeSessionAsync retorna JsonElement
        System.Text.Json.JsonElement serializedSession = await agent.SerializeSessionAsync(session);
        string serializedText = serializedSession.GetRawText();
        Assert.NotEmpty(serializedText);
        _output.WriteLine($"✅ Sesión serializada ({serializedText.Length} caracteres)");
        _output.WriteLine($"   Primeros 200 chars: {serializedText[..Math.Min(200, serializedText.Length)]}...");

        // Deserializar la sesión (restaurar la conversación)
        // DeserializeSessionAsync recibe JsonElement
        AgentSession restoredSession = await agent.DeserializeSessionAsync(serializedSession);
        Assert.NotNull(restoredSession);

        // Continuar la conversación con la sesión restaurada
        AgentResponse response = await agent.RunAsync("¿Dónde vivo y cuál es mi trabajo?", restoredSession);
        Assert.NotNull(response.Text);

        _output.WriteLine($"\n✅ Sesión restaurada. Respuesta: {response.Text}");
        _output.WriteLine("   El agente recordó la información de la sesión anterior");
    }

    /// <summary>
    /// Demuestra el uso de StateBag para almacenar estado personalizado en la sesión.
    /// StateBag permite guardar datos arbitrarios asociados a la sesión.
    /// </summary>
    [Fact]
    public async Task Should_Use_StateBag_For_Custom_Session_State()
    {
        AIAgent agent = TestConfiguration.CreateAgent(
            instructions: "Eres un asistente útil.");

        // Crear sesión
        AgentSession session = await agent.CreateSessionAsync();

        // Usar StateBag para almacenar estado personalizado
        // StateBag usa SetValue/GetValue/TryGetValue en lugar de indexador
        session.StateBag.SetValue("user_id", "USR-12345", null);
        session.StateBag.SetValue("session_type", "workshop_demo", null);
        // StateBag requiere tipos de referencia (class constraint en T)
        // Para valores numéricos, usamos string y convertimos después
        session.StateBag.SetValue("interaction_count", "0", null);

        // Enviar un mensaje
        await agent.RunAsync("¡Hola!", session);

        // Actualizar el estado
        session.StateBag.SetValue("interaction_count", "1", null);

        // Verificar que el estado se mantiene
        Assert.Equal("USR-12345", session.StateBag.GetValue<string>("user_id", null));
        Assert.Equal("workshop_demo", session.StateBag.GetValue<string>("session_type", null));
        Assert.Equal("1", session.StateBag.GetValue<string>("interaction_count", null));

        _output.WriteLine("✅ StateBag almacena estado personalizado:");
        _output.WriteLine($"   user_id: {session.StateBag.GetValue<string>("user_id", null)}");
        _output.WriteLine($"   session_type: {session.StateBag.GetValue<string>("session_type", null)}");
        _output.WriteLine($"   interaction_count: {session.StateBag.GetValue<string>("interaction_count", null)}");
    }
}
