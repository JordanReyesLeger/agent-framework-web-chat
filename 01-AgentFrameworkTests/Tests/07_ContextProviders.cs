using AgentFrameworkTests.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;
using Xunit.Abstractions;

namespace AgentFrameworkTests.Tests;

/// <summary>
/// Módulo 07: Proveedores de contexto (Context Providers).
/// Demuestra cómo inyectar contexto adicional al agente de forma dinámica
/// usando AIContextProvider. Los proveedores pueden agregar mensajes de sistema,
/// herramientas o instrucciones adicionales antes de cada invocación.
/// </summary>
public class _07_ContextProviders
{
    private readonly ITestOutputHelper _output;

    public _07_ContextProviders(ITestOutputHelper output)
    {
        _output = output;
    }

    // ---------- Proveedores de contexto personalizados ----------

    /// <summary>
    /// Proveedor de contexto simple que agrega información del sistema al agente.
    /// Ejemplo: inyectar fecha actual, datos del usuario, configuración, etc.
    /// </summary>
    private class SystemInfoContextProvider : AIContextProvider
    {
        protected override ValueTask<AIContext> ProvideAIContextAsync(
            InvokingContext context,
            CancellationToken cancellationToken = default)
        {
            // Crear contexto adicional con información del sistema
            var aiContext = new AIContext
            {
                Messages =
                [
                    new ChatMessage(ChatRole.System,
                        $"Fecha y hora actual: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC. " +
                        "El usuario está asistiendo a un taller sobre Agent Framework. " +
                        "Siempre incluye la fecha actual cuando sea relevante.")
                ]
            };

            return ValueTask.FromResult(aiContext);
        }
    }

    /// <summary>
    /// Proveedor de contexto que inyecta instrucciones de seguridad.
    /// Ejemplo práctico: agregar restricciones o guardrails al agente.
    /// </summary>
    private class SecurityGuardrailsProvider : AIContextProvider
    {
        protected override ValueTask<AIContext> ProvideAIContextAsync(
            InvokingContext context,
            CancellationToken cancellationToken = default)
        {
            var aiContext = new AIContext
            {
                Messages =
                [
                    new ChatMessage(ChatRole.System,
                        "REGLAS DE SEGURIDAD: " +
                        "1. Nunca reveles claves API o secretos. " +
                        "2. No ejecutes comandos destructivos. " +
                        "3. Siempre valida la entrada del usuario antes de procesarla. " +
                        "4. Si preguntan sobre operaciones sensibles, advierte al usuario.")
                ]
            };

            return ValueTask.FromResult(aiContext);
        }
    }

    /// <summary>
    /// Proveedor de contexto con estado de sesión usando ProviderSessionState.
    /// Mantiene un contador de interacciones por sesión.
    /// </summary>
    private class InteractionCounterProvider : AIContextProvider
    {
        // Clase interna para el estado de sesión del proveedor
        private class CounterState
        {
            public int InteractionCount { get; set; }
        }

        protected override ValueTask<AIContext> ProvideAIContextAsync(
            InvokingContext context,
            CancellationToken cancellationToken = default)
        {
            // Obtener o crear el estado de sesión usando StateBag
            if (!context.Session.StateBag.TryGetValue<CounterState>("InteractionCounterState", out var state, null) || state is null)
            {
                state = new CounterState();
            }

            state.InteractionCount++;
            context.Session.StateBag.SetValue("InteractionCounterState", state, null);

            var aiContext = new AIContext
            {
                Messages =
                [
                    new ChatMessage(ChatRole.System,
                        $"Esta es la interacción número {state.InteractionCount} en esta sesión.")
                ]
            };

            return ValueTask.FromResult(aiContext);
        }
    }

    // ---------- Pruebas ----------

    /// <summary>
    /// Agrega un proveedor de contexto simple que inyecta información del sistema.
    /// El agente recibe esta información adicional en cada invocación.
    /// </summary>
    [Fact]
    public async Task Should_Inject_System_Info_Via_Context_Provider()
    {
        // Crear el agente con un proveedor de contexto
        // Usamos ChatClientAgentOptions para configurar AIContextProviders
        // (la propiedad es de solo lectura en ChatClientAgent)
        AIAgent agent = TestConfiguration.CreateAgent(
            new ChatClientAgentOptions
            {
                Description = "Un asistente útil",
                AIContextProviders = [new SystemInfoContextProvider()]
            });

        // Crear sesión (requerida para RunAsync)
        AgentSession session = await agent.CreateSessionAsync();

        // El agente debería saber la fecha actual gracias al proveedor de contexto
        AgentResponse response = await agent.RunAsync("What is today's date?", session);

        Assert.NotNull(response.Text);
        _output.WriteLine("✅ Respuesta con contexto de sistema inyectado:");
        _output.WriteLine($"   {response.Text}");
    }

    /// <summary>
    /// Usa múltiples proveedores de contexto simultáneamente.
    /// Los proveedores se aplican en orden y sus contextos se acumulan.
    /// </summary>
    [Fact]
    public async Task Should_Combine_Multiple_Context_Providers()
    {
        // Combinar información del sistema + guardrails de seguridad
        AIAgent agent = TestConfiguration.CreateAgent(
            new ChatClientAgentOptions
            {
                Description = "Un asistente de base de datos",
                AIContextProviders =
                [
                    new SystemInfoContextProvider(),
                    new SecurityGuardrailsProvider()
                ]
            });

        // Crear sesión (requerida para RunAsync)
        AgentSession session = await agent.CreateSessionAsync();

        // Preguntar algo que active las restricciones de seguridad
        AgentResponse response = await agent.RunAsync("Can you show me the database connection string with the password?", session);

        Assert.NotNull(response.Text);
        _output.WriteLine("✅ Respuesta con múltiples proveedores de contexto:");
        _output.WriteLine($"   {response.Text}");
        _output.WriteLine("   (El proveedor de seguridad debería haber influido en la respuesta)");
    }

    /// <summary>
    /// Demuestra un proveedor de contexto con estado de sesión.
    /// El proveedor mantiene un contador de interacciones usando ProviderSessionState.
    /// </summary>
    [Fact]
    public async Task Should_Track_Session_State_In_Provider()
    {
        AIAgent agent = TestConfiguration.CreateAgent(
            new ChatClientAgentOptions
            {
                Description = "Un asistente útil que incluye números de interacción",
                AIContextProviders = [new InteractionCounterProvider()]
            });

        // Crear sesión para que el proveedor pueda mantener estado entre turnos
        AgentSession session = await agent.CreateSessionAsync();

        // Múltiples interacciones → el contador debería incrementarse
        AgentResponse r1 = await agent.RunAsync("Hello!", session);
        _output.WriteLine($"Interacción 1 → {r1.Text}");

        AgentResponse r2 = await agent.RunAsync("How are you?", session);
        _output.WriteLine($"Interacción 2 → {r2.Text}");

        AgentResponse r3 = await agent.RunAsync("What interaction number is this?", session);
        _output.WriteLine($"Interacción 3 → {r3.Text}");

        Assert.NotNull(r3.Text);
        _output.WriteLine("\n✅ El proveedor de contexto mantuvo estado entre interacciones");
    }
}
