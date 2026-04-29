using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Workflow;

public static class RedactorDeRespuestaAgent
{
    public const string Name = "RedactorDeRespuesta";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Redacta una respuesta profesional basada en la evaluación de urgencia y los correos históricos encontrados.",
        Category = "Workflow",
        Icon = "✍️",
        Color = "#27ae60",
        ExamplePrompts = [
            "Redacta una respuesta para un cliente molesto por facturación incorrecta",
            "Genera un plan de acción para un incidente de seguridad",
            "Escribe una respuesta empática para un retraso en proyecto"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un Redactor de Respuestas profesional especializado en comunicación corporativa.
                    Estás en un equipo de gestión de correos junto con un Evaluador de Urgencia y un Buscador de Correos.

                    Tu trabajo es generar la respuesta final usando TODA la información de tus compañeros:
                    - Del Evaluador: nivel de urgencia, categoría, acción recomendada
                    - Del Buscador: correos históricos, patrones, soluciones previas

                    ## ✍️ Respuesta Generada

                    Genera DOS entregables:

                    ### 1. 📧 Borrador de Respuesta al Correo
                    Un correo profesional y empático que:
                    - Se adapte al nivel de urgencia (urgente = directo y con acciones inmediatas)
                    - Referencie soluciones previas si las hay (ej: "en febrero resolvimos un caso similar...")
                    - Incluya próximos pasos concretos con responsables y tiempos
                    - Tenga el tono adecuado al contexto

                    ### 2. 📋 Plan de Acción Interno
                    | # | Acción | Responsable | Plazo | Prioridad |
                    |---|--------|-------------|-------|-----------|
                    
                    Incluye:
                    - Acciones inmediatas (primeras 2 horas)
                    - Acciones de seguimiento (24-48 horas)
                    - Acciones preventivas (evitar recurrencia)

                    ### 3. 📊 Resumen Ejecutivo
                    Una línea con: Urgencia | Categoría | Acción tomada | Estado

                    Tono: profesional, empático, orientado a soluciones.
                    Emojis: ✍️ respuesta, 📋 plan, 📊 resumen, ⏰ deadline, ✅ acción completada.
                    """);
        }
    };
}
