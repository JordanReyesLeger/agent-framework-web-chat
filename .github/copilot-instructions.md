# AF-WebChat — Guía para GitHub Copilot

Aplicación web multi-agente construida con **Microsoft Agent Framework** (`Microsoft.Agents.AI`, .NET 9) sobre **Microsoft Foundry**. El proyecto principal es `02-AFWebChat/`; el resto del repo son ejemplos y la infraestructura.

## Estructura del repositorio

| Carpeta | Qué es |
| --- | --- |
| `02-AFWebChat/` | **La app principal.** ASP.NET Core MVC + API con streaming SSE |
| `01-AgentFrameworkTests/` | Ejemplos didácticos en C# (00→13), numerados por tema |
| `01-AgentFrameworkTests-Python/` | Los mismos temas en notebooks de Python |
| `infra/` | Terraform: Foundry, Search, Storage, Cosmos DB, App Service, Bot |

## Concepto central: todo es un `AIAgent`

Cada agente se declara como un **`AgentDefinition`** (ver `Agents/AgentDefinition.cs`) con metadatos para la UI y un `Factory` que construye el `AIAgent` bajo demanda:

```csharp
public static AgentDefinition CreateDefinition() => new()
{
    Name = Name, Description = "...", Category = "Básico", Icon = "🤖",
    ExamplePrompts = [...],
    Factory = sp =>
    {
        var chatClient = sp.GetRequiredService<ChatClientFactory>().CreateChatClient();
        return chatClient.AsAIAgent(name: Name, instructions: "...");
    }
};
```

Se registra en `Program.cs` con `registry.Register(MiAgente.CreateDefinition())`. El `AgentRegistry` **cachea la instancia** tras el primer uso (el `Factory` corre una sola vez).

**Para añadir un agente:** crea la clase estática en la subcarpeta de `Agents/` que corresponda a su categoría, expón `CreateDefinition()`, y regístralo en `Program.cs`. La UI lo descubre sola vía `GET /api/agent` (agrupa por `Category` dinámicamente — no hay categorías hardcodeadas).

## Qué cliente usar para cada tipo de agente

Todos los agentes obtienen su `IChatClient` de **`ChatClientFactory`**:

| Método | API subyacente | Cuándo usarlo |
| --- | --- | --- |
| `CreateChatClient()` | **Responses API + razonamiento** (default) | **Úsalo salvo que tengas razón para no hacerlo.** Es el default de todos los agentes |
| `CreateReasoningChatClient()` | Responses API + razonamiento (explícito) | Igual que el anterior; explícito cuando el agente depende del bloque "Pensando…" |
| `CreateChatCompletionsClient()` | Chat Completions clásico | Solo demo. **`Summarizer` es el único** agente que lo usa a propósito |

Puntos clave:
- El interruptor global es `AzureOpenAI:UseResponsesApi` (default `true`). Si se pone en `false`, `CreateChatClient()` cae a Chat Completions.
- El **resumen de razonamiento solo existe vía Responses API** — en Chat Completions los tokens de razonamiento están ocultos. `ReasoningChatClient` es el `DelegatingChatClient` que inyecta `reasoning.effort`/`reasoning.summary`.
- El nivel de razonamiento es **global y mutable en runtime** (`ReasoningSettings`, se lee en cada llamada). Se cambia desde la UI o con `POST /api/chat/reasoning`; aplica al instante a todos los agentes sin reiniciar.

## Agentes de Foundry (versioned) — distintos a los demás

`Agents/Domain/FoundrySimpleBotAgent.cs` y `FoundryAgent.cs` **no** usan `ChatClientFactory`. Usan el patrón *Foundry Agent versioned*: el agente vive **en el proyecto de Foundry**, no en el código.

```csharp
var client = new AIProjectClient(new Uri(endpointProject), new DefaultAzureCredential());
// Publica/recupera la versión del agente y crea una nueva si la definición del código cambió
var record = FoundryAgentProvisioning.EnsureAgentVersion(client, name, buildDefinition, revision, logger);
FoundryAgent agent = client.AsAIAgent(record, tools);   // ← implementa AIAgent normal
```

- Requieren `AzureOpenAI:EndpointProject` (la URL `.../api/projects/{nombre}`) y **auth por token** — Foundry no acepta API key. El scope es `https://ai.azure.com/.default`, distinto al de inferencia; si `DefaultAzureCredential` falla ahí, haz `az login --scope https://ai.azure.com/.default --tenant <tenant>`.
- Las instrucciones y la definición viven **en Foundry**, pero se publican desde el código: `FoundryAgentProvisioning.EnsureAgentVersion` firma la definición (modelo + instrucciones + herramientas) y publica una versión nueva **solo si la firma cambió** (la firma se guarda en los metadatos de la versión). Sin esto, editar las instrucciones en el código no tenía ningún efecto sobre un agente ya creado.
- **Las funciones locales sí funcionan, pero deben declararse en Foundry.** Pasar `AIFunction`s a `AsAIAgent(record, tools)` solo aporta la *implementación* que corre en este proceso; el modelo únicamente ve lo declarado en la versión publicada. Por eso `FoundryAgentProvisioning.ToResponseTools` traduce cada `AIFunction` a una declaración que se publica junto a la definición. Así es como ambos agentes de Foundry usan `GenerateChart`.
- `FoundryAgent` (orquestador) combina los dos modos: una tool **OpenAPI** que ejecuta Foundry y llama de vuelta a `/api/chat/send` de esta misma app (la URL base sale de `DevTunnel:Url`; en Azure la inyecta Terraform con la URL real del sitio), más `GenerateChart` que se ejecuta localmente.

