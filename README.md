# SE-AgentFramework

**Plataforma de referencia para construir sistemas multi-agente con [Microsoft Agent Framework SDK](https://github.com/microsoft/agents) y Azure OpenAI.** Demuestra patrones de orquestación, workflows, integración con Microsoft Teams, Model Context Protocol (MCP), Azure AI Foundry, y más — todo en una solución .NET 9.

---

## ¿Qué es este proyecto?

SE-AgentFramework es una implementación completa y funcional que muestra cómo diseñar, orquestar y publicar agentes de IA usando el stack de Microsoft. No es un SDK ni una librería — es una **aplicación de referencia** que puedes clonar, explorar y adaptar a tus propios escenarios.

El objetivo es demostrar de forma práctica:

- Cómo crear agentes de IA con `Microsoft.Agents.AI` y conectarlos a Azure OpenAI
- Cómo orquestar múltiples agentes con patrones de la industria (Sequential, GroupChat, FanOut, Conditional, Iterative)
- Cómo publicar agentes en Microsoft Teams, Web, y Azure AI Foundry
- Cómo integrar herramientas externas vía plugins nativos y Model Context Protocol (MCP)
- Cómo implementar RAG con Azure AI Search, consultas SQL, Bing Grounding, y más
- Cómo diseñar una UI web con streaming SSE, theming dinámico y Adaptive Cards

---

## Estructura de la solución

```
SE-AgentFramework.sln
│
├── 02-AFWebChat/              ← Aplicación principal
│   ├── Agents/                   35+ agentes organizados por categoría
│   ├── Orchestrations/           Orquestación multi-agente (Sequential, GroupChat, GroupChatAI)
│   ├── Workflows/                Workflows (Iterative, Conditional, FanOut)
│   ├── Bot/                      Integración Bot Framework para Teams
│   ├── Controllers/              API REST (Chat, Agents, Sessions, Proactive)
│   ├── Services/                 ChatClientFactory, SessionService, Orchestration
│   ├── Tools/Plugins/            16 plugins (SQL, Search, MCP, Bing, Web Scraping...)
│   ├── Middleware/               Auditoría, Logging, Métricas
│   ├── Views/                    UI Razor (Chat, Documents, Notifications)
│   └── wwwroot/                  CSS, JS, assets del frontend
│
└── 01-AgentConsole/           ← (Reservado para demo de consola)
```

---

## Capacidades principales

### Agentes de IA (35+)

| Categoría | Agentes | Descripción |
|---|---|---|
| **Básico** | GeneralAssistant, Translator, Summarizer | Conversación general, traducción, resúmenes |
| **Herramientas** | DatabaseQuery, WebSearch, Lights, Weather, FileManager | Agentes con tool-calling (function calling) |
| **Dominio** | SqlAzure, LegalAdvisor, CodeReviewer, BingGrounding, AzureSearch | Especializados con conocimiento de dominio |
| **Empresarial** | MultiAgentPlanner, DataStoryteller | Orquestadores que combinan SQL + RAG + Web |
| **Structured Output** | EntityExtractor, SentimentAnalyzer | Salida JSON estructurada |
| **Multimodal** | Vision | Análisis de imágenes con GPT-4o |
| **Composite** | ResearchAssistant | Investigación multi-paso |
| **MCP** | McpTools | Herramientas vía Model Context Protocol |
| **Foundry** | FoundrySimpleBot, FoundryOrchestrator | Agentes publicados en Azure AI Foundry |
| **Approval** | DataModifier | Agente con aprobación humana antes de ejecutar |
| **Workflow** | 17 agentes especializados | Colaboran en orquestaciones de negocio |

### Patrones de orquestación

| Patrón | Tipo | Descripción |
|---|---|---|
| **Sequential** | Orchestration | Agentes se ejecutan uno tras otro, pasándose el contexto |
| **Concurrent** | Orchestration | Agentes se ejecutan en paralelo |
| **GroupChat (Round-Robin)** | Orchestration | Agentes discuten por turnos como en una junta |
| **GroupChat (AI Moderator)** | Orchestration | Un LLM decide quién habla según el contexto |
| **Handoff** | Orchestration | Un agente delega a otro dinámicamente |
| **Iterative** | Workflow | Writer↔Reviewer loop hasta aprobación |
| **Conditional (Switch)** | Workflow | Clasificador IA enruta al agente correcto |
| **FanOut** | Workflow | Ejecución paralela + síntesis |

### Canales de publicación

| Canal | Tecnología | Características |
|---|---|---|
| **Web Chat** | ASP.NET + SSE | Streaming en tiempo real, theming, markdown |
| **Microsoft Teams** | Bot Framework + Adaptive Cards | Cards interactivas, notificaciones proactivas |
| **Azure AI Foundry** | `Azure.AI.Projects` SDK | Agentes versionados con RBAC y trazabilidad |
| **REST API** | HTTP endpoints | Integración con cualquier cliente |

---

## Requisitos

| Componente | Mínimo | Notas |
|---|---|---|
| **.NET SDK** | 9.0+ | |
| **Azure OpenAI** | Deployment `gpt-4o` | Endpoint + API Key o `DefaultAzureCredential` |
| **Node.js** | 18+ | Solo si usas MCP Server |
| **Azure Bot** | | Solo si publicas en Teams |

### Servicios opcionales

| Servicio | Para qué |
|---|---|
| Azure AI Search | RAG (Retrieval Augmented Generation) |
| Azure SQL Database | Agentes de consulta SQL |
| Azure Blob Storage | Subir y procesar documentos |
| Azure Document Intelligence | OCR y extracción de documentos |
| Bing Search API | Grounding con búsqueda web |
| Azure AI Foundry | Publicar agentes como servicio |
| Azure Cosmos DB | Persistencia de sesiones (opcional) |

---

## Inicio rápido

### 1. Clonar el repositorio

```bash
git clone https://github.com/JordanReyesLeger/agent-framework-web-chat.git
cd agent-framework-web-chat
```

### 2. Configurar secretos

Crea `02-AFWebChat/appsettings.Development.json` (ya está en `.gitignore`):

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://tu-recurso.openai.azure.com/",
    "ApiKey": "tu-api-key",
    "ChatDeployment": "gpt-4o",
    "EmbeddingDeployment": "text-embedding-3-large"
  }
}
```

> Si no proporcionas `ApiKey`, se usará `DefaultAzureCredential` (requiere `az login`).

### 3. Ejecutar

```bash
cd 02-AFWebChat
dotnet run
```

Abre `https://localhost:5001/Home/Chat` en tu navegador.

