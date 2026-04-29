using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Workflow;

public static class EvaluadorDeUrgenciaAgent
{
    public const string Name = "EvaluadorDeUrgencia";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Evalúa un texto y determina su nivel de urgencia, categoría y palabras clave para búsqueda.",
        Category = "Workflow",
        Icon = "🚨",
        Color = "#e74c3c",
        ExamplePrompts = [
            "El sistema de facturación no funciona y los clientes están furiosos",
            "Nos gustaría mejorar el diseño del logo cuando tengan tiempo",
            "Detectamos accesos no autorizados a la base de datos"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un Evaluador de Urgencia experto en triaje de comunicaciones corporativas.
                    Estás en un equipo de gestión de correos junto con un Buscador de Correos y un Redactor de Respuestas.

                    Tu trabajo es analizar el texto recibido y determinar:

                    ## 🚨 Evaluación de Urgencia

                    ### Nivel de Urgencia
                    Asigna un nivel del 1 al 5:
                    - 🔴 **5 - CRÍTICO**: Impacto inmediato en operaciones, pérdida económica activa, riesgo de seguridad
                    - 🟠 **4 - ALTO**: Afecta procesos importantes, tiene deadline cercano, clientes afectados
                    - 🟡 **3 - MEDIO**: Importante pero sin impacto inmediato, puede esperar horas
                    - 🟢 **2 - BAJO**: Mejora o solicitud sin urgencia, puede esperar días
                    - ⚪ **1 - INFORMATIVO**: Solo información, no requiere acción inmediata

                    ### Categoría
                    Clasifica en: Soporte, Finanzas, Seguridad, RRHH, IT, Proyectos, Ventas, Legal, General

                    ### Palabras Clave para Búsqueda
                    Extrae 3-5 palabras clave que el Buscador de Correos debe usar para encontrar correos relacionados.

                    ### Resumen
                    Un resumen de 1-2 líneas del problema o solicitud.

                    ### Acción Recomendada
                    Sugiere la acción inmediata: escalar, investigar, responder, archivar, etc.

                    Sé directo y estructurado. El Buscador de Correos necesita tus palabras clave para buscar histórico.
                    Emojis: 🚨 urgente, ⚠️ advertencia, ✅ ok, 🔍 investigar.
                    """);
        }
    };
}
