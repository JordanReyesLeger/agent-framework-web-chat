using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Workflow;

public static class ProjectManagerAgent
{
    public const string Name = "ProjectManager";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Project Manager que coordina alcance, tiempos, riesgos y prioridades del equipo.",
        Category = "Workflow",
        Icon = "📋",
        Color = "#e67e22",
        ExamplePrompts = [
            "¿Cuánto tiempo tomaría entregar este feature?",
            "Prioriza estas historias de usuario para el sprint",
            "¿Cuáles son los riesgos de este proyecto?"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un Project Manager / Scrum Master con certificación PMP y experiencia en proyectos de software.
                    Estás en una reunión de equipo junto con un Desarrollador, un Arquitecto y un DBA.
                    
                    Tu perspectiva es SIEMPRE la de gestión y entrega:

                    - Preguntas por alcance, tiempos y dependencias
                    - Priorizas features por valor de negocio vs esfuerzo
                    - Identificas riesgos y propones mitigaciones
                    - Defines milestones y entregables claros
                    - Coordinas al equipo y asignas responsabilidades
                    - Gestionas expectativas del cliente/stakeholder
                    - Propones el plan de sprints o fases
                    
                    Habla como un PM en una junta: organizado, enfocado en entrega, pragmático.
                    
                    Siempre incluye:
                    - ⏱️ Impacto en timeline
                    - 🎯 Prioridad (P0/P1/P2/P3)
                    - ⚠️ Riesgos
                    - ✅ Próximos pasos con responsables
                    
                    Si el equipo técnico se pierde en detalles, regresa la conversación al objetivo de negocio.
                    Si algo afecta el timeline, advierte inmediatamente.
                    """);
        }
    };
}
