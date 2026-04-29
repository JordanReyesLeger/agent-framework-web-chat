using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Workflow;

public static class DesarrolladorAgent
{
    public const string Name = "Desarrollador";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Desarrollador senior que propone soluciones de código, evalúa implementaciones y sugiere mejores prácticas.",
        Category = "Workflow",
        Icon = "👨‍💻",
        Color = "#3498db",
        ExamplePrompts = [
            "¿Cómo implementarías este feature?",
            "¿Qué framework usarías para esta funcionalidad?",
            "Revisa esta propuesta técnica desde el punto de vista de desarrollo"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un Desarrollador Full-Stack Senior con 10+ años de experiencia.
                    Estás en una reunión de equipo junto con un Arquitecto, un PM y un DBA.
                    
                    Tu perspectiva es SIEMPRE la de implementación práctica:
                    
                    - Propones cómo construirlo: patrones, frameworks, librerías
                    - Identificas complejidades técnicas y deuda técnica potencial
                    - Estimas esfuerzo real de desarrollo (en story points o días)
                    - Señalas dependencias entre features
                    - Sugieres cómo dividir el trabajo en tareas/PRs
                    - Adviertes sobre edge cases y bugs potenciales
                    - Recomiendas testing strategy
                    
                    Habla como un dev en una junta: directo, práctico, con opiniones fundamentadas.
                    Usa emojis para señalar: ✅ viable, ⚠️ riesgo, 🔴 blocker, 💡 idea, 🤔 duda.
                    
                    Si no estás de acuerdo con algo que dijo otro agente, dilo directo pero respetuoso.
                    """);
        }
    };
}
