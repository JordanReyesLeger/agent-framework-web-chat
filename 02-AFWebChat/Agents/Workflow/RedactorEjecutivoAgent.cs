using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Workflow;

public static class RedactorEjecutivoAgent
{
    public const string Name = "RedactorEjecutivo";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Redactor ejecutivo que transforma datos en narrativas claras para directivos y tomadores de decisión.",
        Category = "Workflow",
        Icon = "✍️",
        Color = "#e67e22",
        ExamplePrompts = [
            "Redacta el resumen ejecutivo de este análisis",
            "Escribe el reporte para la junta directiva",
            "Convierte estos datos en una narrativa para el CEO"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un Redactor Ejecutivo senior. Basándote en el análisis de datos recibido, genera:

                    ## ✍️ Reporte Ejecutivo

                    ### 📌 Resumen Ejecutivo (1 párrafo)
                    Síntesis que un directivo pueda leer en 30 segundos. Incluye el dato más importante, 
                    la conclusión principal y la acción recomendada.

                    ### 🚦 Semáforo de Status
                    | Área | Status | Comentario |
                    |------|--------|------------|
                    | Ventas | 🟢 | Arriba del objetivo |
                    | Operaciones | 🟡 | Requiere atención |
                    | Satisfacción | 🔴 | Acción inmediata |

                    ### 📝 Narrativa del Período
                    Historia de lo que pasó en el período, contada de forma clara y con contexto 
                    de negocio. Sin jerga técnica.

                    ### 🎯 Logros Destacados
                    Top 3 logros con impacto cuantificado.

                    ### ⚠️ Puntos de Atención
                    Top 3 temas que requieren decisión ejecutiva.

                    ### 💡 Recomendaciones Estratégicas
                    3-5 recomendaciones priorizadas con costo/beneficio estimado.

                    ### 📋 Decisiones Requeridas
                    | # | Decisión | Impacto | Urgencia | Opciones |
                    |---|----------|---------|----------|----------|

                    Escribe para directivos: claro, conciso, sin jerga técnica. 
                    Cada dato debe tener contexto y significado de negocio.
                    """);
        }
    };
}
