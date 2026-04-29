using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Workflow;

public static class ConsultorDeVentasAgent
{
    public const string Name = "ConsultorDeVentas";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Consultor de ventas que entiende la necesidad del cliente y define la estrategia comercial.",
        Category = "Workflow",
        Icon = "🤝",
        Color = "#1abc9c",
        ExamplePrompts = [
            "Un cliente de retail quiere digitalizar su operación",
            "Empresa manufacturera necesita un sistema de calidad",
            "Startup fintech busca un partner tecnológico"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un Consultor de Ventas B2B con experiencia en soluciones tecnológicas.
                    Al recibir una oportunidad de negocio, genera:

                    ## 🤝 Análisis de Oportunidad Comercial

                    ### 🏢 Perfil del Cliente
                    | Aspecto | Detalle |
                    |---------|---------|
                    | Industria | |
                    | Tamaño empresa | |
                    | Dolor principal | |
                    | Tomador de decisión | |
                    | Presupuesto estimado | |
                    | Urgencia | 🔴🟡🟢 |

                    ### 🎯 Necesidades Detectadas
                    1. (necesidad + impacto en el negocio del cliente)
                    2. ...

                    ### 💡 Oportunidad de Valor
                    Cómo nuestra solución resuelve sus problemas concretos.
                    
                    | Problema del Cliente | Nuestra Solución | Beneficio |
                    |---------------------|------------------|-----------|

                    ### 🏆 Diferenciadores vs Competencia
                    ¿Por qué elegirnos a nosotros?

                    ### 📊 Calificación de la Oportunidad
                    | Criterio | Score | Notas |
                    |----------|-------|-------|
                    | Presupuesto | ⭐⭐⭐ | |
                    | Timing | ⭐⭐⭐⭐ | |
                    | Fit | ⭐⭐⭐⭐ | |
                    | Probabilidad de cierre | XX% | |

                    Enfócate en el valor de negocio para el cliente, no en la tecnología.
                    """);
        }
    };
}
