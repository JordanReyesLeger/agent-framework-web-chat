# 🧪 Workshop: Microsoft Agent Framework — Pruebas Unitarias

> Proyecto de demostración con pruebas unitarias xUnit que cubren las funcionalidades principales del **Microsoft Agent Framework** (`Microsoft.Agents.AI.OpenAI` y `Microsoft.Agents.AI.Workflows`).

## 📋 Requisitos previos

- **.NET 9.0 SDK**
- **Azure OpenAI** con un deployment de GPT-4o (o compatible)
- Configurar `appsettings.json` con los datos de conexión:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://tu-recurso.openai.azure.com/",
    "ApiKey": "tu-api-key",
    "DeploymentName": "gpt-4o"
  }
}
```

## 🚀 Ejecución

```bash
# Ejecutar todas las pruebas
dotnet test

# Ejecutar un módulo específico (ejemplo: módulo 03)
dotnet test --filter "FullyQualifiedName~_03_"

# Ejecutar una prueba específica
dotnet test --filter "Should_Use_Single_Function_Tool"

# Ver salida detallada con los logs de cada prueba
dotnet test --logger "console;verbosity=detailed"
```

---

## 📚 Módulos

### Módulo 00 — Creación Básica de Agentes
**Archivo:** `Tests/00_BasicAgentCreation.cs`

Muestra las diferentes formas de crear un agente con `AIAgent`:
- **Constructor directo** usando la extensión `.AsAIAgent()` sobre `ChatClient`
- **Constructor con opciones** usando `ChatClientAgentOptions` para configurar nombre, descripción y opciones de chat
- **Verificación de respuesta** — crear un agente y obtener una respuesta básica

| Prueba | Descripción |
|--------|-------------|
| `Should_Create_Agent_With_AsAIAgent_Extension` | Crea agente con la extensión simplificada |
| `Should_Create_Agent_With_Custom_Options` | Crea agente con ChatClientAgentOptions personalizado |
| `Should_Create_Agent_And_Get_Basic_Response` | Crea agente y verifica que responde correctamente |

---

### Módulo 01 — Ejecución de Agentes
**Archivo:** `Tests/01_RunningAgents.cs`

Demuestra los modos de ejecución de un agente:
- **RunAsync** — ejecución completa que retorna `AgentResponse` con propiedad `.Text`
- **RunStreamingAsync** — ejecución en streaming que retorna `AgentResponseUpdate` con propiedad `.Text`
- **RunOptions** — configuración personalizada con `ChatClientAgentRunOptions`

| Prueba | Descripción |
|--------|-------------|
| `Should_Run_Agent_And_Get_Complete_Response` | Ejecución completa con `RunAsync` |
| `Should_Stream_Response_Token_By_Token` | Streaming token por token con `RunStreamingAsync` |
| `Should_Run_Agent_With_Custom_Run_Options` | Ejecución con opciones personalizadas de chat |

---

### Módulo 02 — Salida Estructurada
**Archivo:** `Tests/02_StructuredOutput.cs`

Demuestra cómo obtener respuestas como objetos tipados usando `RunAsync<T>`:
- El framework configura automáticamente el esquema JSON a partir del tipo `T`
- No se necesita pasar `ChatOptions` ni `ResponseFormat` manualmente
- La respuesta es `AgentResponse<T>` con propiedad `.Result`

| Prueba | Descripción |
|--------|-------------|
| `Should_Return_Structured_Sentiment_Analysis` | Extrae análisis de sentimiento como objeto tipado |
| `Should_Return_Structured_Nested_Object` | Extrae objetos anidados complejos (persona con dirección) |

---

### Módulo 03 — Herramientas de Función (Function Tools)
**Archivo:** `Tests/03_FunctionTools.cs`

Muestra cómo extender las capacidades del agente con herramientas:
- Herramientas definidas como métodos estáticos con `[Description]`
- Selección automática de herramienta según el contexto
- Lambdas inline como herramientas rápidas

| Prueba | Descripción |
|--------|-------------|
| `Should_Use_Single_Function_Tool` | Agente usa una herramienta del clima |
| `Should_Select_Appropriate_Tool_From_Multiple` | Agente selecciona entre calculadora y clima |
| `Should_Use_Inline_Lambda_Tools` | Herramientas definidas como lambdas |
| `Should_Use_Lights_Plugin_Tools` | Agente controla luces con LightsPlugin (listar y encender/apagar) |

---

### Módulo 04 — Aprobación de Herramientas (Human-in-the-Loop)
**Archivo:** `Tests/04_ToolApproval.cs`

Demuestra el patrón Human-in-the-Loop donde ciertas herramientas requieren aprobación antes de ejecutarse:
- Uso de `RequiresConfirmation` en las herramientas
- Manejo de `AgentConversationToolCall` para aprobar/rechazar
- Mezcla de herramientas normales y con aprobación

| Prueba | Descripción |
|--------|-------------|
| `Should_Require_Approval_Before_Sending_Email` | Herramienta de email requiere confirmación |
| `Should_Mix_Normal_And_Approval_Tools` | Combina herramientas normales y con aprobación |

---

### Módulo 05 — Multimodal (Análisis de Imágenes)
**Archivo:** `Tests/05_Multimodal.cs`

Muestra cómo enviar imágenes al agente para análisis usando `ChatMessage` con contenido multimodal:
- Envío de URL de imagen como `UriContent`
- Combinación de texto e imagen en un solo mensaje

| Prueba | Descripción |
|--------|-------------|
| `Should_Analyze_Image_From_Url` | Analiza una imagen desde URL |
| `Should_Process_Text_And_Image_Together` | Procesa texto e imagen combinados |

---

### Módulo 06 — Conversaciones y Sesiones
**Archivo:** `Tests/06_ConversationsSessions.cs`

Demuestra la gestión de sesiones y estado:
- **AgentSession** — mantiene contexto conversacional entre mensajes
- **Serialización** — `SerializeSessionAsync` retorna `JsonElement`, `DeserializeSessionAsync` recibe `JsonElement`
- **StateBag** — almacén de estado personalizado con `SetValue<T>`/`GetValue<T>` (T debe ser tipo referencia)

| Prueba | Descripción |
|--------|-------------|
| `Should_Maintain_Conversation_Context_With_Session` | Verifica persistencia de contexto con sesión |
| `Should_Not_Remember_Without_Session` | Demuestra que sin sesión no hay memoria |
| `Should_Serialize_And_Deserialize_Session` | Serializa/deserializa sesión como JsonElement |
| `Should_Use_StateBag_For_Custom_Session_State` | Usa StateBag para estado personalizado |

---

### Módulo 07 — Proveedores de Contexto (Context Providers)
**Archivo:** `Tests/07_ContextProviders.cs`

Muestra cómo inyectar información contextual automáticamente en cada ejecución:
- Implementación de `IAIContextProvider` personalizado
- Se configuran via `ChatClientAgentOptions.AIContextProviders` (propiedad de solo lectura en el agente)
- Múltiples proveedores combinados

| Prueba | Descripción |
|--------|-------------|
| `Should_Inject_System_Info_Via_Context_Provider` | Inyecta fecha/hora del sistema |
| `Should_Combine_Multiple_Context_Providers` | Combina proveedor de sistema y guardrails de seguridad |
| `Should_Track_Session_State_In_Provider` | Proveedor que mantiene estado entre invocaciones |

---

### Módulo 08 — Pipeline y Middleware
**Archivo:** `Tests/08_AgentPipelineMiddleware.cs`

Demuestra el patrón pipeline para interceptar y modificar ejecuciones:
- **AIAgentBuilder.Use()** con delegado de 5 parámetros: `(messages, session, options, next, ct)`
- Middleware de logging, auditoría y metadata
- Encadenamiento de múltiples middleware

| Prueba | Descripción |
|--------|-------------|
| `Should_Execute_Middleware_Before_And_After` | Middleware que ejecuta lógica antes y después |
| `Should_Chain_Multiple_Middleware` | Cadena de middleware en orden |
| `Should_Use_Middleware_To_Track_With_Metadata` | Middleware para tracking con metadatos |

---

### Módulo 09 — Workflows: Ejecutores (Executors)
**Archivo:** `Tests/09_WorkflowsExecutors.cs`

Introduce los workflows con ejecutores — unidades de procesamiento que transforman datos:
- `Executor<TInput, TOutput>` — ejecutor con entrada y salida tipada
- `Executor<TInput>` con `[YieldsOutput]` — ejecutor terminal que genera salida del workflow
- `InProcessExecution.RunStreamingAsync` para ejecutar workflows
- `WorkflowEvent`, `ExecutorCompletedEvent`, `WorkflowOutputEvent` para monitoreo

| Prueba | Descripción |
|--------|-------------|
| `Should_Run_Sequential_Workflow_With_Two_Executors` | Pipeline secuencial de 2 ejecutores |
| `Should_Run_Three_Executor_Pipeline` | Pipeline de 3 ejecutores encadenados |
| `Should_Share_State_Between_Executors` | Ejecutores comparten estado vía WorkflowContext |

---

### Módulo 10 — Workflows: Edges y Enrutamiento Condicional
**Archivo:** `Tests/10_WorkflowsEdges.cs`

Demuestra enrutamiento en workflows:
- **Edge directo** — `AddEdge(from, to)`: flujo incondicional
- **Edge condicional** — `AddEdge<T>(from, to, condition:)`: bifurcación según resultado (requiere tipo explícito)
- `WithOutputFrom()` para definir múltiples puntos de salida

| Prueba | Descripción |
|--------|-------------|
| `Should_Route_Through_Direct_Edges` | Enrutamiento directo secuencial |
| `Should_Route_Conditionally_To_Positive_Handler` | Bifurcación condicional → ruta positiva |
| `Should_Route_Conditionally_To_Negative_Handler` | Bifurcación condicional → ruta negativa |

---

### Módulo 11 — Workflows: Eventos y Patrones Avanzados
**Archivo:** `Tests/11_WorkflowsEvents.cs`

Cubre patrones avanzados de workflows:
- Captura de eventos durante la ejecución (`ExecutorCompletedEvent`, `WorkflowOutputEvent`)
- Patrón loop — workflows que repiten ejecución hasta cumplir una condición
- Monitoreo detallado del flujo de datos entre ejecutores

| Prueba | Descripción |
|--------|-------------|
| `Should_Capture_Executor_Completed_Events` | Captura eventos de completado de cada ejecutor |
| `Should_Capture_Workflow_Output_Event` | Captura el evento de salida final del workflow |
| `Should_Execute_Loop_Until_Number_Found` | Workflow con bucle condicionado |

---

### Módulo 12 — Múltiples Agentes en una Ejecución
**Archivo:** `Tests/12_MultipleAgents.cs`

Demuestra la coordinación manual de múltiples agentes usando sesiones y `RunAsync`:
- **Secuencial** — un agente genera contenido y otro lo evalúa
- **Pipeline** — cadena de 3 agentes donde la salida de uno alimenta al siguiente
- **Fan-out** — múltiples agentes analizan el mismo input en paralelo

| Prueba | Descripción |
|--------|-------------|
| `Should_Run_Writer_And_Critic_Sequentially` | Escritor genera, Crítico evalúa secuencialmente |
| `Should_Run_Three_Agent_Pipeline` | Pipeline: Redactor → Traductor → Resumidor |
| `Should_Run_Multiple_Agents_On_Same_Input` | Fan-out: 3 agentes analizan el mismo mensaje |

---

### Módulo 13 — Agentes en Workflows (AgentWorkflowBuilder + WorkflowBuilder)
**Archivo:** `Tests/13_AgentsInWorkflows.cs`

Integra agentes AI dentro del framework de workflows de dos formas:

**Orquestación de alto nivel (`AgentWorkflowBuilder`):**
- **BuildSequential** — pipeline de agentes donde la salida fluye al siguiente
- **BuildConcurrent** — agentes en paralelo (fan-out) con agregación automática
- **CreateGroupChatBuilderWith** — group chat con `RoundRobinGroupChatManager`

**Workflow manual con edges (`WorkflowBuilder` + `BindAsExecutor`):**
- Convierte agentes AI en nodos del grafo con `agent.BindAsExecutor(AIAgentHostOptions)`
- Conecta nodos manualmente con `AddEdge(from, to)` — mismo patrón de los módulos 09-11
- Control total sobre la topología del grafo de agentes
- Usa `TurnToken(emitEvents: true)` para activar agentes y `AgentResponseUpdateEvent` para streaming

**Enrutamiento condicional con agentes (`AddEdge<object>` + clausura):**
- Combina el patrón de edges condicionales del módulo 10 con agentes AI como nodos
- Un agente clasificador analiza el input y los edges condicionales enrutan al agente correcto
- Usa `AddEdge<object>` con clausura para capturar el estado de clasificación entre mensajes
- `ForwardIncomingMessages = false` en el clasificador evita que reenvíe el input original antes de clasificar
- **Insight clave:** `AIAgentHostExecutor` envía `List<ChatMessage>` ANTES que `TurnToken` — la clausura captura la clasificación del primer mensaje y aplica la misma decisión al segundo

| Prueba | Descripción |
|--------|-------------|
| `Should_Run_Sequential_Agent_Workflow` | Pipeline secuencial: Escritor → Traductor |
| `Should_Run_Concurrent_Agent_Workflow` | Agentes concurrentes: Optimista ↔ Pesimista |
| `Should_Run_GroupChat_Agent_Workflow` | Group chat round-robin: Desarrollador ↔ Diseñador ↔ Gerente |
| `Should_Run_Agent_Workflow_With_Manual_Edges` | Workflow manual con edges: Investigador → Resumidor vía `BindAsExecutor` + `AddEdge` |
| `Should_Route_Conditionally_To_Negative_Agent` | Enrutamiento condicional: Clasificador → Optimista / Pesimista vía `AddEdge<object>` con condición |

---

## 🏗️ Estructura del proyecto

```
01-AgentFrameworkTests/
├── 01-AgentFrameworkTests.csproj   # Proyecto xUnit con dependencias
├── WORKSHOP.md                     # Esta guía
├── Helpers/
│   └── TestConfiguration.cs        # Configuración de Azure OpenAI
├── Tests/
│   ├── 00_BasicAgentCreation.cs    # Creación de agentes
│   ├── 01_RunningAgents.cs         # Ejecución y streaming
│   ├── 02_StructuredOutput.cs      # Salida estructurada con RunAsync<T>
│   ├── 03_FunctionTools.cs         # Herramientas de función
│   ├── 04_ToolApproval.cs          # Aprobación Human-in-the-Loop
│   ├── 05_Multimodal.cs            # Análisis de imágenes
│   ├── 06_ConversationsSessions.cs # Sesiones y estado
│   ├── 07_ContextProviders.cs      # Proveedores de contexto
│   ├── 08_AgentPipelineMiddleware.cs # Pipeline y middleware
│   ├── 09_WorkflowsExecutors.cs    # Workflows: ejecutores
│   ├── 10_WorkflowsEdges.cs        # Workflows: edges condicionales
│   └── 11_WorkflowsEvents.cs       # Workflows: eventos y loops
│   ├── 12_MultipleAgents.cs        # Múltiples agentes coordinados
│   └── 13_AgentsInWorkflows.cs     # Agentes en workflows (AgentWorkflowBuilder)
└── appsettings.json                # Configuración Azure OpenAI
```

## 📦 Paquetes NuGet utilizados

| Paquete | Versión | Descripción |
|---------|---------|-------------|
| `Microsoft.Agents.AI.OpenAI` | 1.0.0-rc4 | Agentes con AIAgent (.AsAIAgent()) |
| `Microsoft.Agents.AI.Workflows` | 1.0.0-rc4 | Workflows, ejecutores y edges |
| `Microsoft.Extensions.AI.OpenAI` | 10.3.0 | Integración con Microsoft.Extensions.AI |
| `Azure.AI.OpenAI` | 2.2.0-beta.1 | Cliente Azure OpenAI |
| `Microsoft.Extensions.Configuration.Json` | 9.0.4 | Configuración desde JSON |
| `xunit` | 2.9.3 | Framework de pruebas |

## 🔑 Conceptos clave del API

| Concepto | Detalle |
|----------|---------|
| `AIAgent` | Agente principal — se crea con `ChatClient.AsAIAgent()` + opciones |
| `AgentSession` | Sesión conversacional — se crea con `agent.CreateSessionAsync()` |
| `AgentResponse.Text` | Texto de respuesta (no `.Message`) |
| `AgentResponse<T>.Result` | Resultado tipado (no `.Value`) |
| `AgentResponseUpdate.Text` | Token en streaming (no `.ContentUpdate`) |
| `RunAsync<T>` | Salida estructurada — el framework configura JSON schema automáticamente |
| `SerializeSessionAsync` | Retorna `JsonElement` (no `string`) |
| `StateBag.SetValue<T>` | T debe ser tipo referencia (`class`) |
| `AIContextProviders` | Solo lectura en `AIAgent` — configurar via `ChatClientAgentOptions` |
| `AIAgentBuilder.Use()` | Middleware con firma `(messages, session, options, next, ct)` |
| `AddEdge<T>` | Edge condicional requiere tipo explícito |
| `AgentWorkflowBuilder.BuildSequential` | Pipeline secuencial de agentes AI en un workflow |
| `AgentWorkflowBuilder.BuildConcurrent` | Agentes AI en paralelo (fan-out) con agregación |
| `AgentWorkflowBuilder.CreateGroupChatBuilderWith` | Group chat con gestor de turnos (ej. round-robin) |
| `TurnToken(emitEvents: true)` | Activa procesamiento de agentes y emite eventos de streaming |
| `AgentResponseUpdateEvent` | Evento de streaming parcial de texto del agente |
| `RoundRobinGroupChatManager` | Gestor de turnos round-robin para group chat |
| `BindAsExecutor(AIAgentHostOptions)` | Convierte un `AIAgent` en `ExecutorBinding` compatible con `WorkflowBuilder.AddEdge()` |
| `AIAgentHostOptions` | Opciones para alojar agentes en workflows: `ReassignOtherAgentsAsUsers`, `ForwardIncomingMessages` |
| `AddEdge<object>` con clausura | Patrón para enrutamiento condicional con agentes: captura estado de clasificación de `List<ChatMessage>` y lo aplica a `TurnToken` |
| `ForwardIncomingMessages = false` | Evita que el agente reenvíe mensajes entrantes antes de procesar — necesario en agentes clasificadores |

## ⚠️ Notas importantes

1. **Las pruebas de los módulos 00-08, 12-13 requieren conexión a Azure OpenAI** — utilizan IA real.
2. **Los módulos 09-11 (Workflows) son determinísticos** — no requieren IA, usan ejecutores locales.
3. **El módulo 13 combina agentes con workflows** — usa `AgentWorkflowBuilder` (alto nivel), `WorkflowBuilder` + `BindAsExecutor` (manual) y enrutamiento condicional con `AddEdge<object>` + clausura (requiere Azure OpenAI).
3. **`RunAsync<T>` no necesita `ChatOptions` ni `ResponseFormat`** — el framework infiere el esquema JSON del tipo genérico automáticamente.
4. **`StateBag` solo acepta tipos referencia** — usar `string` en lugar de `int`, `bool`, etc.
5. **Los warnings CS8602/CS8604 son esperados** — se suprimen o son inofensivos en el contexto de pruebas.
