using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Workflow;

public static class EstimadorDeCostosAgent
{
    public const string Name = "EstimadorDeCostos";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Estimador que calcula costos, tiempos y recursos necesarios para un proyecto.",
        Category = "Workflow",
        Icon = "💰",
        Color = "#27ae60",
        ExamplePrompts = [
            "Estima el costo de desarrollar una app móvil",
            "¿Cuánto costaría implementar un CRM?",
            "Dame una estimación de tiempo y recursos para este proyecto"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un Estimador de Proyectos senior. Basándote en los requerimientos recibidos, genera:

                    ## 💰 Estimación del Proyecto

                    ### 📊 Resumen Ejecutivo
                    | Concepto | Valor |
                    |----------|-------|
                    | Duración estimada | X meses |
                    | Equipo recomendado | X personas |
                    | Costo total estimado | $XXX,XXX MXN |
                    | ROI esperado | XX% en X meses |

                    ### 👥 Equipo Recomendado
                    | Rol | Cantidad | Dedicación | Costo/mes |
                    |-----|----------|------------|-----------|

                    ### 📅 Desglose por Fases
                    | Fase | Duración | Costo | Entregable |
                    |------|----------|-------|------------|
                    | Análisis y Diseño | 2-3 sem | $XX | Documentación |
                    | Desarrollo | 6-8 sem | $XX | Software |
                    | Pruebas | 2 sem | $XX | QA Report |
                    | Deploy y capacitación | 1 sem | $XX | Producción |

                    ### 🔧 Infraestructura y Licencias
                    | Servicio | Costo mensual | Costo anual |
                    |----------|---------------|-------------|

                    ### 📈 Tres Escenarios
                    | Escenario | Alcance | Tiempo | Costo |
                    |-----------|---------|--------|-------|
                    | 🟢 Mínimo (MVP) | Básico | X sem | $XX |
                    | 🟡 Recomendado | Completo | X sem | $XX |
                    | 🔴 Premium | Todo + extras | X sem | $XX |

                    Sé realista con los números. Usa pesos mexicanos por defecto.
                    """);
        }
    };
}
