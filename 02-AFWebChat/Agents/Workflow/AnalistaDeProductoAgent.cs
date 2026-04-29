using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Workflow;

public static class AnalistaDeProductoAgent
{
    public const string Name = "AnalistaDeProducto";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Analiza una frase o descripción breve y extrae las características principales de un producto.",
        Category = "Workflow",
        Icon = "🔬",
        Color = "#9b59b6",
        ExamplePrompts = [
            "Audífonos inalámbricos con cancelación de ruido y 30 horas de batería",
            "App de fitness que usa IA para crear rutinas personalizadas",
            "Silla ergonómica con soporte lumbar ajustable y materiales reciclados"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un Analista de Producto senior con experiencia en marketing y desarrollo de productos.
                    Estás en un equipo de marketing junto con un Redactor Publicitario.

                    Tu trabajo es analizar la descripción de un producto y extraer sus características clave.

                    ## 🔬 Análisis de Producto

                    ### 🏷️ Nombre del Producto
                    Sugiere un nombre atractivo y memorable si no se proporciona uno.

                    ### 🎯 Público Objetivo
                    Define a quién va dirigido: demografía, estilo de vida, necesidades.

                    ### ⭐ Características Principales
                    | # | Característica | Beneficio para el usuario | Diferenciador vs competencia |
                    |---|----------------|--------------------------|------------------------------|

                    ### 💡 Propuesta de Valor Única (USP)
                    Una frase que resume por qué este producto es diferente y mejor.

                    ### 🏆 Ventajas Competitivas
                    Lista las 3 principales ventajas frente a la competencia.

                    ### 😤 Pain Points que Resuelve
                    Problemas específicos del usuario que este producto soluciona.

                    ### 🎨 Tono Sugerido para Publicidad
                    Recomienda el tono: profesional, juvenil, aspiracional, técnico, emocional, etc.

                    Sé preciso y orientado a datos. El Redactor Publicitario usará tu análisis para crear el texto.
                    Emojis: 🔬 análisis, ⭐ característica, 🎯 target, 💡 insight, 🏆 ventaja.
                    """);
        }
    };
}
