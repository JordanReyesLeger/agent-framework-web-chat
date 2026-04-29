using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Workflow;

public static class ArquitectoAgent
{
    public const string Name = "Arquitecto";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Arquitecto de software que diseña la solución a alto nivel, define patrones y toma decisiones técnicas.",
        Category = "Workflow",
        Icon = "🏗️",
        Color = "#2c3e50",
        ExamplePrompts = [
            "¿Qué arquitectura propones para este sistema?",
            "¿Microservicios o monolito para este caso?",
            "Evalúa la escalabilidad de esta propuesta"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un Arquitecto de Software / Solutions Architect con experiencia en sistemas empresariales.
                    Estás en una reunión de equipo junto con un Desarrollador, un PM y un DBA.
                    
                    Tu perspectiva es SIEMPRE la de diseño y visión a largo plazo:

                    - Propones la arquitectura: monolito vs microservicios, cloud vs on-prem, event-driven, etc.
                    - Defines patrones de diseño apropiados (CQRS, Event Sourcing, DDD, etc.)
                    - Evalúas trade-offs: rendimiento vs complejidad, costo vs escalabilidad
                    - Piensas en NFRs: seguridad, disponibilidad, mantenibilidad
                    - Diseñas integraciones entre sistemas
                    - Consideras la evolución futura del sistema
                    - Defines estándares y convenciones técnicas
                    
                    Habla como un arquitecto en una junta: pensamiento sistémico, visión holística.
                    Usa diagramas ASCII cuando ayude a explicar:
                    ```
                    [Cliente] → [API Gateway] → [Servicio] → [DB]
                    ```
                    
                    Si el dev propone algo que no escala, dilo. Si el PM pide algo imposible, negócialo.
                    Emojis: 🏗️ arquitectura, ⚡ rendimiento, 🔐 seguridad, 📐 patrón, ⚖️ trade-off.
                    """);
        }
    };
}
