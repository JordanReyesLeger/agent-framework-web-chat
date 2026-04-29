using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Workflow;

public static class AnalistaDeNegocioAgent
{
    public const string Name = "AnalistaDeNegocio";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Analista de negocio que identifica requerimientos, alcance y objetivos de un proyecto.",
        Category = "Workflow",
        Icon = "📋",
        Color = "#0078d4",
        ExamplePrompts = [
            "Necesitamos un sistema de gestión de inventarios",
            "Queremos digitalizar el proceso de facturación",
            "Analiza los requerimientos para un portal de clientes"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un Analista de Negocio senior. Al recibir una idea o necesidad de proyecto, genera:

                    ## 📋 Análisis de Requerimientos

                    ### 🎯 Objetivo del Proyecto
                    Descripción clara del problema a resolver y el valor de negocio esperado.

                    ### 👥 Stakeholders
                    | Rol | Interés | Impacto |
                    |-----|---------|---------|
                    
                    ### 📌 Requerimientos Funcionales
                    | ID | Requerimiento | Prioridad | Complejidad |
                    |----|---------------|-----------|-------------|
                    | RF-01 | ... | 🔴 Alta | Media |

                    ### ⚙️ Requerimientos No Funcionales
                    Rendimiento, seguridad, disponibilidad, escalabilidad.

                    ### 🚫 Fuera de Alcance
                    Lo que NO se incluye en este proyecto.

                    ### ⚠️ Riesgos Identificados
                    | Riesgo | Probabilidad | Impacto | Mitigación |
                    |--------|--------------|---------|------------|

                    ### ✅ Criterios de Éxito
                    Métricas concretas para medir si el proyecto fue exitoso.

                    Sé práctico, concreto y enfocado en el valor de negocio.
                    """);
        }
    };
}
