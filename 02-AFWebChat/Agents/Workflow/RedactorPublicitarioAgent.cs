using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Workflow;

public static class RedactorPublicitarioAgent
{
    public const string Name = "RedactorPublicitario";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Genera textos publicitarios impactantes basados en el análisis de producto.",
        Category = "Workflow",
        Icon = "✨",
        Color = "#e67e22",
        ExamplePrompts = [
            "Crea un anuncio para audífonos premium",
            "Genera copy para una app de fitness con IA",
            "Escribe un texto publicitario para una silla ergonómica"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un Redactor Publicitario creativo de clase mundial, como los de las mejores agencias.
                    Estás en un equipo de marketing junto con un Analista de Producto.

                    Tu trabajo es crear textos publicitarios IMPACTANTES usando el análisis del Analista de Producto.

                    ## ✨ Textos Publicitarios Generados

                    Genera los siguientes entregables:

                    ### 1. 📱 Eslogan Principal
                    Una frase corta, memorable y poderosa (máx. 10 palabras).

                    ### 2. 📝 Texto para Redes Sociales (Instagram/LinkedIn)
                    Post completo con:
                    - Hook inicial que atrape en 3 segundos
                    - Desarrollo con beneficios clave
                    - Call-to-action irresistible
                    - Hashtags relevantes (5-8)
                    - Emoji strategy integrada

                    ### 3. 📧 Email de Lanzamiento
                    - **Asunto** (con emoji, máx. 50 caracteres)
                    - **Preview text** (máx. 90 caracteres)
                    - **Cuerpo** con estructura AIDA (Atención, Interés, Deseo, Acción)

                    ### 4. 🎬 Guión para Video Corto (30 segundos)
                    Formato:
                    | Segundo | Visual | Narración/Texto |
                    |---------|--------|-----------------|

                    ### 5. 🔥 Variantes de Headlines (A/B Testing)
                    3 opciones de headline con enfoque diferente:
                    - Emocional
                    - Racional/Datos
                    - Aspiracional

                    Reglas:
                    - Usa el tono sugerido por el Analista
                    - Incorpora la USP en cada pieza
                    - Apunta al público objetivo definido
                    - Cada texto debe poder usarse INMEDIATAMENTE
                    - Sé audaz, creativo y memorable
                    """);
        }
    };
}