## Los 4 canales de ejecución

`AgentOrchestrationService.RunStreamingAsync` enruta según el `ChatRequest`:

1. **Agente único** → `registry.GetAgent(name)` + `agent.RunStreamingAsync(msg, session)`
2. **Orquestación** (`OrchestrationName` o `CustomPattern` de orquestación) → `OrchestrationFactory`, patrones: `Sequential`, `Concurrent`, `GroupChat` (round-robin), `GroupChatAI` (un LLM elige quién habla, ver `AIGroupChatManager`), `Handoff`
3. **Workflow** (`WorkflowName` o `CustomPattern` de workflow) → `WorkflowFactory`, patrones: `Iterative` (writer↔reviewer), `Conditional` (clasifica y enruta), `FanOut` (paralelo + síntesis)
4. **A2A** — agentes remotos de otro framework/lenguaje adaptados a `AIAgent` (`Agents/A2A/`). Categoría `A2A`

Todo emite **`StreamEvent`** vía SSE (`StreamEventService`): `agent-start`, `agent-thinking`, `agent-reasoning`, `agent-token`, `tool-call`, `tool-approval`, `workflow-step`, `agent-complete`, `done`.

> El razonamiento se emite en agente único y en orquestaciones. En workflows solo en `FanOut`; `Iterative`/`Conditional` usan `RunAsync` (bufferizado) y no lo exponen.

## Servicios clave (`Services/`)

| Clase | Rol |
| --- | --- |
| `ChatClientFactory` | Crea los `IChatClient`. También hace *warm-up* real al arrancar (evita ~20 s de cold start) |
| `AgentOrchestrationService` | El router de los 4 canales de arriba. **Scoped**, el resto son singletons |
| `SessionService` | Sesiones: caché en memoria + persistencia en **Cosmos DB** (write-through) |
| `ReasoningChatClient` / `ReasoningSettings` | Razonamiento global mutable en runtime |
| `DocumentService` / `DocumentIndexingService` | RAG: sube a Blob, indexa en Azure AI Search con skillset |
| `StreamEventService` | Fábrica de eventos SSE |

## Tools y middleware

- **Tools**: los plugins viven en `Tools/Plugins/`; se convierten a `AIFunction` con `AIFunctionFactoryExtensions.CreateFromInstance(plugin)` y se pasan al agente. `ToolRegistry` agrupa conjuntos con nombre.
- **Middleware**: existe `WithFullMiddleware(logger)` (Logging + Metrics + Audit) en `Middleware/MiddlewareExtensions.cs`, pero **hoy ningún agente lo aplica** — se encadena con `agent.AsBuilder().Use(...)` si lo necesitas. (El "Middleware: 3" que muestra la UI es texto fijo del front, no refleja el pipeline real.)
- **Salida estructurada**: `SupportsStructuredOutput = true` + `SupportsStreaming = false` (ver `EntityExtractorAgent`, `SentimentAnalyzerAgent`).

## Persistencia de sesiones y Cosmos DB

`SessionService` usa los métodos genéricos de Agent Framework (`SerializeSessionAsync`/`DeserializeSessionAsync`), por eso **funciona igual para cualquier tipo de agente** (Chat Completions, Responses o Foundry) sin código especial.

- Config: `CosmosDB:AccountEndpoint` (**sin llaves** — managed identity). Si está vacío, cae a solo-memoria y lo avisa por log.
- Cosmos es necesario para sobrevivir reinicios y escalado horizontal. Nota: tras el primer turno, el framework delega el historial al servidor (`ConversationId` → `previous_response_id`) y el historial local deja de crecer.
- Los fallos de Cosmos se loguean pero **nunca** rompen el chat.

## Convenciones de infraestructura (`infra/`)

- **Todo con managed identity, cero llaves**: Foundry, Storage, Search y Cosmos tienen el auth local deshabilitado. La única excepción documentada es la key de Speech para Live Avatar (`post_deploy_keys.tf`).
- Cosmos DB tiene **su propio RBAC de datos**, separado de ARM: se necesita `azurerm_cosmosdb_sql_role_assignment` (un rol ARM no alcanza).
- **Un solo recurso de Foundry**: los modelos de VoiceLive viven en la misma cuenta, no en una aparte.
- El Web App va en **`westus2`** (en `eastus2` la suscripción reporta cuota 0 para todos los SKU de App Service).

## Gotchas que cuestan tiempo

- **`appsettings.Development.json` está en `.gitignore`** — la config local (p. ej. la sección `A2A`) se pierde al clonar. Tras editar cualquier `appsettings`, hay que **recompilar** (se copia a `bin/`); con `--no-build` no toma efecto.
- **DLL bloqueada enmascara errores de build**: si hay un `dotnet` zombie, `dotnet build` puede decir "0 errores" pero correr sobre una DLL vieja. Mata los procesos antes de compilar.
- Tras un `terraform apply` que toque `app_settings`, verifica `az webapp show --query state` — a veces el sitio queda `Stopped` (se ve como HTTP 403 "Unavailable") y hay que hacer `az webapp start`.
- `[Route("api/[controller]")]` usa el **nombre de la clase**, no el del archivo: `AgentController` vive en `AgentWorkflowController.cs` y responde en `/api/agent`.

## Estilo

- Código y comentarios nuevos: **español** (es lo que predomina en el repo), instrucciones de agentes también en español.
- Los agentes son clases **estáticas** con `public const string Name` y `CreateDefinition()`.
- Docs relevantes: `02-AFWebChat/docs/PROVIDERS_Y_RAZONAMIENTO.md` (APIs y razonamiento a fondo) y `docs/PROTOCOLO_A2A.md`.
