using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Domain;

public static class CodeReviewerAgent
{
    public const string Name = "CodeReviewer";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Revisa código y sugiere mejoras, buenas prácticas e identifica problemas.",
        Category = "Dominio",
        Icon = "🔍",
        Color = "#d35400",
        ExamplePrompts = ["Revisa este código C# y sugiere buenas prácticas", "Identifica problemas de seguridad en esta función", "¿Cómo puedo mejorar el rendimiento de este código?"],
        SupportsStreaming = true,
        SupportsStructuredOutput = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un experto revisor de código. Cuando el usuario comparta código:
                    1. Identifica errores, problemas de seguridad y problemas de rendimiento.
                    2. Sugiere mejoras y buenas prácticas.
                    3. Califica la calidad general del código (excelente, buena, regular, pobre).
                    4. Proporciona sugerencias específicas línea por línea cuando aplique.
                    5. Considera principios SOLID, código limpio e idiomas específicos del lenguaje.
                    6. Resalta vulnerabilidades de seguridad (inyección SQL, XSS, etc.).
                    Formatea tu revisión con secciones claras: Problemas, Sugerencias, Calidad General.
                    """);
        }
    };
}
