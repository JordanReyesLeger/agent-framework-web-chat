# Microsoft Agent Framework vs. Foundry Agent Service

## La Confusión Más Común en el Ecosistema de Agentes de Microsoft

Muchas personas confunden **Microsoft Agent Framework** con **Foundry Agent Service** porque ambos se usan para construir agentes de IA. Sin embargo, **no son lo mismo ni son competencia entre sí** — son **complementarios** y operan en capas diferentes del stack tecnológico.

> **Analogía simple:** Agent Framework es como **Express.js** (un framework para escribir código) y Agent Service es como **Azure App Service** (una plataforma para hospedar y escalar tu aplicación). Puedes usar Express.js dentro de App Service, y de la misma manera, puedes usar Agent Framework dentro de Agent Service.

---

## ¿Qué es Microsoft Agent Framework?

**Microsoft Agent Framework** (anteriormente conocido como **Semantic Kernel Agent Framework**) es un **SDK open-source** que te permite crear agentes de IA directamente en tu código.

### Características Principales

| Característica | Descripción |
|---|---|
| **Tipo** | SDK / Librería de código (NuGet, pip) |
| **Lenguajes** | C#, Python |
| **Ejecución** | Local — corre donde tú lo hospedes |
| **Open Source** | Sí — [github.com/microsoft/Agents-for-net](https://github.com/microsoft/Agents-for-net) / [Agents-for-python](https://github.com/microsoft/Agents-for-python) |
| **Código requerido** | Sí — control total del código |
| **Base** | Construido sobre `Microsoft.Extensions.AI` (`IChatClient`) — no requiere Semantic Kernel |
| **Orquestación** | Patrones built-in: Sequential, Concurrent, Handoff, Group Chat, Magentic |
| **Hosting** | Tú decides: App Service, Container Apps, VM, on-premises, etc. |

### El Agente Central: `AIAgent`

En Microsoft Agent Framework, el concepto central es **`AIAgent`**. Se crea a partir de cualquier `IChatClient` (de `Microsoft.Extensions.AI`) usando el método de extensión `.AsAIAgent()`. No necesitas Semantic Kernel.

| Concepto | Descripción |
|---|---|
| **`AIAgent`** | La clase principal. Envuelve un `IChatClient` y le agrega instrucciones, tools, middleware y orquestación. Se crea con `.AsAIAgent()`. |
| **`IChatClient`** | Interfaz estándar de `Microsoft.Extensions.AI` para hablar con cualquier modelo de chat (Azure OpenAI, OpenAI, Ollama, etc.). Es la base sobre la que se construye el agente. |
| **`ChatClientAgentOptions`** | Opciones de configuración: instrucciones, nombre, descripción, tools, output format, etc. |
| **`AIAgentThread`** | Mantiene el historial de conversación (local, en memoria). Cada sesión tiene su propio thread. |
| **`AITool` / `AIFunctionFactory`** | Herramientas (function calling) que el agente puede invocar. Se crean desde métodos C# con `AIFunctionFactory.Create()`. |

```csharp
// Así se crea un agente en Microsoft Agent Framework:
using Microsoft.Agents.AI;
using Azure.AI.OpenAI;

AIAgent agent = new AzureOpenAIClient(
        new Uri(endpoint), new AzureCliCredential())
    .GetChatClient("gpt-4o")
    .AsAIAgent(instructions: "Eres un asistente útil.");

// Ejecutar
string response = await agent.RunAsync("¿Cuál es la ciudad más grande de Francia?");

// Con thread para multi-turn
var thread = agent.GetNewThread();
await foreach (var msg in agent.RunAsync("Hola", thread)) { ... }
await foreach (var msg in agent.RunAsync("Cuéntame más", thread)) { ... }
```

> **Nota clave:** `AIAgent` trabaja con `IChatClient`, que es una **abstracción estándar de .NET** (`Microsoft.Extensions.AI`). Esto significa que tu agente funciona con Azure OpenAI, OpenAI directo, Ollama, o cualquier proveedor que implemente `IChatClient`. **No estás atado a ningún vendor.**

---

### Profundizando: Assistants API vs. Responses API (y cómo se relacionan con Foundry)

Esta es la confusión más común. Veamos las dos **APIs de OpenAI** que están detrás de estos agentes:

#### La historia: dos generaciones de APIs

```
2023-2024                          2025+
┌──────────────────────┐           ┌──────────────────────────────┐
│  Assistants API (v2) │           │      Responses API           │
│  ──────────────────  │           │  ────────────────────────    │
│  POST /assistants    │  ──────►  │  POST /responses             │
│  POST /threads       │  SUCESOR  │                              │
│  POST /threads/runs  │           │  Una sola llamada unificada  │
│                      │           │  que combina lo mejor de     │
│  Threads + Messages  │           │  Chat Completions +          │
│  (estado en servidor)│           │  Assistants                  │
└──────────────────────┘           └──────────────────────────────┘
                                   ↑ Foundry Agent Service usa esta
```

> **¿Y Chat Completions?** La API de Chat Completions (`POST /chat/completions`) sigue siendo la más usada y estable. Es la que usa `IChatClient` y por tanto la que usa **Microsoft Agent Framework** (`AIAgent`). No está deprecada — simplemente Responses es una capa adicional con más features.

#### Assistants API

La **Assistants API** fue lanzada por OpenAI en 2023 y mejorada con v2 en 2024. Fue la primera API que permitió crear agentes con estado persistente en el servidor.

**Cómo funciona:**
1. Creas un **Assistant** (definición persistente con instrucciones, modelo y tools)
2. Creas un **Thread** (una conversación)
3. Agregas **Messages** al thread
4. Creas un **Run** (el assistant procesa el thread)
5. Lees la respuesta del run

**Conceptos clave:** Assistant → Thread → Message → Run

**Herramientas nativas:** Code Interpreter, File Search (vector stores), Function Calling

**Disponibilidad en Azure OpenAI:** Sí, disponible a través de Azure OpenAI Service.

**Estado:** Funcional pero considerado la **generación anterior**. No recibirá features nuevos.

---

#### Responses API

La **Responses API** es la API **más nueva** de OpenAI (2025+). Fue diseñada para ser el **sucesor unificado** tanto de Chat Completions como de Assistants. Es la API que usa Foundry Agent Service internamente.

**Cómo funciona:**
1. Haces una sola llamada a `POST /responses` con tu input
2. La respuesta incluye un `response.id` que puedes encadenar con `previous_response_id`
3. No necesitas crear assistants ni threads por separado

**Concepto clave:** Response → Response → Response (encadenadas por ID)

```python
# Ejemplo con la Responses API directa (Python)
from openai import OpenAI

client = OpenAI(
    base_url="https://TU-RECURSO.openai.azure.com/openai/v1/",
    api_key=os.getenv("AZURE_OPENAI_API_KEY")
)

# Primera llamada
response = client.responses.create(
    model="gpt-4o",
    input="Explica qué es catastrophic forgetting."
)

# Segunda llamada encadenada (multi-turn)
response2 = client.responses.create(
    model="gpt-4o",
    previous_response_id=response.id,  # ← Así se encadena
    input="Explícalo para un estudiante de primer año."
)
```

**Herramientas nativas (más que Assistants):**
- **Code Interpreter** — Con containers sandboxed mejorados
- **File Search** — Búsqueda en archivos (PDFs, docs, etc.)
- **Web Search** — Búsqueda web nativa (¡nuevo! no existe en Assistants)
- **Image Generation** — Generación de imágenes con GPT Image models
- **MCP Servers** — Conexión directa a servidores MCP remotos (¡nuevo!)
- **Computer Use** — Control de computadora con Playwright (¡nuevo!)
- **Background Tasks** — Tareas asíncronas de larga duración (¡nuevo!)
- **Compaction** — Compresión automática de contexto para conversaciones largas (¡nuevo!)

**Disponibilidad en Azure OpenAI:** Sí, disponible en la mayoría de regiones de Azure.

**Estado:** GA en Azure OpenAI. Es la API principal de Foundry Agent Service.

---

#### Comparación lado a lado

| Aspecto | Assistants API | Responses API |
|---|---|---|
| **Lanzamiento** | 2023 (v1), 2024 (v2) | 2025 |
| **Endpoint** | `POST /assistants`, `/threads`, `/runs` | `POST /responses` (un solo endpoint) |
| **Estado de conversación** | Threads + Messages en servidor | Response IDs encadenados |
| **Modelo de recursos** | Assistant → Thread → Message → Run | Response → Response (encadenadas) |
| **Persistencia** | El Assistant persiste hasta que lo borres | Las responses se retienen 30 días (configurable) |
| **Herramientas** | Code Interpreter, File Search, Functions | Todo de Assistants + Web Search, MCP, Image Gen, Computer Use, Background Tasks |
| **Streaming** | Sí | Sí (mejorado, con resume de stream) |
| **Modelos soportados** | GPT-4o, GPT-4o-mini | GPT-4o, GPT-4.1, GPT-5, o3, o4-mini y más |
| **Compaction** | No | Sí (compresión automática de contexto largo) |
| **MCP nativo** | No | Sí (conexión directa a MCP servers) |
| **Background tasks** | No | Sí (tareas async de larga duración) |
| **Dirección futura** | Mantenida pero no recibirá features nuevos | **Es el futuro** — todos los features nuevos van aquí |
| **Usa Foundry Agent Service** | No | **Sí** — es la API interna de Foundry |

---

#### ¿Y en Foundry? ¿Cuál se usa?

| Contexto | API subyacente |
|---|---|
| **Foundry Agent Service (Prompt Agents, Workflows)** | **Responses API** — Es la API que Foundry usa internamente |
| **Hosted Agents en Foundry** | La que tú elijas en tu código (normalmente Chat Completions vía `IChatClient`) |
| **Microsoft Agent Framework (`AIAgent`)** | **Chat Completions** — Usa `IChatClient` que habla con el endpoint de chat completions |
| **Azure.AI.Projects.Agents SDK** | **Responses API** — SDK directo para interactuar con agentes de Foundry |

> **Conclusión práctica:** Si usas **Microsoft Agent Framework** (`AIAgent` + `IChatClient`), tu agente habla con el modelo vía **Chat Completions**, que es la API más estable y universal. Si necesitas conectarte directamente con **agentes que ya existen en Foundry Agent Service**, usas el SDK `Azure.AI.Projects.Agents`. Ambos enfoques son compatibles con Foundry — ya sea como Hosted Agent (tu código empaquetado como container) o como app auto-hospedada que consume modelos de Azure OpenAI.

---

### Paquetes NuGet (.NET)

```
Microsoft.Agents.AI.OpenAI                        # AIAgent + .AsAIAgent() para Azure OpenAI
Microsoft.Agents.AI.Workflows                     # Orquestaciones (Sequential, GroupChat, Handoff, etc.)
Azure.AI.OpenAI                                   # Cliente Azure OpenAI (IChatClient)
Azure.AI.Projects                                 # SDK para Foundry Projects
Azure.AI.Projects.Agents                          # SDK para Foundry Agent Service
```

> **No necesitas `Microsoft.SemanticKernel`**. Agent Framework está construido sobre `Microsoft.Extensions.AI` (`IChatClient`), que es un estándar .NET independiente de SK.

### Ejemplo Básico (C#)

```csharp
using Microsoft.Agents.AI;
using Azure.AI.OpenAI;
using Azure.Identity;

// Crear el agente — así de simple
AIAgent agent = new AzureOpenAIClient(
        new Uri(endpoint), new AzureCliCredential())
    .GetChatClient("gpt-4o")
    .AsAIAgent(instructions: "Eres un asistente útil. Responde de forma concisa.");

// Ejecutar (una sola respuesta)
string response = await agent.RunAsync("¿Cuál es la ciudad más grande de Francia?");

// Multi-turn con thread
var thread = agent.GetNewThread();
await foreach (var msg in agent.RunAsync("Hola, soy Jordan", thread))
    Console.WriteLine(msg);
await foreach (var msg in agent.RunAsync("¿Recuerdas mi nombre?", thread))
    Console.WriteLine(msg);
```

---

## ¿Qué es Foundry Agent Service?

**Foundry Agent Service** es una **plataforma administrada (PaaS)** dentro de Microsoft Foundry para construir, desplegar y escalar agentes de IA con seguridad empresarial.

### Características Principales

| Característica | Descripción |
|---|---|
| **Tipo** | Plataforma administrada (PaaS) en Azure |
| **Portal** | [ai.azure.com](https://ai.azure.com) (Microsoft Foundry Portal) |
| **Código requerido** | No obligatorio — tiene modo no-code y low-code |
| **Hosting** | Fully managed — Azure se encarga de todo |
| **Escalabilidad** | Auto-scaling administrado |
| **Seguridad** | Microsoft Entra ID, RBAC, content filters, VNet isolation |
| **Observabilidad** | Tracing end-to-end, métricas, Application Insights integrado |
| **Publicación** | Teams, Microsoft 365 Copilot, Entra Agent Registry, endpoints estables |

### Tres Tipos de Agentes en Agent Service

#### 1. Prompt Agents (No-Code) — GA
Agentes definidos completamente a través de configuración: instrucciones, modelo y herramientas. Se crean en el portal de Foundry sin escribir código.

**Mejor para:** Prototipado rápido, herramientas internas, agentes que no necesitan orquestación custom.

#### 2. Workflow Agents (Low-Code) — Preview
Orquestan secuencias de acciones o coordinan múltiples agentes usando definiciones declarativas. Se construyen visualmente en el portal o con YAML en VS Code.

**Mejor para:** Orquestación multi-paso, coordinación agente-a-agente, workflows de aprobación, automatización repetible sin código custom.

#### 3. Hosted Agents (Pro-Code) — Preview
Agentes basados en código, construidos con **cualquier framework** (Agent Framework, LangGraph, custom) y desplegados como **contenedores** en Agent Service.

**Mejor para:** Workflows complejos, integraciones custom, sistemas multi-agente, control total sobre el comportamiento del agente.

### Comparación de Tipos de Agentes

| Aspecto | Prompt Agent | Workflow Agent | Hosted Agent |
|---|---|---|---|
| **Código requerido** | No | No (YAML opcional) | Sí |
| **Hosting** | Fully managed | Fully managed | Container-based, VMs aisladas |
| **Orquestación** | Agente único | Multi-agente, branching | Lógica custom |
| **Mejor para** | Prototipado, tareas simples | Automatización multi-paso | Control total, frameworks custom |

---

## Comparación Directa: Agent Framework vs. Agent Service

| Dimensión | Agent Framework | Agent Service |
|---|---|---|
| **¿Qué es?** | SDK / librería de código | Plataforma administrada (PaaS) |
| **Capa** | Código / Framework | Infraestructura / Runtime |
| **Código requerido** | Siempre | No (Prompt/Workflow) o Sí (Hosted) |
| **Hosting** | Self-hosted (tú decides) | Fully managed por Azure |
| **Rendimiento** | Más rápido (ejecución local) | Más lento (ejecución remota con overhead) |
| **Control** | Total — tú escribes toda la lógica | Parcial a total (depende del tipo) |
| **Seguridad** | Implementación manual | Built-in: Entra ID, RBAC, content filters, VNet |
| **Escalabilidad** | Manual / app-managed | Auto-scaling administrado |
| **Observabilidad** | Manual (OpenTelemetry DIY) | Built-in: tracing, métricas, App Insights |
| **Publicación** | Manual (APIs propias) | Built-in: Teams, M365, endpoints estables |
| **Identidad del agente** | N/A — usa la identidad de tu app | Cada agente tiene su propio Microsoft Entra ID |
| **Multi-modelo** | Sí — tú configuras los providers | Sí — catálogo de modelos (GPT-4o, Llama, DeepSeek, Grok, etc.) |
| **Open Source** | Sí | No |
| **Costo del SDK/Servicio** | Gratis (open-source) | Sin cargo adicional por prompt/workflow agents |

---

## ¿Cuándo Usar Cada Uno?

### Usa **Agent Framework** (solo) cuando:

- Necesitas **máximo control y rendimiento** sobre la lógica del agente
- Quieres hospedar en tu **propia infraestructura** (on-premises, otras nubes, Docker local)
- Ya tienes un **stack de hosting** establecido (App Service, Container Apps, Kubernetes propio)
- Necesitas **latencia mínima** (ejecución local, sin overhead de plataforma)
- Quieres **evitar dependencia** de un servicio administrado específico
- Tu equipo tiene experiencia en C#/Python/Java y prefiere **código puro**

### Usa **Agent Service** (sin framework, Prompt/Workflow agents) cuando:

- Quieres **crear agentes rápidamente sin escribir código**
- Necesitas **seguridad empresarial out-of-the-box** (Entra ID, RBAC, content safety)
- Quieres **publicar directamente en Teams o Microsoft 365 Copilot**
- Necesitas **observabilidad integrada** (tracing, métricas, dashboards)
- Tu equipo es de **low-code / no-code** o necesitas **prototipos rápidos**
- Quieres **workflows visuales** con lógica de branching y human-in-the-loop

### Usa **Ambos Juntos** (Agent Framework + Agent Service como Hosted Agent) cuando:

- Necesitas **control total del código** Y **infraestructura administrada**
- Quieres escribir tu propia orquestación con Agent Framework pero **sin gestionar servidores**
- Necesitas **identidad de agente dedicada** (Microsoft Entra ID por agente)
- Quieres **auto-scaling, observabilidad y seguridad** sin implementarlo tú
- Necesitas publicar en **Teams/M365** con un agente de código custom
- Quieres usar **Toolbox de Foundry** (MCP servers, Web Search, File Search) desde tu código

---

## ¿Cómo se Combinan? (El Escenario Ideal)

```
┌─────────────────────────────────────────────────┐
│           Microsoft Foundry Portal              │
│              (ai.azure.com)                     │
├─────────────────────────────────────────────────┤
│                                                 │
│  ┌──────────────┐  ┌──────────────────────────┐│
│  │ Prompt Agent  │  │    Hosted Agent           ││
│  │ (no-code)     │  │  ┌──────────────────┐    ││
│  │               │  │  │ Tu código con     │    ││
│  │ Instrucciones │  │  │ Agent Framework   │    ││
│  │ + Modelo      │  │  │ (C# / Python)     │    ││
│  │ + Tools       │  │  │                    │    ││
│  │               │  │  │ - ChatCompletion  │    ││
│  │               │  │  │   Agent           │    ││
│  │               │  │  │ - Orchestrations  │    ││
│  │               │  │  │ - Custom Tools    │    ││
│  │               │  │  └──────────────────┘    ││
│  └──────────────┘  │                            ││
│                     │  Container en Micro-VM    ││
│  ┌──────────────┐  │  aislada por sesión       ││
│  │ Workflow      │  └──────────────────────────┘│
│  │ Agent         │                               │
│  │ (visual/YAML) │  ┌──────────────────────────┐│
│  │               │  │   Agent Service Runtime   ││
│  │ Orquesta      │  │   - Hosting & Scaling     ││
│  │ múltiples     │  │   - Identity (Entra ID)   ││
│  │ agentes       │  │   - Observability         ││
│  └──────────────┘  │   - Content Safety         ││
│                     │   - Publishing (Teams/M365)││
│                     └──────────────────────────┘│
└─────────────────────────────────────────────────┘
```

### Flujo Típico de Combinación

1. **Desarrollas** tu agente usando Agent Framework (Semantic Kernel) en C# o Python
2. **Empaquetas** tu código como container (Docker)
3. **Subes** la imagen a Azure Container Registry
4. **Despliegas** como Hosted Agent en Foundry Agent Service
5. Agent Service te da automáticamente:
   - Microsoft Entra ID dedicado para el agente
   - Endpoint estable para invocarlo
   - Auto-scaling por sesión (cada sesión = VM aislada)
   - Tracing y métricas con Application Insights
   - Publicación en Teams/M365 si lo necesitas

---

## Costos

### Agent Framework (SDK)

| Concepto | Costo |
|---|---|
| **SDK / NuGet packages** | **Gratis** (open-source, MIT license) |
| **Hosting** | Lo que cueste tu infraestructura (App Service, Container Apps, VM, etc.) |
| **Modelo de IA** | Tokens consumidos del modelo que uses (Azure OpenAI, OpenAI, etc.) |

> El SDK no tiene costo. Solo pagas por la infraestructura donde corras tu aplicación y por los tokens del modelo de IA.

### Agent Service (Plataforma)

| Concepto | Costo |
|---|---|
| **Prompt Agents** | **Sin cargo adicional** por crear/ejecutar agentes |
| **Workflow Agents** | **Sin cargo adicional** por crear/ejecutar workflows |
| **Hosted Agents** | Facturación por **consumo de cómputo** (vCPU/hora + Memoria GiB/hora) |
| **Modelos** | Tokens consumidos según [precios de Foundry Models](https://azure.microsoft.com/en-us/pricing/details/foundry-models/) |

#### Costos de Herramientas (Built-in Tools)

Estas son las herramientas que Agent Service ofrece integradas. Se cobran **por uso**, independiente de los tokens del modelo:

**1. File Search Storage — $0.11/GB por día (1 GB gratis)**

File Search te permite subir documentos (PDFs, Word, CSVs, etc.) y el servicio los indexa automáticamente en un **vector store** para que tu agente pueda buscar información relevante (RAG). El costo es por el almacenamiento del vector store, no por cada búsqueda.

- *Ejemplo:* Subes 500 MB de manuales técnicos → Costo = **$0.00/día** (cabe en el 1 GB gratis). Si subes 5 GB → Costo ≈ **$0.55/día** ($16.50/mes).
- Se cobra diariamente por el total de GB almacenados en vector stores activos.
- Si borras el vector store, dejas de pagar.

**2. Code Interpreter — $0.033/sesión**

Code Interpreter le da a tu agente un sandbox aislado donde puede escribir y ejecutar código Python. Es útil para análisis de datos, generar gráficas, procesar archivos Excel/CSV, o hacer cálculos complejos.

- *Ejemplo:* Un usuario pide "analiza este CSV y genera una gráfica" → Se crea **1 sesión** = $0.033.
- Una sesión dura hasta 1 hora activa, con timeout de inactividad de 20 minutos.
- Si dos usuarios piden Code Interpreter al mismo tiempo, son **2 sesiones** = $0.066.
- El costo es por sesión creada, no por línea de código ejecutada.

**3. Web Search — $14/1,000 transacciones**

Permite a tu agente buscar en internet en tiempo real para responder preguntas con información actualizada. Cada vez que el agente decide buscar en la web cuenta como una transacción.

- *Ejemplo:* Tu agente recibe 100 preguntas al día y en 30 de ellas necesita buscar en la web → 30 transacciones/día × 30 días = 900 transacciones/mes → Costo ≈ **$12.60/mes**.
- Una sola pregunta del usuario puede generar múltiples búsquedas si el agente decide hacer varias queries.

**4. Custom Search — $14/1,000 transacciones**

Similar a Web Search pero usa un **índice de búsqueda personalizado** (por ejemplo, buscar solo dentro de los sitios de tu empresa). Misma estructura de precios.

| Herramienta | Precio | Qué incluye | Free tier |
|---|---|---|---|
| **File Search Storage** | $0.11/GB/día | Almacenamiento de vector store (RAG) | 1 GB gratis |
| **Code Interpreter** | $0.033/sesión | Sandbox Python aislado por sesión | No |
| **Web Search** | $14/1K transacciones | Búsqueda en internet en tiempo real | No |
| **Custom Search** | $14/1K transacciones | Búsqueda en índice personalizado | No |

> **Otras conexiones** (Azure Logic Apps, Microsoft Fabric, SharePoint, Bing Search, Foundry IQ/Azure AI Search, Speech API, Language API, Translator API) tienen sus propios precios independientes según el servicio.

#### Costos de Hosted Agents (Preview)

| Recurso | Tamaños Disponibles |
|---|---|
| **Sandbox Sizes** | 0.5 vCPU / 1 GiB, 1 vCPU / 2 GiB, 2 vCPU / 4 GiB |
| **Facturación** | Por consumo de CPU + memoria durante sesiones activas |
| **Idle timeout** | 15 minutos (el cómputo se desprovisiona automáticamente) |
| **Sesión máxima** | 30 días |
| **Sesiones concurrentes** | 50 por suscripción/región (ajustable con soporte) |

> **Nota importante:** Cada sesión corre en su propio sandbox aislado. El costo de CPU y memoria se multiplica por el número de sesiones concurrentes. Sobredimensionar las VMs aumenta el costo proporcionalmente.

#### Plan de Pre-compra (Microsoft Agent Pre-Purchase Plan)

Microsoft ofrece un plan de pre-compra de agentes para empresas que necesitan compromisos de volumen. Contacta a tu representante de ventas de Azure para más detalles.

### Resumen de Costos por Escenario

| Escenario | Costos Involucrados |
|---|---|
| **Agent Framework auto-hospedado** | Infraestructura propia + tokens del modelo |
| **Prompt Agent en Agent Service** | Solo tokens del modelo + herramientas usadas |
| **Workflow Agent en Agent Service** | Solo tokens del modelo + herramientas usadas |
| **Hosted Agent en Agent Service** | Cómputo del container + tokens del modelo + herramientas usadas |
| **Agent Framework como Hosted Agent** | Cómputo del container + tokens del modelo + herramientas de Foundry |

---

## Tabla de Decisión Rápida

| Pregunta | Si tu respuesta es SÍ → |
|---|---|
| ¿Necesitas crear un agente rápido sin código? | **Agent Service** (Prompt Agent) |
| ¿Necesitas orquestar múltiples agentes visualmente? | **Agent Service** (Workflow Agent) |
| ¿Necesitas control total del código y la orquestación? | **Agent Framework** |
| ¿Necesitas control total PERO sin gestionar infraestructura? | **Agent Framework + Agent Service** (Hosted Agent) |
| ¿Necesitas publicar en Teams/M365 rápidamente? | **Agent Service** |
| ¿Necesitas ejecución on-premises o en otra nube? | **Agent Framework** (solo) |
| ¿Necesitas latencia mínima? | **Agent Framework** (auto-hospedado) |
| ¿Necesitas seguridad empresarial out-of-the-box? | **Agent Service** |
| ¿Necesitas identidad Entra ID por agente? | **Agent Service** (Hosted Agent) |
| ¿Tu equipo es low-code? | **Agent Service** (Prompt/Workflow) |

---

## Protocolos Soportados por Hosted Agents

Cuando combinas Agent Framework con Agent Service (Hosted Agents), puedes usar varios protocolos:

| Protocolo | Uso | Descripción |
|---|---|---|
| **Responses** | Chatbots, Q&A con RAG, multi-turn | La plataforma gestiona historial, streaming y sesiones |
| **Invocations** | Webhooks, procesamiento async, payloads custom | JSON arbitrario, control total del HTTP |
| **Activity** | Teams, Microsoft 365 | Se integra automáticamente con Responses para delivery en canales |
| **A2A** (Preview) | Agente-a-agente | Delegación entre agentes |

> **Tip:** Si no estás seguro, comienza con **Responses**. Siempre puedes agregar Invocations después — un Hosted Agent puede soportar ambos protocolos simultáneamente.

---

## Regiones Disponibles (Agent Service)

### Hosted Agents (Preview)
East US 2, North Central US, Sweden Central, Canada Central, Canada East, Southeast Asia, Poland Central, South Africa North, Korea Central, South India, Brazil South, West US, West US 3, Norway East, Japan East, France Central, Germany West Central, Switzerland North, Spain Central, Australia East.

### Prompt/Workflow Agents
Disponibles en todas las regiones que soporten la [Azure OpenAI Responses API](https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/responses#supported-regions).

---

## Conclusión

| | Agent Framework | Agent Service | Ambos Juntos |
|---|---|---|---|
| **Control** | ★★★★★ | ★★☆☆☆ (Prompt) / ★★★★★ (Hosted) | ★★★★★ |
| **Facilidad** | ★★☆☆☆ | ★★★★★ (Prompt) / ★★★☆☆ (Hosted) | ★★★★☆ |
| **Seguridad Enterprise** | ★★☆☆☆ (DIY) | ★★★★★ | ★★★★★ |
| **Rendimiento** | ★★★★★ | ★★★☆☆ | ★★★★☆ |
| **Costo inicial** | Bajo (solo infra) | Muy bajo (solo tokens) | Medio (cómputo + tokens) |

**No es "uno u otro"** — la mejor arquitectura para producción empresarial es frecuentemente **Agent Framework ejecutándose como Hosted Agent dentro de Agent Service**, obteniendo lo mejor de ambos mundos: control total del código con infraestructura administrada, seguridad enterprise, observabilidad y publicación integradas.

---

## Referencias

- [What is Microsoft Foundry Agent Service?](https://learn.microsoft.com/en-us/azure/foundry/agents/overview)
- [Semantic Kernel Agent Framework](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/)
- [Agent Framework Architecture](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-architecture)
- [What are Hosted Agents?](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agents)
- [Agent Development Lifecycle](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/development-lifecycle)
- [Foundry Agent Service Pricing](https://azure.microsoft.com/en-us/pricing/details/foundry-agent-service/)
- [Quotas, Limits, and Regional Support](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/limits-quotas-regions)
- [Build a Workflow in Microsoft Foundry](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/workflow)
- [Tutorial: Agentic App with Agent Framework or Foundry Agent Service](https://learn.microsoft.com/en-us/azure/app-service/tutorial-ai-agent-web-app-semantic-kernel-foundry-dotnet)