---

## Stack tecnológico

| Tecnología | Versión | Propósito |
|---|---|---|
| **Microsoft 365 Agents SDK** | 1.4.83 | Hosting de Bot Framework |
| **Microsoft.Agents.AI** | 1.0.0-rc5 | Runtime de agentes IA |
| **Microsoft.Agents.AI.Workflows** | 1.0.0-rc5 | GroupChat, WorkflowBuilder |
| **Microsoft.Agents.AI.Foundry** | 1.1.0 | Integración Azure AI Foundry |
| **Azure.AI.OpenAI** | 2.9.0 | SDK de Azure OpenAI |
| **Azure.AI.Projects** | 2.0.0 | Foundry Agent Service |
| **Azure.Search.Documents** | 11.8.0 | Azure AI Search (RAG) |
| **Azure.Identity** | 1.20.0 | DefaultAzureCredential |
| **ModelContextProtocol** | 1.0.0 | MCP client para tools externos |
| **AdaptiveCards** | 3.1.0 | UI rica en Teams |
| **.NET** | 9.0 | Runtime |
| **Bootstrap** | 5.3 | UI web |

---

## Configuración de secretos

El proyecto usa el patrón estándar de ASP.NET Core para separar configuración sensible:

| Archivo | Se sube al repo | Propósito |
|---|---|---|
| `appsettings.json` | ✅ Sí | Estructura base, valores por defecto (sin secretos) |
| `appsettings.Development.json` | ❌ No | Tus valores reales (API keys, connection strings) |

ASP.NET Core carga `appsettings.json` primero y luego sobreescribe con `appsettings.Development.json` cuando `ASPNETCORE_ENVIRONMENT=Development`. No necesitas cambiar nada en código.

---

## Documentación adicional

| Documento | Descripción |
|---|---|
| [02-AFWebChat/README.md](02-AFWebChat/README.md) | Documentación detallada del proyecto principal |
| [02-AFWebChat/THEMING.md](02-AFWebChat/THEMING.md) | Guía de personalización visual y branding |
| [02-AFWebChat/docs/TEAMS_INTEGRATION_GUIDE.md](02-AFWebChat/docs/TEAMS_INTEGRATION_GUIDE.md) | Guía paso a paso para publicar en Teams |

---

## Licencia

Este proyecto es una implementación de referencia con fines educativos y de demostración.
