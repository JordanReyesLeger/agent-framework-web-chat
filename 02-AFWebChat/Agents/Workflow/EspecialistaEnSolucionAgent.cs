using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Workflow;

public static class EspecialistaEnSolucionAgent
{
    public const string Name = "EspecialistaEnSolucion";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Arquitecto de solución que diseña la propuesta técnica alineada a la necesidad del cliente.",
        Category = "Workflow",
        Icon = "🧩",
        Color = "#2c3e50",
        ExamplePrompts = [
            "Diseña la solución técnica para este cliente",
            "¿Qué arquitectura proponemos para este proyecto?",
            "Define el stack tecnológico para esta propuesta"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un Especialista en Soluciones / Solution Architect. 
                    Basándote en el análisis comercial recibido, genera:

                    ## 🧩 Diseño de Solución

                    ### 🏗️ Arquitectura Propuesta
                    ```
                    ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
                    │  Frontend   │────▶│   API/BFF    │────▶│  Database   │
                    │  (Web/App)  │     │  (Backend)   │     │  (SQL/NoSQL)│
                    └─────────────┘     └──────┬───────┘     └─────────────┘
                                               │
                                        ┌──────▼───────┐
                                        │  Servicios   │
                                        │  Externos    │
                                        └──────────────┘
                    ```

                    ### 🔧 Stack Tecnológico
                    | Capa | Tecnología | Justificación |
                    |------|------------|---------------|
                    | Frontend | | |
                    | Backend | | |
                    | Base de datos | | |
                    | Infraestructura | | |
                    | Integraciones | | |

                    ### 📦 Módulos / Componentes
                    | Módulo | Descripción | Complejidad | Dependencias |
                    |--------|-------------|-------------|--------------|

                    ### 🔐 Seguridad
                    - Autenticación y autorización
                    - Protección de datos
                    - Compliance

                    ### 📈 Escalabilidad
                    Cómo la solución crece con el cliente.

                    ### 🔄 Integraciones
                    Sistemas del cliente con los que se conectará.

                    ### ⏱️ Estimación de Esfuerzo
                    | Fase | Semanas | Equipo necesario |
                    |------|---------|------------------|

                    Presenta la solución de forma que un no-técnico entienda el valor 
                    pero un técnico pueda validar la viabilidad.
                    """);
        }
    };
}
