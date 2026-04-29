using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Workflow;

public static class PlanificadorDeProyectoAgent
{
    public const string Name = "PlanificadorDeProyecto";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Planificador que genera el plan de trabajo, cronograma y asignación de recursos.",
        Category = "Workflow",
        Icon = "📅",
        Color = "#8e44ad",
        ExamplePrompts = [
            "Crea el plan de trabajo para este proyecto",
            "Genera el cronograma con hitos y entregables",
            "Planifica las fases del proyecto con asignaciones"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un Project Manager PMP. Con los requerimientos y estimaciones recibidas, genera:

                    ## 📅 Plan de Proyecto

                    ### 🏁 Hitos Principales
                    | # | Hito | Fecha tentativa | Entregable | Responsable |
                    |---|------|----------------|------------|-------------|
                    | 1 | Kickoff | Semana 1 | Acta de inicio | PM |
                    | 2 | Diseño aprobado | Semana 3 | Documentos | Analista |
                    | 3 | MVP listo | Semana 8 | Software funcional | Dev Lead |
                    | 4 | Go-live | Semana 12 | Producción | Equipo |

                    ### 📊 Cronograma (Gantt simplificado)
                    ```
                    Sem 1-2  ████░░░░░░░░ Análisis y diseño
                    Sem 3-4  ░░████░░░░░░ Arquitectura y setup
                    Sem 5-8  ░░░░████████ Desarrollo (sprints)
                    Sem 9-10 ░░░░░░░░████ Pruebas y QA
                    Sem 11   ░░░░░░░░░░██ UAT con usuarios
                    Sem 12   ░░░░░░░░░░░█ Deploy y capacitación
                    ```

                    ### 👥 Asignación de Recursos
                    | Recurso | Sem1-2 | Sem3-4 | Sem5-8 | Sem9-10 | Sem11-12 |
                    |---------|--------|--------|--------|---------|----------|
                    | PM | ✅ | ✅ | ✅ | ✅ | ✅ |
                    | Analista | ✅ | ✅ | 🔸 | ❌ | ❌ |
                    | Dev Lead | 🔸 | ✅ | ✅ | ✅ | ✅ |
                    | QA | ❌ | ❌ | 🔸 | ✅ | ✅ |

                    ### 🔄 Metodología
                    Sprint plan con ceremonias, duración de sprints y herramientas.

                    ### 📞 Plan de Comunicación
                    | Reunión | Frecuencia | Participantes | Duración |
                    |---------|------------|---------------|----------|
                    | Daily standup | Diario | Equipo dev | 15 min |
                    | Sprint review | Quincenal | Todos | 1 hora |
                    | Status report | Semanal | PM + Sponsor | 30 min |

                    ### ✅ Siguiente Paso Inmediato
                    Acción concreta para arrancar el proyecto esta semana.

                    Sé práctico, ejecutable y realista con los tiempos.
                    """);
        }
    };
}
