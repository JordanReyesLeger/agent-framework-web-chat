# APIs, Providers y Razonamiento en Microsoft Agent Framework

> **Fuentes oficiales:** [Providers Overview](https://learn.microsoft.com/en-us/agent-framework/agents/providers/) · [Azure OpenAI Agents](https://learn.microsoft.com/en-us/agent-framework/agents/providers/azure-openai) · [TextReasoningContent (.NET)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.textreasoningcontent) · [TextReasoningContent (Python)](https://learn.microsoft.com/en-us/python/api/agent-framework-core/agent_framework.textreasoningcontent)
> **Última actualización:** 15 de julio, 2026 (docs oficiales actualizadas 10/07/2026)
> **Aplica a:** `02-AFWebChat` (app C#/.NET) y `01-AgentFrameworkTests*` (workshops C# + Python)

---

## 📋 Tabla de contenidos

- [Resumen ejecutivo (TL;DR)](#-resumen-ejecutivo-tldr)
- [¿Qué es un "provider" en Agent Framework?](#-qué-es-un-provider-en-agent-framework)
- [Providers soportados](#-providers-soportados)
- [Evolución de la API: de Chat Completions a Responses](#-evolución-de-la-api-de-chat-completions-a-responses)
- [Los 3 providers principales, a detalle](#-los-3-providers-principales-a-detalle)
- [Razonamiento (reasoning / thinking) a detalle](#-razonamiento-reasoning--thinking-a-detalle)
- [Estado, historial y privacidad](#-estado-historial-y-privacidad)
- [Cómo aplica a este repositorio](#-cómo-aplica-a-este-repositorio)
- [Matriz de decisión](#-matriz-de-decisión)
- [Referencias](#-referencias)

---

## 🎯 Resumen ejecutivo (TL;DR)

Hay que separar **dos ejes** que la gente suele confundir:

| Eje | Qué decide | Ejemplo |
| --- | --- | --- |
| **El modelo** | Si **existe** razonamiento | `gpt-4o` → no razona · `o3` / `gpt-5-reasoning` → sí · Claude → *extended thinking* |
| **La API / provider** | Si ese razonamiento y las tools **se exponen** | Chat Completions → básico · Responses → completo · Foundry → server-side |

**Regla de oro:** para tener "thinking" real necesitas **ambos** — un modelo de razonamiento **y** una API que lo exponga (Responses o Foundry). Cambiar solo de API sin cambiar el modelo no da razonamiento.

---

## 🧩 ¿Qué es un "provider" en Agent Framework?

Un **provider** es la integración con un servicio de inferencia. Agent Framework abstrae todos los providers detrás de una **interfaz uniforme**, así que tu código de agente no cambia aunque cambies de backend:

| Lenguaje | Abstracción del agente | Abstracción del cliente |
| --- | --- | --- |
| **.NET (C#)** | `AIAgent` | `Microsoft.Extensions.AI.IChatClient` |
| **Python** | `BaseAgent` | `ChatClient` |
| **Go** | `*agent.Agent` | constructor por provider |

> 💡 Cualquier servicio que provea un `IChatClient` (en .NET) puede convertirse en agente con `.AsAIAgent(...)`. Por eso, **el mismo loop de streaming sirve para todos los providers**: cambias el cliente, no la lógica.

---

## 🔌 Providers soportados

Agent Framework soporta múltiples servicios de inferencia. Cada uno ofrece un set distinto de capacidades.

### Providers disponibles (.NET)

Cada provider expone una **clase cliente** propia; sobre esa clase (o su `IChatClient`) llamas al método de extensión **`.AsAIAgent(...)`** para obtener un `AIAgent`. Ese es el patrón uniforme: **cambias el cliente, no la lógica del agente**.

| Provider | Descripción | Clase cliente (.NET) | Cómo se crea el agente |
| --- | --- | --- | --- |
| **Azure OpenAI** | Provider completo: **Chat Completions** + **Responses API** + tools. Autenticación con Azure Identity. | `AzureOpenAIClient` → `.GetChatClient(deployment)` o `.GetResponsesClient()` | `chatClient.AsAIAgent(instructions, name)` / `responsesClient.AsAIAgent(model, instructions, name)` |
| **OpenAI** | API directa de OpenAI (chat completion y responses). | `OpenAIClient` → `.GetChatClient(model)` o `.GetResponsesClient()` | `client.GetChatClient(...).AsAIAgent(...)` / `client.GetResponsesClient().AsAIAgent(model, ...)` |
| **Microsoft Foundry** | Agentes gestionados server-side, con historial de chat administrado. | `AIProjectClient` (`Azure.AI.Projects`) | `project.AsAIAgent(model, name, instructions)` (directo) · `project.AsAIAgent(record)` → `FoundryAgent` (versionado) |
| **Anthropic** | Modelos Claude con function tools, streaming y *extended thinking*. | `AnthropicClient` (o `AnthropicFoundryClient`) | `client.AsAIAgent(model, name, instructions)` |
| **Ollama** | Modelos open-source ejecutados localmente. | `OllamaChatClient` (`Microsoft.Extensions.AI.Ollama`) | `chatClient.AsAIAgent(instructions)` |
| **GitHub Copilot** | Integración con el SDK de GitHub Copilot (acceso a shell y archivos). | `CopilotClient` (`GitHub.Copilot`) | `copilotClient.AsAIAgent(...)` · o la clase `GitHubCopilotAgent` |
| **Copilot Studio** | Integración con agentes de Microsoft Copilot Studio. | `CopilotStudioChatClient` (`Microsoft.Agents.AI.CopilotStudio`) | `copilotClient.AsAIAgent(instructions)` |
| **A2A** | Conexión a agentes remotos vía protocolo Agent-to-Agent. | `A2AClient` · `A2ACardResolver` · `AgentCard` (`A2A`) | `a2aClient.AsAIAgent(name, description)` · `await resolver.GetAIAgentAsync()` · `agentCard.AsAIAgent()` → `A2AAgent` |
| **Custom** | Tu propio provider implementando la clase base `AIAgent`. | Tu clase que hereda de `AIAgent` | Instancias tu propia clase (implementas `RunAsync` / `RunStreamingAsync`) |

> 💡 **El método `.AsAIAgent(...)`** es la pieza común: cualquier cliente con un `IChatClient` lo obtiene. Solo los agentes remotos/gestionados difieren — A2A usa `GetAIAgentAsync()` / `AgentCard.AsAIAgent()`, y Foundry versionado devuelve un `FoundryAgent` con `AsAIAgent(record)`.
>
> 🐍 **Python** usa el patrón equivalente `client.as_agent(...)` (p. ej. `AnthropicClient().as_agent(...)`, `OllamaChatClient().as_agent(...)`, `CopilotStudioAgent()`), y añade además **Foundry Local** (`FoundryLocalClient`) para correr modelos Foundry en local.

### Comparación de capacidades (fuente oficial)

| Provider | Function Tools | Structured Outputs | Code Interpreter | File Search | MCP Tools | Background Responses |
| --- | --- | --- | --- | --- | --- | --- |
| **Azure OpenAI** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **OpenAI** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Microsoft Foundry** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Anthropic** | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ |
| **Ollama** | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| **Foundry Local** | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **GitHub Copilot** | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ |
| **Copilot Studio** | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |

> ⚠️ Los providers de terceros (Anthropic, Ollama, etc.) son *Non-Microsoft Products*: se rigen por sus propios términos y tú eres responsable del uso y los costos.

---

## � Evolución de la API: de Chat Completions a Responses

La parte de **"Chat Completions"** que conoces ya evolucionó. Sobre los mismos modelos han existido **tres generaciones** de API:

| Generación | API | Estado | Qué aporta |
| --- | --- | --- | --- |
| 1ª · 2023 | **Chat Completions** | ✅ Vigente (compatibilidad) | *Stateless*: envías todo el historial en cada llamada. Máxima compatibilidad de modelos. |
| 2ª · 2024 | **Assistants** | ⛔ **Deprecada** | Introdujo agentes/threads server-side y hosted tools, pero fue reemplazada. |
| 3ª · 2025 | **Responses** ⭐ | ✅ **Recomendada** | **Unifica** Chat Completions + Assistants: estado en servidor, hosted tools, background y razonamiento. |

> 📌 **Cita oficial:** *"la Responses API reúne las capacidades de chat completions y la Assistants API en una experiencia unificada."*

### Qué añade Responses sobre Chat Completions

| Capacidad | Chat Completions | Responses |
| --- | --- | --- |
| Estado en el servidor (`store=true`, `previous_response_id`, retención 30 días) | ❌ (tú mandas el historial) | ✅ |
| Hosted tools (code interpreter, file search, web search, hosted MCP, image gen, computer use) | ❌ | ✅ |
| Background mode (`background=true` + poll/cancel) para tareas largas | ❌ | ✅ |
| Razonamiento: `reasoning.effort` y `reasoning.encrypted_content` (stateless) | ❌ | ✅ |
| Compactación server-side (`context_management` con `compact_threshold`) | ❌ | ✅ |
| Streaming reanudable (`sequence_number` + `starting_after`) | ❌ | ✅ |
| Compatibilidad amplia de modelos / integración existente | ✅ | ✅ |

A continuación, cada capacidad: **para qué sirve, sus parámetros y un ejemplo**. Los ejemplos usan la API cruda (Python) para mostrar los parámetros exactos; en Agent Framework se exponen con los mismos nombres.

#### 1. Estado en el servidor (`store` + `previous_response_id`)

**Para qué sirve:** que el **servicio guarde la conversación** por ti. En vez de reenviar todo el historial en cada llamada (Chat Completions), mandas solo el mensaje nuevo y referencias la respuesta anterior por su `id`. Lo guardado se retiene **30 días**.

**Parámetros:**

- `store` — `true` (default): guarda la respuesta en el servidor; podrás encadenar, recuperar (`retrieve`) o borrar (`delete`). `false`: **stateless**, no se guarda nada y tú vuelves a mandar el historial (como Chat Completions, pero con las demás ventajas de Responses).
- `previous_response_id` — el `id` de la respuesta anterior; el servicio recupera ese contexto automáticamente.

**Ejemplo — encadenar turnos con estado:**

```python
r1 = client.responses.create(model="gpt-4o", input="Define catastrophic forgetting.")

r2 = client.responses.create(
    model="gpt-4o",
    previous_response_id=r1.id,        # ← reutiliza el contexto del turno anterior
    input="Explícalo para un estudiante de primer año.",
)
print(r2.output_text)
```

**Ejemplo — `store=false` (stateless, tú controlas el historial):**

```python
r = client.responses.create(
    model="gpt-4o",
    input=[{"role": "user", "content": "Hola"}],
    store=False,                        # ← nada se guarda en el servidor
)
```

> 🔗 En .NET son los flags `StoredOutputEnabled` y `PreviousResponseId` de `CreateResponseOptions`.

#### 2. Hosted tools (tools que ejecuta el servicio)

**Para qué sirve:** tools que **corren en el servidor** de OpenAI/Foundry, no en tu proceso: intérprete de código, búsqueda de archivos, búsqueda web, MCP hospedado, generación de imágenes, *computer use*. Chat Completions solo soporta function tools locales + web search.

**Parámetros:** cada tool es un objeto en `tools=[...]` con su `type` y su config. Ejemplos: `{"type": "code_interpreter", "container": {"type": "auto"}}`, `{"type": "image_generation"}`, `{"type": "mcp", "server_label": "...", "server_url": "...", "require_approval": "never"}`.

**Ejemplo — code interpreter:**

```python
r = client.responses.create(
    model="gpt-4o",
    tools=[{"type": "code_interpreter", "container": {"type": "auto"}}],
    instructions="Eres tutor de mate; escribe y ejecuta Python para resolver.",
    input="Resuelve 3x + 11 = 14.",
)
print(r.output_text)
```

**En Agent Framework (Python)** las creas con factories del cliente Responses:

```python
client = OpenAIChatClient()
tools = [client.get_code_interpreter_tool(), client.get_web_search_tool()]
agent = client.as_agent(instructions="…", tools=tools)
```

#### 3. Background mode (tareas largas asíncronas)

**Para qué sirve:** lanzar tareas que tardan **minutos** (modelos de razonamiento, Deep Research, Codex) sin mantener la conexión abierta. Encolas, haces *poll* del estado y recoges el resultado; puedes **cancelar**.

**Parámetros:**

- `background` — `true` para encolar (devuelve `id` + status `queued`).
- Requiere `store=true` (no funciona en modo stateless).
- Opcional `stream=true` para stream reanudable de la tarea en background.

**Ejemplo:**

```python
import time

r = client.responses.create(model="o3", input="Escribe un ensayo largo…", background=True)

while r.status in {"queued", "in_progress"}:
    time.sleep(2)
    r = client.responses.retrieve(r.id)      # poll del estado

print(r.output_text)
# client.responses.cancel(r.id)              # para cancelar
```

#### 4. Razonamiento (`reasoning.effort` + `reasoning.encrypted_content`)

**Para qué sirve:** controlar **cuánto** razona un modelo de razonamiento (o-series, gpt-5-reasoning) y —en modo stateless— **conservar** el razonamiento entre turnos.

**Parámetros:**

- `reasoning={"effort": "low" | "medium" | "high"}` — más *effort* = más calidad pero más tokens/latencia.
- `include=["reasoning.encrypted_content"]` + `store=False` — devuelve el razonamiento **cifrado** para reenviarlo en el siguiente turno sin guardarlo en el servidor.

**Ejemplo:**

```python
r = client.responses.create(
    model="o3",
    reasoning={"effort": "medium"},
    input="¿Cómo está el clima hoy?",
    include=["reasoning.encrypted_content"],
    store=False,
)
```

> ⚠️ Solo aplica a **modelos de razonamiento**; en `gpt-4o` no hace nada.

#### 5. Compactación server-side (`context_management` + `compact_threshold`)

**Para qué sirve:** que el **servicio resuma automáticamente** el contexto cuando crece demasiado, preservando el estado esencial con menos tokens — sin que tú implementes la compactación.

**Parámetros:**

- `context_management=[{"type": "compaction", "compact_threshold": 200000}]` — cuando el conteo de tokens de salida cruza el umbral, el servicio compacta y poda el contexto, emitiendo un *compaction item* (opaco/cifrado).
- Funciona con `store=false` (encadenando el input-array) o con `previous_response_id`.

**Ejemplo:**

```python
r = client.responses.create(
    model="gpt-4o",
    input=conversation,
    store=False,
    context_management=[{"type": "compaction", "compact_threshold": 200000}],
)
```

> También existe el endpoint explícito `client.responses.compact(...)` si prefieres disparar la compactación tú.

#### 6. Streaming reanudable (`sequence_number` + `starting_after`)

**Para qué sirve:** si la conexión de streaming **se cae**, retomas desde el último evento recibido en vez de empezar de cero.

**Parámetros:**

- `stream=true` — activa eventos incrementales; cada evento trae un `sequence_number`.
- Al reconectar: `GET .../responses/{id}?stream=true&starting_after=<sequence_number>` y el servicio **reenvía** los eventos posteriores.

**Ejemplo — consumir el stream y guardar el cursor:**

```python
cursor = None
for event in client.responses.create(model="gpt-4o", input="…", stream=True):
    if event.type == "response.output_text.delta":
        print(event.delta, end="")
    cursor = event.sequence_number      # guárdalo para reanudar
```

**Ejemplo — reanudar tras una caída:**

```bash
curl -N "https://<recurso>.openai.azure.com/openai/v1/responses/<id>?stream=true&starting_after=42" \
  -H "api-key: $AZURE_OPENAI_API_KEY"
```

En Agent Framework, **ambas viven bajo el mismo cliente** de Azure OpenAI — solo cambia el método:

| API | .NET | Python |
| --- | --- | --- |
| **Chat Completions** | `client.GetChatClient("<deployment>")` | `OpenAIChatCompletionClient(...)` |
| **Responses** ⭐ | `client.GetResponsesClient()` | `OpenAIChatClient(...)` |

> 🔎 **La confusión típica:** "Chat Completions" **no** desapareció — sigue siendo válido y es lo que usa este repo. Pero **Responses es su evolución** y el cliente recomendado para agentes nuevos.

---

## 🥇 Los 3 providers principales, a detalle

Las tres primeras filas de la tabla —**Azure OpenAI**, **OpenAI** y **Microsoft Foundry**— son los caminos principales. Comparten el mismo modelo de agente (`AIAgent` / `BaseAgent`), pero cambian el **endpoint**, la **autenticación** y **cómo identifican el modelo**.

| | Azure OpenAI | OpenAI (directo) | Microsoft Foundry |
| --- | --- | --- | --- |
| **Qué es** | Modelos OpenAI en **tu recurso de Azure** | API pública de **openai.com** | Plataforma gestionada de Azure (inferencia + agentes) |
| **Endpoint** | `https://<recurso>.openai.azure.com` | `api.openai.com` | `https://<proyecto>.services.ai.azure.com` |
| **Auth** | Entra ID (recomendado) o API key | API key | Entra ID / token (`az login`) |
| **Identifica el modelo por** | **deployment** | **model** (nombre) | **deployment** en el proyecto |
| **APIs** | Chat Completions + Responses | Chat Completions + Responses | Responses (inferencia) + Agent Service |
| **Agentes server-side versionados** | ❌ (tú los defines) | ❌ | ✅ Prompt / Hosted Agents |
| **Governance, red privada, catálogo multimodelo** | 🟡 Azure | ❌ | ✅ máximo |

### 1. Azure OpenAI

Los modelos de OpenAI desplegados en **tu propio recurso de Azure** (lo que usa `02-AFWebChat`). Un mismo `AzureOpenAIClient` te da **dos clientes**: Chat Completion y Responses.

**Paquetes .NET:** `Azure.AI.OpenAI` · `Azure.Identity` · `Microsoft.Agents.AI.OpenAI`

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;

// Un solo AzureOpenAIClient sirve para las dos APIs
AzureOpenAIClient client = new(
    new Uri("https://<recurso>.openai.azure.com"),
    new DefaultAzureCredential());
```

**Python** — Azure OpenAI usa los **mismos** clientes `agent_framework.openai`, pasando el routing de Azure:

```python
from agent_framework.openai import OpenAIChatClient   # Responses API
from azure.identity import AzureCliCredential

agent = OpenAIChatClient(
    model="gpt-4o",                                   # nombre del deployment
    azure_endpoint="https://<recurso>.openai.azure.com",
    credential=AzureCliCredential(),
).as_agent(name="Resp", instructions="Eres útil.")
```

Dentro de Azure OpenAI, Agent Framework expone **dos tipos de cliente** que apuntan a **APIs distintas** con capacidades distintas.

| Tipo de cliente | API | Ideal para |
| --- | --- | --- |
| **Responses** (recomendado) | Responses API | Agentes completos con hosted tools (code interpreter, file search, web search, hosted MCP) |
| **Chat Completion** | Chat Completions API | Agentes simples, máxima compatibilidad de modelos |

#### Matriz de tools por cliente

| Tool | Responses | Chat Completion |
| --- | --- | --- |
| Function Tools | ✅ | ✅ |
| Tool Approval | ✅ | ✅ |
| Code Interpreter | ✅ | ❌ |
| File Search | ✅ | ❌ |
| Web Search | ✅ | ✅ |
| Hosted MCP Tools | ✅ | ❌ |
| Local MCP Tools | ✅ | ✅ |

#### Chat Completion Client (lo que usa hoy este repo)

```csharp
var chatClient = client.GetChatClient("gpt-4o-mini");

AIAgent agent = chatClient.AsAIAgent(
    instructions: "You are good at telling jokes.",
    name: "Joker");
```

#### Responses Client (el recomendado)

```csharp
var responsesClient = client.GetResponsesClient();

AIAgent agent = responsesClient.AsAIAgent(
    model: "gpt-4o-mini",
    instructions: "You are a helpful coding assistant.",
    name: "CodeHelper");
```

> ℹ️ La **Assistants API está deprecada**. Para código nuevo, Microsoft recomienda el **Responses client**.

### 2. OpenAI (directo)

La API pública de **openai.com** (no Azure). Mismo par de clientes —Responses (recomendado) y Chat Completion— pero identifica el modelo por **nombre** y se autentica con **API key**.

**Paquetes:** .NET `Microsoft.Agents.AI.OpenAI` · Python `agent-framework-openai`

**C#:**

```csharp
using Microsoft.Agents.AI;
using OpenAI;

OpenAIClient client = new("<api_key>");

// Responses (recomendado)
AIAgent agent = client.GetResponsesClient()
    .AsAIAgent(model: "gpt-4o-mini", instructions: "Eres útil.", name: "Resp");

// Chat Completions
AIAgent chat = client.GetChatClient("gpt-4o-mini")
    .AsAIAgent(instructions: "Eres útil.", name: "Chat");
```

**Python:**

```python
from agent_framework.openai import OpenAIChatClient            # Responses
from agent_framework.openai import OpenAIChatCompletionClient  # Chat Completions

# Lee OPENAI_API_KEY del entorno
agent = OpenAIChatClient().as_agent(name="Resp", instructions="Eres útil.")
```

> 🔁 **Azure OpenAI vs OpenAI = mismo SDK, distinto routing.** Azure: tu recurso + Entra ID + *deployment*. OpenAI: `api.openai.com` + API key + *model name*. En Python es literalmente el mismo cliente con inputs distintos.

### 3. Microsoft Foundry

Plataforma **gestionada** de Azure. Además de inferencia, puede **hospedar y versionar** agentes. Requiere **autenticación por token** (`az login` / Managed Identity) — la API key **no** es soportada por el SDK de Foundry. Ofrece **dos patrones** (ver también el notebook [18_foundry_agents.ipynb](../../01-AgentFrameworkTests-Python/notebooks/18_foundry_agents.ipynb)):

| Patrón | .NET | Python | Cuándo |
| --- | --- | --- | --- |
| **Inferencia directa** | `AIProjectClient.AsAIAgent(model, …)` → `ChatClientAgent` | `Agent(client=FoundryChatClient(…))` | Tú controlas instrucciones, tools y sesión en código |
| **Agente gestionado / versionado** | `AIProjectClient.AsAIAgent(record)` → `FoundryAgent` | `FoundryAgent(agent_name, agent_version, …)` | El agente vive en Foundry; instrucciones y tools **fijas** |

**Paquetes:** .NET `Azure.AI.Projects` · `Azure.Identity` · `Microsoft.Agents.AI.Foundry` — Python `agent-framework-foundry`

**C# — inferencia directa (recomendado para empezar):**

```csharp
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;

AIAgent agent = new AIProjectClient(
        new Uri("<endpoint-del-proyecto-foundry>"),
        new DefaultAzureCredential())
    .AsAIAgent(model: "gpt-4o", name: "Joker", instructions: "Cuentas chistes.");
```

**C# — agente versionado (definido en el portal de Foundry):**

```csharp
var project = new AIProjectClient(new Uri("<endpoint>"), new DefaultAzureCredential());
ProjectsAgentRecord record = await project.AgentAdministrationClient.GetAgentAsync("Joker");
FoundryAgent agent = project.AsAIAgent(record);   // instrucciones y tools fijas en Foundry
```

**Python — inferencia directa (`FoundryChatClient`):**

```python
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from azure.identity import AzureCliCredential

agent = Agent(
    client=FoundryChatClient(
        project_endpoint="https://<proyecto>.services.ai.azure.com",
        model="gpt-4o",
        credential=AzureCliCredential(),
    ),
    name="FoundryAgent",
    instructions="Eres útil.",
)
```

**Python — agente gestionado (`FoundryAgent`):**

```python
from agent_framework.foundry import FoundryAgent
from azure.identity import AzureCliCredential

agent = FoundryAgent(
    project_endpoint="https://<proyecto>.services.ai.azure.com",
    agent_name="my-prompt-agent",
    agent_version="1.0",      # omítelo para Hosted Agents
    credential=AzureCliCredential(),
)
```

> ⚠️ En el **agente gestionado** (`FoundryAgent`), las instrucciones y las tools son las de la definición en Foundry: **no** puedes cambiarlas por-run desde el código (`instructions`, `tools`, `model` se ignoran o se eliminan del request). Si necesitas control dinámico, usa la **inferencia directa** (`FoundryChatClient`).

---

## 🧠 Razonamiento (reasoning / thinking) a detalle

### Qué es `TextReasoningContent`

Agent Framework modela el razonamiento como un **tipo de contenido separado** del texto final:

- **`TextContent`** → el **texto de la respuesta** que ve el usuario.
- **`TextReasoningContent`** → el **"pensamiento" / razonamiento** que hace el modelo *antes* de responder. Es una subclase de `AIContent`, distinta de `TextContent`.

En cada update del stream, el agente devuelve una lista `Contents` que puede incluir **texto**, **llamadas a tools**, **resultados de tools** y —cuando el modelo razona— **`TextReasoningContent`**.

### Qué modelos razonan y qué API lo expone

| Combinación | ¿Razona el modelo? | ¿La API expone el reasoning? |
| --- | --- | --- |
| `gpt-4o` + Chat Completions | ❌ No | ❌ No |
| `gpt-4o` + Responses | ❌ No | — (no hay nada que exponer) |
| `o3` / `o4-mini` / `gpt-5.x` + **Chat Completions** | ✅ Sí (interno) | ❌ **No** (oculto; solo `reasoning_tokens` en `usage`) |
| `o3` / `o4-mini` / `gpt-5.x` + **Responses** | ✅ Sí | ✅ **Sí** — resumen vía `reasoning.summary` (`TextReasoningContent`) |
| **DeepSeek-R1 / Grok / Qwen3 / vLLM** + **Chat Completions** | ✅ Sí | ✅ **Sí** — campo `reasoning_content` en el delta (ver abajo) |
| Modelo de razonamiento + **Foundry** | ✅ Sí | ✅ Sí (según despliegue) |
| Claude (Anthropic) | ✅ *Extended thinking* | ✅ Sí (mecanismo propio de Anthropic) |

> 🔑 **Conclusión:** el "bloque de pensamiento" estilo Copilot **no sale con `gpt-4o`**. Requiere un **modelo de razonamiento** + una **API que lo exponga** (Responses o Foundry). **Ojo:** los modelos gpt/o-series **no** exponen su razonamiento por Chat Completions (ver la siguiente sección).

### ¿Se puede razonar por Chat Completions? — Depende del modelo

Esta es la pregunta clave y la respuesta tiene **dos casos** que se suelen confundir:

#### Caso 1 — Modelos OpenAI / Azure OpenAI (gpt-5.x, o-series): ❌ NO por Chat Completions

Aunque estos modelos **sí razonan** en Chat Completions (lo ves en `usage.completion_tokens_details.reasoning_tokens`), **nunca devuelven ese razonamiento como texto** por ese endpoint. Es una **decisión de diseño de OpenAI**, no una limitación técnica. Citas oficiales ([OpenAI · Reasoning models](https://developers.openai.com/api/docs/guides/reasoning)):

> *"While reasoning tokens are not visible via the API, they still occupy space in the model's context window and are billed as output tokens."*
>
> *"While we don't expose the raw reasoning tokens emitted by the model, you can view a summary of the model's reasoning using the `summary` parameter."*

- El **resumen** (`reasoning.summary`) sale como un **output item de tipo `reasoning`** con un array `summary` — una estructura que **solo existe en la Responses API**. Chat Completions no tiene forma de devolverlo.
- OpenAI empuja explícitamente a Responses: *"Reasoning models work better with the Responses API. While the Chat Completions API is still supported, you'll get improved model intelligence and performance by using Responses."* Incluso: *desde GPT-5.4, el tool calling ya no se soporta en Chat Completions.*
- Azure lo confirma y añade una advertencia: intentar extraer el razonamiento crudo por otras vías *"may violate the Acceptable Use Policy."*

👉 Por eso, para gpt-5.1 la **única** vía de mostrar el "pensando" es la **Responses API** (lo que implementa `ReasoningChatClient` en este repo).

#### Caso 2 — Modelos DeepSeek-R1, xAI Grok, Qwen3, backends vLLM: ✅ SÍ por Chat Completions

Estos modelos **eligieron** exponer el razonamiento en un campo **no estándar** llamado **`reasoning_content`**, dentro de `choices[].delta` (streaming) o `choices[].message` (no streaming) de la **misma Chat Completions API**. DeepSeek inició la convención y Grok/Qwen/vLLM la adoptaron.

**El obstáculo en .NET:** el SDK de OpenAI **no** tipa `reasoning_content`, así que `Microsoft.Extensions.AI` **no** lo mapea a `TextReasoningContent` automáticamente (confirmado en [dotnet/extensions#6208](https://github.com/dotnet/extensions/issues/6208), cerrado por el equipo: *"the OpenAI library does not currently expose the ability to access this information"*; y en [agent-framework#3662](https://github.com/microsoft/agent-framework/discussions/3662) para Python: *"Chat Completions API has limited support for reasoning content extraction … you should use the Responses API instead"*).

**Workaround implementado en este repo** ([AgentOrchestrationService.cs](../Services/AgentOrchestrationService.cs), `TryGetChatCompletionsReasoning` / `ExtractReasoningContent`): se toma el `update.RawRepresentation` (que envuelve el `OpenAI.Chat.StreamingChatCompletionUpdate`), se re-serializa con `ModelReaderWriter.Write(...)` — el SDK **conserva** los campos desconocidos en los datos adicionales del delta — y se lee `choices[0].delta.reasoning_content`. Si no está, devuelve `null` (inofensivo). El razonamiento se emite con el mismo evento `AgentReasoning` → **la misma UI** que la ruta Responses.

```csharp
// Extracción de reasoning_content del delta crudo (Chat Completions)
BinaryData json = ModelReaderWriter.Write(streamingUpdate);
using var doc = JsonDocument.Parse(json.ToMemory());
var delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");
if (delta.TryGetProperty("reasoning_content", out var rc) && rc.ValueKind == JsonValueKind.String)
    yield return StreamEventService.AgentReasoning(agentName, rc.GetString()!);
```

> ⚠️ **Validación (jul 2026):** en el recurso de Azure de este repo no se pudo probar live porque `grok-4-fast-reasoning` está **deprecado** (410) y los DeepSeek-R1/R1-0528/V3.1 también (desde 2026-07-13); DeepSeek-V3.2 híbrido por `/openai/v1` no expone el modo *thinking*. Se validó con un **test determinista de round-trip** que confirma que la extracción recupera `reasoning_content` cuando el modelo lo emite (y devuelve `null` sin romper cuando no).

#### Resumen de las dos vías

| Vía | Modelos | Mecanismo | Dónde en este repo |
| --- | --- | --- | --- |
| **Responses API** | OpenAI gpt-5.x, o-series | `reasoning.summary` → `TextReasoningContent` | `ChatClientFactory.CreateReasoningChatClient()` + `ReasoningChatClient` |
| **Chat Completions** | DeepSeek-R1, Grok, Qwen3, vLLM | campo `reasoning_content` (extraído del raw) | `AgentOrchestrationService.TryGetChatCompletionsReasoning()` |

### `reasoning_effort` (específico de OpenAI / Azure OpenAI)

En los modelos de razonamiento de OpenAI/Azure OpenAI puedes controlar **cuánto** razona el modelo con el parámetro `reasoning_effort` (p. ej. `low` / `medium` / `high`). Es **específico de OpenAI/Azure**; otros modelos (Anthropic, etc.) lo controlan de forma distinta.

### Cómo capturarlo en el stream (patrón .NET)

El **mismo loop** sirve para cualquier provider; solo hay que leer `update.Contents` en lugar de solo el texto:

```csharp
await foreach (var update in agent.RunStreamingAsync(message, session))
{
    foreach (var content in update.Contents)
    {
        switch (content)
        {
            // 🧠 Solo llega con modelos de razonamiento (o-series, gpt-5-reasoning…)
            case Microsoft.Extensions.AI.TextReasoningContent reasoning
                    when !string.IsNullOrEmpty(reasoning.Text):
                // → pintar en un bloque colapsable de "pensamiento"
                break;

            // 🔧 El modelo decidió llamar una tool → "Buscando en el índice…"
            case Microsoft.Extensions.AI.FunctionCallContent call:
                // → evento de progreso de herramienta
                break;

            // ✅ La tool respondió
            case Microsoft.Extensions.AI.FunctionResultContent result:
                // → evento de resultado de herramienta
                break;

            // 💬 Texto final de la respuesta
            case Microsoft.Extensions.AI.TextContent text
                    when !string.IsNullOrEmpty(text.Text):
                // → token normal de la respuesta
                break;
        }
    }
}
```

> ⚠️ **Importante:** lo que devuelven los modelos suele ser un **resumen** del razonamiento (*reasoning summary*), **no** el chain-of-thought crudo — por diseño de seguridad de OpenAI/Azure.

---

## 💾 Estado, historial y privacidad

Uno de los puntos que más confunde: **quién guarda el historial de la conversación** y qué implica en **costo** y **privacidad**. Aquí Chat Completions y Responses se diferencian de raíz.

### Los dos modelos de estado

| | **Stateless** (Chat Completions, o Responses `store=false`) | **Stateful** (Responses `store=true`) |
| --- | --- | --- |
| ¿Quién guarda el historial? | **Tú** (tu app → Cosmos DB, SQL, etc.) | **El servicio** (retención 30 días) |
| Qué mandas cada turno | **Todos** los mensajes | Solo el mensaje nuevo + `previous_response_id` |
| Qué persistes en tu store | El historial completo | Solo un **id** (puntero) |
| Control / portabilidad de datos | Total (los datos son tuyos, sin límite de 30 días) | Menor (dependes del servicio y su retención) |
| APIs | Chat Completions **y** Responses (`store=false`) | Solo **Responses** |

> 🔑 **Responses NO te obliga a ceder el historial.** Con `store=false` obtienes razonamiento + hosted tools **y** sigues siendo dueño del historial (como en Chat Completions). El estado en servidor es **opt-in**.

Existe un tercer modelo: los **agentes gestionados de Foundry** mantienen un **thread persistente** server-side; tú solo guardas el `threadId`.

| Modelo | Dónde vive el historial | Qué guardas tú |
| --- | --- | --- |
| **Chat Completions** | Tu app | Todo el historial |
| **Responses `store=false`** | Tu app | Todo el historial |
| **Responses `store=true`** | El servicio (30 días) | Un `response_id` |
| **Foundry (managed thread)** | El servicio (persistente) | Un `threadId` |

### El "seam" en Agent Framework (`ConversationId`)

Agent Framework abstrae esto con el `AgentSession` y sus métodos `SerializeSessionAsync` / `DeserializeSessionAsync` (así es como persistes el estado a tu store). Lo que **cambia** según el modo es *qué* se serializa:

- **Sin `ChatOptions.ConversationId`** → el cliente **reenvía los mensajes** (stateless). El `AgentSession` lleva todo el historial → tu blob crece.
- **Con `ConversationId`** (Responses) → usa el estado del servidor (`previous_response_id`). El `AgentSession` lleva un **puntero** → tu blob es diminuto.

> El **mismo** código `Serialize/Deserialize + persistir` sirve para todos los providers; solo cambia el tamaño de lo guardado y dónde vive la "verdad".

### Costos de guardar el historial

**Guardar el estado NO tiene tarifa de almacenamiento** (ni OpenAI ni Azure cobran por `store=true`; en Foundry se guarda en tu propio recurso). El costo real son **los tokens**:

| Concepto | ¿Se cobra? | Detalle |
| --- | --- | --- |
| Almacenar el historial (`store=true`) | ❌ Gratis | Solo lo persiste 30 días |
| Reprocesar el contexto cada turno | ✅ Sí | El modelo "lee" el historial → **input tokens** en cada llamada |
| Reasoning tokens | ✅ Sí | Se facturan como **output tokens** aunque no se muestren |

> ⚠️ **La trampa:** el estado en servidor te ahorra **reenviar** los mensajes por la red, **no** los tokens. El modelo procesa el contexto como input igual. En costo de tokens, `store=true` ≈ `store=false`.

**Lo que sí abarata multi-turn: prompt caching** ([OpenAI](https://developers.openai.com/api/docs/guides/prompt-caching) · [Azure](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/prompt-caching)):

- Se activa **automático** (sin cambios de código) en prompts > 1.024 tokens.
- La parte repetida del input (tu historial, que se repite turno a turno) se cobra al **~10%** del precio normal (**90% de descuento**).
- Aparece en el campo `cached_tokens` del `usage`.

> Por eso el ahorro grande viene del **caché de prompt**, no de dónde guardes el historial: tu modelo (Cosmos + `store=false`) cuesta casi lo mismo que ceder el estado, pero conservas el control.

### Privacidad en Foundry / Azure OpenAI

Garantías base ([Data, privacy & security · Microsoft Learn](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/openai/data-privacy)) — tus prompts, completions, embeddings y training data:

- ❌ NO están disponibles para **otros clientes**.
- ❌ NO están disponibles para **OpenAI ni otros proveedores** del modelo.
- ❌ NO se usan para **entrenar ni mejorar** sus modelos.
- Los modelos son **stateless**: *"no prompts or completions are stored in the model."*
- Foundry corre en el entorno Azure de Microsoft y **no interactúa** con servicios de OpenAI (ChatGPT / OpenAI API).

Cuando usas **estado en servidor** (Responses / Threads / Stored completions):

- Se guarda **en tu recurso Foundry, en tu tenant Azure, en tu misma geografía**.
- **Encriptado en reposo con AES-256** por defecto (opción de **customer-managed key**).
- **Lo puedes borrar cuando quieras**; procesamiento en tu geografía (salvo deployments `Global` / `DataZone`).

**Abuse monitoring** (único punto con retención por defecto):

- Por defecto, una **muestra** de prompts/completions puede almacenarse **hasta 30 días** para detección de abuso (revisión automática; humana solo si se marca).
- Clientes aprobados pueden **apagarlo** (*modified abuse monitoring* / Zero Data Retention). Se verifica con `"ContentLogging": "false"` (portal o `az cognitiveservices account show`).

### Cómo aplica a este repo

- El [SessionService.cs](../Services/SessionService.cs) usa hoy un `ConcurrentDictionary` en memoria + `agent.SerializeSessionAsync` / `DeserializeSessionAsync`. Ese patrón es **stateless** (tú eres dueño del historial) y **provider-agnóstico**.
- Aunque `GeneralAssistant` ya usa Responses (para razonamiento), **sigue stateless** porque no se setea `ConversationId` → el `SessionService` guarda el historial igual, sin romperse.
- Para producción, basta con **cambiar el diccionario por Cosmos DB** (la infra ya tiene [cosmosdb.tf](../../infra/cosmosdb.tf)) manteniendo el mismo `Serialize/Deserialize`. Es el modelo recomendado: control total + sin límite de 30 días.

> 🧭 **Regla mental:** **Chat Completions = el API tiene amnesia** (tú guardas y reenvías todo → Cosmos). **Responses `store=true` = recuerda 30 días** (guardas un ticket). **Foundry = tu bodega** (guardas un `threadId`). Guardar es gratis; procesar cuesta tokens (mitigado por prompt caching). En Foundry tus datos no salen de tu Azure ni entrenan a nadie.

---

## 🧰 Cómo aplica a este repositorio

`02-AFWebChat` ya implementa el "bloque de pensamiento" estilo Copilot con **ambas** vías de razonamiento:

- [ChatClientFactory.cs](../Services/ChatClientFactory.cs) expone dos fábricas: `CreateChatClient()` (Chat Completions, `GetChatClient(deployment)`) y `CreateReasoningChatClient()` (Responses API, `GetResponsesClient().AsIChatClient(deployment)` envuelto en `ReasoningChatClient` con `reasoning.effort` + `reasoning.summary`).
- [ReasoningChatClient.cs](../Services/ReasoningChatClient.cs) es un `DelegatingChatClient` que inyecta las opciones de razonamiento vía `RawRepresentationFactory` → `CreateResponseOptions.ReasoningOptions`.
- El loop de streaming en [AgentOrchestrationService.cs](../Services/AgentOrchestrationService.cs) captura razonamiento por **las dos vías**: `TextReasoningContent` en `update.Contents` (Responses) **y** `reasoning_content` del raw (Chat Completions con modelos DeepSeek/Grok). Ambas emiten `AgentReasoning` → mismo bloque en la UI.
- `GeneralAssistantAgent` usa `CreateReasoningChatClient()` con `gpt-5.1` como demo; el resto de agentes siguen en `CreateChatClient()`.
- La UI ([wwwroot/js/chat.js](../wwwroot/js/chat.js) + [wwwroot/css/agent-chat.css](../wwwroot/css/agent-chat.css)) pinta el evento `agent-reasoning` en una tarjeta colapsable "Pensando… / Razonó durante Ns".

**Qué elegir según el modelo:**

| Modelo del deployment | Vía de razonamiento | Config |
| --- | --- | --- |
| `gpt-5.x`, `o-series` (OpenAI) | **Responses** | Usar `CreateReasoningChatClient()`; ajustar `AzureOpenAI:ReasoningEffort` / `ReasoningSummary` |
| DeepSeek-R1, Grok, Qwen3 (vía Chat Completions) | **`reasoning_content`** | Usar `CreateChatClient()`; la extracción es automática |
| `gpt-4o` u otros sin razonamiento | — | No hay razonamiento; la UI simplemente no muestra el bloque (no truena) |

> 💡 Para mostrar además **progreso de tools** ("Buscando…", "Leyendo…") basta con inspeccionar `FunctionCallContent` / `FunctionResultContent` en `update.Contents` — funciona con cualquier modelo, incluso `gpt-4o`.

---

## 🧭 Matriz de decisión

| Necesito… | Usa |
| --- | --- |
| Máxima compatibilidad de modelos, integración simple | **Chat Completions** |
| Hosted tools (code interpreter, file search, hosted MCP) | **Responses** |
| Razonamiento de **gpt-5.x / o-series** expuesto | **Responses** (`reasoning.summary`) o **Foundry** — NO Chat Completions |
| Razonamiento de **DeepSeek-R1 / Grok / Qwen3** expuesto | **Chat Completions** (campo `reasoning_content`) |
| Que el servicio gestione el historial de la conversación | **Foundry** (agente gestionado) o **Responses** (`store=true`) |
| Control total del historial y compactación | **Chat Completions** o **Responses** (`store=false`) |
| Correr modelos en local (sin nube) | **Ollama** o **Foundry Local** (Python) |

---

## 📚 Referencias

| Tema | Enlace |
| --- | --- |
| Providers Overview | [learn.microsoft.com](https://learn.microsoft.com/en-us/agent-framework/agents/providers/) |
| Azure OpenAI: Chat Completions vs Responses | [learn.microsoft.com](https://learn.microsoft.com/en-us/agent-framework/agents/providers/azure-openai) |
| OpenAI provider (Chat Completions vs Responses) | [learn.microsoft.com](https://learn.microsoft.com/en-us/agent-framework/agents/providers/openai) |
| Microsoft Foundry provider | [learn.microsoft.com](https://learn.microsoft.com/en-us/agent-framework/agents/providers/microsoft-foundry) |
| `TextReasoningContent` (.NET) | [learn.microsoft.com](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.textreasoningcontent) |
| `TextReasoningContent` (Python) | [learn.microsoft.com](https://learn.microsoft.com/en-us/python/api/agent-framework-core/agent_framework.textreasoningcontent) |
| Responses API (Azure OpenAI) | [learn.microsoft.com](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/responses) |
| Reasoning models (razonamiento solo por Responses) | [developers.openai.com](https://developers.openai.com/api/docs/guides/reasoning) |
| Azure OpenAI reasoning (razonamiento oculto en Chat Completions) | [learn.microsoft.com](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/reasoning) |
| `reasoning_content` no expuesto en el SDK .NET | [dotnet/extensions#6208](https://github.com/dotnet/extensions/issues/6208) |
| Chat Completions con soporte limitado de reasoning | [agent-framework#3662](https://github.com/microsoft/agent-framework/discussions/3662) |
| Historial y costo por API (notebook interno) | [06_conversations_sessions.ipynb](../../01-AgentFrameworkTests-Python/notebooks/06_conversations_sessions.ipynb) |
| Patrones de Foundry (notebook interno) | [18_foundry_agents.ipynb](../../01-AgentFrameworkTests-Python/notebooks/18_foundry_agents.ipynb) |

> Las funciones **preview** y los nombres de clases del SDK pueden cambiar entre versiones — confirma siempre en los enlaces oficiales.
