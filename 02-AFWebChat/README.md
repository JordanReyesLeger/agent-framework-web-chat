# AF-WebChat

Aplicación web multi-agente construida con **[Microsoft Agent Framework SDK](https://github.com/microsoft/agents)** y **Azure OpenAI**. Permite crear, orquestar y publicar agentes de IA a través de una interfaz web interactiva con streaming en tiempo real, Microsoft Teams con Adaptive Cards, y Azure AI Foundry como servicio versionado.

![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4) ![Azure OpenAI](https://img.shields.io/badge/Azure%20OpenAI-GPT--4o-0078D4) ![Bot Framework](https://img.shields.io/badge/Bot%20Framework-Teams-6264A7) ![License](https://img.shields.io/badge/License-MIT-green)

---

## Tabla de contenidos

- [Arquitectura](#arquitectura)
- [Requisitos](#requisitos)
- [Inicio rápido](#inicio-rápido)
- [Configuración detallada](#configuración-detallada)
- [Catálogo de agentes](#catálogo-de-agentes)
- [Patrones de orquestación](#patrones-de-orquestación)
- [Workflows](#workflows)
- [Plugins y herramientas](#plugins-y-herramientas)
- [Canales de publicación](#canales-de-publicación)
- [API REST](#api-rest)
- [Mensajes proactivos](#mensajes-proactivos-notificaciones)
- [Theming y branding](#theming-y-branding)
- [Estructura del proyecto](#estructura-del-proyecto)
- [Despliegue con Docker](#despliegue-con-docker)
- [Stack tecnológico](#stack-tecnológico)

---

## Arquitectura

La aplicación sigue una arquitectura por capas donde múltiples canales de entrada (Web, Teams, API) convergen en un servicio de orquestación central que enruta peticiones a agentes registrados dinámicamente.

```
┌────────────────────────────────────────────────────────────────┐
│                         AF-WebChat                             │
│                                                                │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐ │
│  │  Web Chat UI │  │  REST API    │  │  Bot Framework       │ │
│  │  (SSE Stream)│  │  /api/chat/* │  │  /api/messages       │ │
│  │  Razor Views │  │  HTTP + JSON │  │  Teams / WebChat     │ │
│  └──────┬───────┘  └──────┬───────┘  └──────────┬───────────┘ │
│         │                 │                      │             │
│         └────────────┬────┴──────────────────────┘             │
│                      ▼                                         │
│         ┌─────────────────────────┐                            │
│         │  AgentOrchestrationSvc  │ ← Enrutador central       │
│         │  (Single Agent /        │                            │
│         │   Orchestration /       │                            │
│         │   Workflow / Custom)    │                            │
│         └────────────┬────────────┘                            │
│                      ▼                                         │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                  Agent Registry                          │   │
│  │  ┌─────────┐ ┌──────────┐ ┌───────────┐ ┌───────────┐  │   │
│  │  │ Básicos │ │  Tools   │ │  Dominio  │ │Enterprise │  │   │
│  │  │ 3 agents│ │ 6 agents │ │ 9 agents  │ │ 2 agents  │  │   │
│  │  └─────────┘ └──────────┘ └───────────┘ └───────────┘  │   │
│  │  ┌─────────┐ ┌──────────┐ ┌───────────┐ ┌───────────┐  │   │
│  │  │  MCP    │ │ Foundry  │ │ Structured│ │ Workflow  │  │   │
│  │  │ 1 agent │ │ 2 agents │ │ 2 agents  │ │ 13 agents │  │   │
│  │  └─────────┘ └──────────┘ └───────────┘ └───────────┘  │   │
│  └────────────────────────┬────────────────────────────────┘   │
│                           ▼                                    │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                     Tool Layer                            │  │
│  │  SQL │ Azure Search │ Bing │ MCP │ Blob │ Web Scraping   │  │
│  └──────────────────────────┬───────────────────────────────┘  │
│                             ▼                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  Azure OpenAI  │  Azure AI Search  │  Azure AI Foundry   │  │
│  └──────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────┘
```

### Flujo de una petición

1. El usuario envía un mensaje desde Web Chat, Teams o API REST
2. El `AgentOrchestrationService` determina el tipo de ejecución:
   - **Agente individual** → Obtiene el agente del registro y ejecuta con historial de sesión
   - **Orquestación nombrada** → Ejecuta un patrón multi-agente predefinido (Sequential, GroupChat, etc.)
   - **Workflow nombrado** → Ejecuta un workflow (Iterative, Conditional, FanOut)
   - **Custom** → Construye una orquestación ad-hoc con agentes y patrón elegidos desde la UI
3. Cada agente usa `ChatClientFactory` para comunicarse con Azure OpenAI
4. Los agentes pueden invocar **plugins** (SQL, Search, Bing, MCP, etc.) durante la ejecución
5. La respuesta se envía al cliente como stream SSE (web) o actividad Bot Framework (Teams)

---

## Requisitos

### Mínimos (para chat web básico)

| Componente | Versión | Notas |
|---|---|---|
| **.NET SDK** | 9.0+ | [Descargar](https://dotnet.microsoft.com/download/dotnet/9.0) |
| **Azure OpenAI** | — | Recurso con deployment `gpt-4o` |
| **Autenticación** | — | API Key o `DefaultAzureCredential` (`az login`) |

### Opcionales (para funcionalidades avanzadas)

| Servicio | Agentes que lo usan | Propósito |
|---|---|---|
| Azure AI Search | AzureSearch, LegalAdvisor, RAGAgent, SkillIndex | Retrieval Augmented Generation |
| Azure SQL Database | SqlAzure, DatabaseQuery, DataStoryteller, MultiAgentPlanner | Consultas a bases de datos |
| Azure Blob Storage | DocumentService | Subida y procesamiento de documentos |
| Azure Document Intelligence | DocumentService | OCR y extracción de texto de PDFs/imágenes |
| Bing Search API | BingGrounding, WebSearch | Búsqueda web en tiempo real |
| Azure AI Foundry | FoundrySimpleBot, FoundryOrchestrator | Publicar agentes como servicio versionado |
| Azure Cosmos DB | SessionService | Persistencia duradera de sesiones |
| Azure Bot + Entra ID | TeamsBotAgent | Publicación en Microsoft Teams |
| Node.js 18+ | McpTools | Model Context Protocol server |

---

## Inicio rápido

### 1. Clonar y restaurar

```bash
git clone https://github.com/tu-usuario/SE-AgentFramework.git
cd SE-AgentFramework/02-AFWebChat
dotnet restore
```

### 2. Configurar secretos

Crea `appsettings.Development.json` (ya está en `.gitignore` — nunca se sube al repo):

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://{tu-recurso}.openai.azure.com/",
    "ApiKey": "tu-api-key-aqui",
    "ChatDeployment": "gpt-4o",
    "EmbeddingDeployment": "text-embedding-3-large"
  }
}
```

> **Sin API Key:** Si omites `ApiKey`, se usará `DefaultAzureCredential`. Ejecuta `az login` primero y asegúrate de tener el rol **Cognitive Services OpenAI User** en el recurso.

### 3. Ejecutar

```bash
dotnet run
```

### 4. Abrir el chat

Navega a `https://localhost:5001/Home/Chat` — selecciona un agente del panel lateral y comienza a chatear.

---

## Configuración detallada

El proyecto usa el patrón estándar de ASP.NET Core para separar configuración:

| Archivo | Se sube al repo | Contenido |
|---|---|---|
| `appsettings.json` | ✅ Sí | Estructura completa con valores vacíos/por defecto |
| `appsettings.Development.json` | ❌ No | Tus valores reales (API keys, connection strings, secretos) |

ASP.NET Core carga ambos automáticamente y `Development.json` sobreescribe los valores de `appsettings.json`. No necesitas código adicional.

### Todas las secciones configurables

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://{recurso}.openai.azure.com/",
    "EndpointProject": "https://{recurso}.services.ai.azure.com/api/projects/{proyecto}",
    "ApiKey": "",
    "ChatDeployment": "gpt-4o",
    "EmbeddingDeployment": "text-embedding-3-large"
  },
  "DevTunnel": {
    "Url": "https://{id}.devtunnels.ms"
  },
  "AzureSearch": {
    "Endpoint": "https://{recurso}.search.windows.net",
    "ApiKey": "",
    "IndexName": "skill",
    "LegalIndexName": "legal-documents",
    "SkillIndexName": "skill",
    "SemanticConfigName": "skill-semantic-config",
    "DefaultMaxResults": 15
  },
  "ConnectionStrings": {
    "SqlServer": "Data Source=tcp:{server}.database.windows.net,1433;...",
    "INEGICenso": "..."
  },
  "BlobStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net",
    "ContainerName": "documents"
  },
  "AzureStorage": {
    "AccountName": "",
    "ResourceGroup": "",
    "ContainerName": "skill-documents",
    "UseDefaultCredential": true,
    "ConnectionString": ""
  },
  "Azure": {
    "SubscriptionId": "",
    "ResourceGroup": "",
    "TenantId": ""
  },
  "McpServer": {
    "Name": "GitHub MCP Server",
    "Command": "npx",
    "Arguments": "-y @modelcontextprotocol/server-github",
    "GitHubToken": ""
  },
  "BingSearch": {
    "ApiKey": ""
  },
  "CosmosDB": {
    "ConnectionString": "",
    "DatabaseName": "af-webchat",
    "ContainerName": "sessions"
  }
}
```

---

## Catálogo de agentes

### Básicos — Conversación general

| Agente | Icono | Descripción | Herramientas |
|---|---|---|---|
| **GeneralAssistant** | 🤖 | Asistente conversacional de propósito general. Responde preguntas, redacta textos, explica conceptos | — |
| **Translator** | 🌐 | Traductor multiidioma con detección automática del idioma de entrada | — |
| **Summarizer** | 📝 | Resume textos largos, artículos, documentos y conversaciones | — |

### Herramientas — Agentes con function calling

| Agente | Icono | Descripción | Herramientas |
|---|---|---|---|
| **DatabaseQuery** | 🔍 | Consultas SQL interactivas con esquema automático | GetSchema, QuerySql |
| **WebSearch** | 🌐 | Búsqueda web con Bing y scraping de páginas | SearchWithBing, ScrapeWebPage |
| **Lights** | 💡 | Demo de function calling — controla luces virtuales | GetLights, SetLightState |
| **Weather** | 🌤️ | Demo de function calling — consulta clima simulado | GetWeather |
| **FileManager** | 📁 | Gestión de archivos en Blob Storage | ListFiles, UploadFile, DeleteFile |
| **OrderAgents** | 📦 | Demo de procesamiento de pedidos | CreateOrder, GetOrderStatus |

### Dominio — Especializados con conocimiento profundo

| Agente | Icono | Descripción | Herramientas |
|---|---|---|---|
| **SqlAzure** | 🗄️ | Experto en SQL que genera y ejecuta queries seguros (solo SELECT) | GetSchema, GetTableSchema, QuerySql, QuerySqlTabular |
| **LegalAdvisor** | ⚖️ | Asesor legal que consulta un índice de documentos jurídicos | SearchLegalDocuments |
| **CodeReviewer** | 👨‍💻 | Revisa código, detecta bugs y sugiere mejoras | — |
| **BingGrounding** | 🔎 | Respuestas fundamentadas con información actualizada de internet | SearchWithBingGrounding |
| **AzureSearch** | 📚 | Agente RAG que busca en índices de Azure AI Search | SearchDocuments |
| **SkillIndex** | 🎯 | Busca perfiles de competencias en un índice especializado | SearchSkillIndex |
| **TriageAgent** | 🏥 | Clasifica y enruta solicitudes al agente apropiado | — |
| **FoundrySimpleBot** | 🏗️ | Bot simple publicado en Azure AI Foundry | — |
| **FoundryOrchestrator** | 🏗️ | Orquestador de Foundry con herramienta OpenAPI que llama de vuelta a AF-WebChat | af-webchat-api (OpenAPI) |

### INEGI — Datos censales de México

| Agente | Icono | Descripción | Herramientas |
|---|---|---|---|
| **INEGICenso** | 📊 | Consulta datos del Censo de Población y Vivienda 2020 a nivel AGEB | QueryCenso, GetOntology |

### Empresarial — Orquestadores avanzados

| Agente | Icono | Descripción | Herramientas |
|---|---|---|---|
| **MultiAgentPlanner** | 🧠 | Combina datos SQL + documentos RAG + búsqueda web para planes de proyecto completos | GetSchema, QuerySql, SearchDocuments, ScrapeWebPage, SearchWithBingGrounding |
| **DataStoryteller** | 📈 | Transforma datos SQL crudos en narrativas ejecutivas con KPIs, tendencias y recomendaciones | GetSchema, GetTableSchema, QuerySql, QuerySqlTabular |

### Structured Output — Salida JSON tipada

| Agente | Icono | Descripción | Salida |
|---|---|---|---|
| **EntityExtractor** | 🏷️ | Extrae entidades nombradas (personas, organizaciones, lugares, fechas) | JSON con array de entidades |
| **SentimentAnalyzer** | 😊 | Analiza sentimiento de texto (positivo, negativo, neutro) con score | JSON con sentimiento y score |

### Multimodal — Más allá del texto

| Agente | Icono | Descripción | Capacidades |
|---|---|---|---|
| **Vision** | 👁️ | Analiza y describe imágenes usando GPT-4o vision | Imágenes vía URL o base64 |

### Composite — Multi-paso

| Agente | Icono | Descripción | Patrón |
|---|---|---|---|
| **ResearchAssistant** | 🔬 | Investigación en profundidad: busca, analiza fuentes, sintetiza | Agent-to-Agent |

### MCP — Model Context Protocol

| Agente | Icono | Descripción | Conexión |
|---|---|---|---|
| **McpTools** | 🔌 | Descubre y usa herramientas de servidores MCP externos dinámicamente | Configurable (GitHub, etc.) |

### Approval — Con aprobación humana

| Agente | Icono | Descripción | Patrón |
|---|---|---|---|
| **DataModifier** | ✋ | Solicita aprobación del usuario antes de ejecutar operaciones de modificación | Human-in-the-loop |

### Workflow — Agentes internos para orquestaciones

Estos agentes no aparecen en la barra lateral. Son especialistas que colaboran dentro de orquestaciones de negocio:

**Plan de Proyecto:**
- **AnalistaDeNegocio** — Analiza requerimientos y genera especificaciones funcionales
- **EstimadorDeCostos** — Estima esfuerzo, recursos y presupuesto
- **PlanificadorDeProyecto** — Crea cronogramas y planes ejecutables

**Reporte Ejecutivo:**
- **AnalistaDeDatos** — Procesa datos y genera insights cuantitativos
- **RedactorEjecutivo** — Redacta narrativas ejecutivas
- **DiseñadorDePresentacion** — Estructura el reporte para presentación a directivos

**Propuesta Comercial:**
- **ConsultorDeVentas** — Identifica oportunidades y necesidades del cliente
- **EspecialistaEnSolucion** — Diseña la solución técnica
- **GeneradorDePropuesta** — Integra todo en una cotización formal

**Equipo de Desarrollo (GroupChat):**
- **Desarrollador** — Implementación y código
- **Arquitecto** — Diseño técnico y patrones
- **ProjectManager** — Gestión de alcance y riesgos
- **DBA** — Diseño de datos y consultas

---

## Patrones de orquestación

Las orquestaciones permiten que múltiples agentes colaboren en una sola petición. Se configuran en `OrchestrationFactory.cs`.

### Orquestaciones predefinidas

| Nombre | Patrón | Agentes | Descripción |
|---|---|---|---|
| **PlanDeProyecto** | Sequential | AnalistaDeNegocio → EstimadorDeCostos → PlanificadorDeProyecto | De la idea al plan ejecutable con tiempos y presupuesto |
| **ReporteEjecutivo** | Sequential | AnalistaDeDatos → RedactorEjecutivo → DiseñadorDePresentacion | Datos crudos → presentación para directivos |
| **PropuestaComercial** | Sequential | ConsultorDeVentas → EspecialistaEnSolucion → GeneradorDePropuesta | Oportunidad comercial → cotización formal |
| **EquipoDesarrollo** | GroupChat (Round-Robin) | Desarrollador, Arquitecto, PM, DBA | Junta de equipo donde cada miembro habla por turnos |
| **EquipoDesarrolloAI** | GroupChat (AI Moderator) | Desarrollador, Arquitecto, PM, DBA | Un LLM moderador decide quién habla según el contexto |

### Patrones disponibles

| Patrón | Cómo funciona |
|---|---|
| **Sequential** | Los agentes se ejecutan uno tras otro. La salida de cada agente se pasa como input al siguiente |
| **Concurrent** | Todos los agentes se ejecutan en paralelo sobre el mismo input |
| **GroupChat** | Round-robin: cada agente habla por turnos, todos ven el historial completo |
| **GroupChatAI** | Un LLM analiza el historial y decide dinámicamente quién debe hablar, incluyendo cuándo terminar |
| **Handoff** | Un agente puede delegar a otro dinámicamente basado en el contexto |

### Orquestaciones custom desde la UI

La interfaz web permite construir orquestaciones ad-hoc seleccionando agentes y patrón:

```json
{
  "message": "Diseña un plan de migración a la nube",
  "customAgents": ["GeneralAssistant", "SqlAzure", "CodeReviewer"],
  "customPattern": "Sequential"
}
```

---

## Workflows

Los workflows usan `WorkflowBuilder` y `AgentWorkflowBuilder` del SDK para patrones más sofisticados. Se configuran en `WorkflowFactory.cs`.

### Workflows predefinidos

| Nombre | Patrón | Agentes | Descripción |
|---|---|---|---|
| **RedaccionIterativa** | Iterative | GeneralAssistant ↔ DataStoryteller | Writer redacta, Reviewer evalúa. Se itera hasta aprobación o max iteraciones |
| **EnrutamientoInteligente** | Conditional | Clasificador → (GeneralAssistant \| RAGAgent \| DatabaseQuery \| SqlAzure) | Un LLM clasifica la petición y la enruta al especialista correcto |
| **AnalisisParalelo** | FanOut | SentimentAnalyzer + EntityExtractor → GeneralAssistant | NLP paralelo: sentimiento y entidades se analizan simultáneamente, luego se sintetizan |

### Patrones de workflow

| Patrón | Cómo funciona |
|---|---|
| **Iterative** | Loop Writer↔Reviewer con máximo de iteraciones. El Reviewer decide si aprobar o pedir revisión |
| **Conditional (Switch)** | Un agente clasificador analiza el input y produce un JSON `{selected_index, reason}` para enrutar |
| **FanOut** | Ejecución paralela de N agentes + agente sintetizador que integra todos los resultados |

---

## Plugins y herramientas

Los plugins se registran como singletons en `Program.cs` y se asignan a agentes vía el `ToolRegistry`. Cada plugin expone funciones que los agentes pueden invocar durante el razonamiento.

| Plugin | Archivo | Funciones expuestas | Requiere |
|---|---|---|---|
| **SqlPlugin** | `Plugins/SqlPlugin.cs` | Queries SQL genéricos | ConnectionStrings:SqlServer |
| **GetSchemaPlugin** | `Plugins/GetSchemaPlugin.cs` | `GetSchema`, `GetTableSchema` | ConnectionStrings:SqlServer |
| **QuerySqlPlugin** | `Plugins/QuerySqlPlugin.cs` | `QuerySql`, `QuerySqlTabular` | ConnectionStrings:SqlServer |
| **AzureSearchPlugin** | `Plugins/AzureSearchPlugin.cs` | `SearchDocuments` | AzureSearch:Endpoint |
| **LegalIndexPlugin** | `Plugins/LegalIndexPlugin.cs` | `SearchLegalDocuments` | AzureSearch:LegalIndexName |
| **SkillIndexPlugin** | `Plugins/SkillIndexPlugin.cs` | `SearchSkillIndex` | AzureSearch:SkillIndexName |
| **BingGroundingPlugin** | `Plugins/BingGroundingPlugin.cs` | `SearchWithBingGrounding` | BingSearch:ApiKey |
| **WebScrapingPlugin** | `Plugins/WebScrapingPlugin.cs` | `ScrapeWebPage` | — |
| **WebSearchPlugin** | `Plugins/WebSearchPlugin.cs` | `SearchWithBing` | BingSearch:ApiKey |
| **McpServerPlugin** | `Plugins/McpServerPlugin.cs` | `ListMcpTools`, `CallMcpTool` + tools dinámicos | McpServer config + Node.js |
| **INEGICensoPlugin** | `Plugins/INEGICensoPlugin.cs` | `QueryCenso` | ConnectionStrings:INEGICenso |
| **INEGIOntologyPlugin** | `Plugins/INEGIOntologyPlugin.cs` | `GetOntology` | Bundle JSON incluido |
| **LightsPlugin** | `Plugins/LightsPlugin.cs` | `GetLights`, `SetLightState` | — (demo) |
| **WeatherPlugin** | `Plugins/WeatherPlugin.cs` | `GetWeather` | — (demo) |
| **OrderPlugins** | `Plugins/OrderPlugins.cs` | `CreateOrder`, `GetOrderStatus` | — (demo) |
| **FileManagerPlugin** | `Plugins/FileManagerPlugin.cs` | `ListFiles`, `UploadFile`, `DeleteFile` | BlobStorage |

---

## Canales de publicación

### Canal 1: Web Chat (Browser)

Interfaz web completa con streaming en tiempo real.

**URL:** `https://localhost:5001/Home/Chat`

**Características:**
- Streaming de respuestas via **Server-Sent Events (SSE)**
- Panel lateral con todos los agentes agrupados por categoría
- Selector de orquestaciones y workflows
- Constructor de orquestaciones custom (selecciona agentes + patrón)
- Renderizado de Markdown con syntax highlighting
- Theming dinámico configurable desde `appsettings.json`
- Vista standalone embeddable: `/Home/AgentChat`

**Para embeder en otra página:**
```html
<iframe src="https://{tu-dominio}/Home/AgentChat" 
        width="100%" height="700px" frameborder="0"></iframe>
```

### Canal 2: Microsoft Teams (Bot Framework)

Bot publicado como app de Teams con Adaptive Cards y notificaciones proactivas.

**Características:**
- Adaptive Cards para bienvenida, lista de agentes, respuestas y notificaciones
- Botones interactivos en las cards
- Proactive messaging / notificaciones push vía API
- Sesiones independientes por agente con historial persistente
- Protección contra mensajes duplicados (Teams retries)
- Comandos: `/agents`, `/agent {nombre}`, `/clear`, `/new`, `/help`

**Archivos clave:**
- `Bot/TeamsBotAgent.cs` — Bridge entre Bot Framework y la orquestación
- `Bot/AdaptiveCardBuilder.cs` — Construye las Adaptive Cards
- `Bot/ConversationReferenceStore.cs` — Almacena referencias para proactive messaging
- `Bot/AspNetAuthExtensions.cs` — Validación JWT para el Bot Framework

**Configuración:** Ver la [guía completa de integración con Teams](docs/TEAMS_INTEGRATION_GUIDE.md).

**Pasos resumidos:**
1. Crear App Registration en Entra ID (Single Tenant)
2. Crear Azure Bot con canal de Teams habilitado
3. Configurar `TokenValidation`, `Connections` y `ConnectionsMap` en `appsettings.Development.json`
4. Exponer `/api/messages` con Dev Tunnel o App Service
5. Crear manifest ZIP y sideload en Teams

### Canal 3: Azure AI Foundry

Agentes versionados publicados como servicio en Azure AI Foundry con herramientas OpenAPI.

**Configuración:**
```json
{
  "AzureOpenAI": {
    "EndpointProject": "https://{recurso}.services.ai.azure.com/api/projects/{proyecto}"
  }
}
```

**Dos modos:**
- **FoundrySimpleBot** — Bot básico declarativo en Foundry
- **FoundryOrchestrator** — Agente versionado con herramienta OpenAPI que llama de vuelta al API de AF-WebChat para delegar trabajo a agentes especializados

---

## API REST

### Endpoints de Chat

| Endpoint | Método | Descripción | Body |
|---|---|---|---|
| `/api/chat/stream` | POST | Chat con streaming SSE | `ChatRequest` |
| `/api/chat/send` | POST | Chat sin streaming | `ChatRequest` |
| `/api/chat/approve` | POST | Aprobar/rechazar tool call | `ApprovalResponse` |

**ChatRequest:**
```json
{
  "sessionId": "unique-session-id",
  "message": "Tu mensaje aquí",
  "agentName": "GeneralAssistant",
  "orchestrationName": "PlanDeProyecto",
  "workflowName": "RedaccionIterativa",
  "customAgents": ["Agent1", "Agent2"],
  "customPattern": "Sequential"
}
```

> Usa **uno** de: `agentName`, `orchestrationName`, `workflowName`, o `customAgents`+`customPattern`.

### Endpoints de Agentes

| Endpoint | Método | Descripción |
|---|---|---|
| `/api/agent` | GET | Listar todos los agentes registrados |
| `/api/agent/{name}` | GET | Obtener info de un agente específico |
| `/api/agent` | POST | Crear agente custom en runtime |
| `/api/agent/{name}` | DELETE | Eliminar un agente custom |
| `/api/agent/tools` | GET | Listar tool sets disponibles |

### Endpoints de Orquestación

| Endpoint | Método | Descripción |
|---|---|---|
| `/api/agentworkflow/orchestrations` | GET | Listar orquestaciones disponibles |
| `/api/agentworkflow/workflows` | GET | Listar workflows disponibles |

### Endpoints de Sesión

| Endpoint | Método | Descripción |
|---|---|---|
| `/api/session` | GET | Listar sesiones activas |
| `/api/session/{id}` | GET | Obtener info de una sesión |
| `/api/session/{id}` | DELETE | Eliminar una sesión |

### Endpoints de Documentos

| Endpoint | Método | Descripción |
|---|---|---|
| `/api/document/upload` | POST | Subir documento (PDF, DOCX, etc.) |
| `/api/document/index` | POST | Indexar documento en Azure AI Search |

### Endpoints Proactivos (Teams)

| Endpoint | Método | Descripción |
|---|---|---|
| `/api/proactive/conversations` | GET | Listar conversaciones conectadas |
| `/api/proactive/notify` | POST | Enviar notificación a un usuario |
| `/api/proactive/broadcast` | POST | Broadcast a todos los usuarios |

### Vistas Web

| URL | Descripción |
|---|---|
| `/Home/Chat` | Chat principal con panel de agentes |
| `/Home/AgentChat` | Chat standalone (embeddable, sin layout) |
| `/Home/Documents` | Gestión de documentos |
| `/Home/Notifications` | Panel de notificaciones proactivas |
| `/Home/PublishingDemo` | Vista comparativa de canales |

---

## Mensajes proactivos (Notificaciones)

El sistema permite enviar notificaciones push a usuarios de Teams que hayan interactuado con el bot.

**UI de administración:** `https://{tu-dominio}/Home/Notifications`

**API:**
```bash
# Listar conversaciones conectadas
curl https://{tu-dominio}/api/proactive/conversations

# Notificación dirigida
curl -X POST https://{tu-dominio}/api/proactive/notify \
  -H "Content-Type: application/json" \
  -d '{
    "conversationKey": "19:abc...",
    "title": "Deploy completado",
    "message": "La versión 2.1 está en producción",
    "severity": "success"
  }'

# Broadcast a todos
curl -X POST https://{tu-dominio}/api/proactive/broadcast \
  -H "Content-Type: application/json" \
  -d '{"message": "Mantenimiento programado a las 10pm"}'
```

**Severidades disponibles:** `info` (🔔), `success` (✅), `warning` (⚠️), `error` (🚨)

**Funcionamiento:** Las ConversationReferences se almacenan automáticamente cada vez que un usuario interactúa con el bot. Las notificaciones se entregan como Adaptive Cards.

---

## Theming y branding

La apariencia de la app se controla completamente desde `appsettings.json` sin modificar CSS. Ver la [guía completa de theming](THEMING.md).

**Presets incluidos en `branding-presets.json`:**

| Preset | Tema | Industria | Estilo |
|---|---|---|---|
| `Microsoft_Dark` | Dark | Tecnología | Azul Microsoft |
| `Microsoft_Light` | Light | Tecnología | Azul Microsoft |
| `Corporativo_Dorado_Dark` | Dark | Enterprise | Dorado + Azul oscuro |
| `Banco_Azul_Light` | Light | Banca | Azul corporativo |
| `Salud_Verde_Light` | Light | Salud | Verde clínico |
| `Gobierno_Rojo_Dark` | Dark | Gobierno | Rojo + Verde |

**Cambio rápido:** Copia un preset de `branding-presets.json` a la sección `AppBranding` de `appsettings.json` y reinicia.

---

## Estructura del proyecto

```
02-AFWebChat/
├── Agents/                           # Definiciones de 30+ agentes
│   ├── AgentDefinition.cs               # Clase base: nombre, categoría, icono, factory
│   ├── AgentRegistry.cs                 # Registro singleton con lazy instantiation
│   ├── Approval/                        # Agentes con aprobación humana
│   │   └── DataModifierAgent.cs
│   ├── Basic/                           # Agentes conversacionales básicos
│   │   ├── GeneralAssistantAgent.cs
│   │   ├── SummarizerAgent.cs
│   │   └── TranslatorAgent.cs
│   ├── Composite/                       # Agentes que llaman a otros agentes
│   │   └── ResearchAssistantAgent.cs
│   ├── ContextAware/                    # Agentes con contexto externo (RAG, memoria)
│   │   ├── MemoryAgent.cs
│   │   └── RAGAgent.cs
│   ├── Domain/                          # Agentes especializados por dominio
│   │   ├── AzureSearchAgent.cs
│   │   ├── BingGroundingAgent.cs
│   │   ├── CodeReviewerAgent.cs
│   │   ├── FoundryAgent.cs              # Foundry Orchestrator (versioned + OpenAPI)
│   │   ├── FoundrySimpleBotAgent.cs     # Foundry Simple Bot (declarativo)
│   │   ├── LegalAdvisorAgent.cs
│   │   ├── SkillIndexAgent.cs
│   │   ├── SqlAzureAgent.cs
│   │   └── TriageAgent.cs
│   ├── Enterprise/                      # Agentes empresariales avanzados
│   │   ├── DataStorytellerAgent.cs      # SQL → Narrativas ejecutivas
│   │   └── MultiAgentPlannerAgent.cs    # SQL + RAG + Web → Plan de proyecto
│   ├── INEGI/                           # Datos censales de México
│   │   ├── INEGICensoAgent.cs
│   │   └── bundle_censo_2020_ageb/      # Ontología del censo
│   ├── Mcp/
│   │   └── McpToolsAgent.cs             # Model Context Protocol
│   ├── Multimodal/
│   │   └── VisionAgent.cs               # Análisis de imágenes
│   ├── StructuredOutput/                # Salida JSON tipada
│   │   ├── EntityExtractorAgent.cs
│   │   └── SentimentAnalyzerAgent.cs
│   ├── Tools/                           # Agentes con function calling
│   │   ├── DatabaseQueryAgent.cs
│   │   ├── FileManagerAgent.cs
│   │   ├── LightsAgent.cs
│   │   ├── OrderAgents.cs
│   │   ├── WeatherAgent.cs
│   │   └── WebSearchAgent.cs
│   └── Workflow/                        # 13 agentes internos para orquestaciones
│       ├── AnalistaDeDatosAgent.cs
│       ├── AnalistaDeNegocioAgent.cs
│       ├── ArquitectoAgent.cs
│       ├── ConsultorDeVentasAgent.cs
│       ├── DBAAgent.cs
│       ├── DesarrolladorAgent.cs
│       ├── DiseñadorDePresentacionAgent.cs
│       ├── EspecialistaEnSolucionAgent.cs
│       ├── EstimadorDeCostosAgent.cs
│       ├── GeneradorDePropuestaAgent.cs
│       ├── PlanificadorDeProyectoAgent.cs
│       ├── ProjectManagerAgent.cs
│       └── RedactorEjecutivoAgent.cs
│
├── appManifest/                      # Manifest para Teams
│   ├── manifest.json
│   ├── color.png
│   └── outline.png
│
├── Bot/                              # Integración con Bot Framework
│   ├── TeamsBotAgent.cs                 # Bridge Bot Framework ↔ Orchestration
│   ├── AdaptiveCardBuilder.cs           # Construye Adaptive Cards para Teams
│   ├── AspNetAuthExtensions.cs          # Validación JWT
│   └── ConversationReferenceStore.cs    # Store para proactive messaging
│
├── Controllers/                      # API REST
│   ├── AgentWorkflowController.cs       # CRUD de agentes + lista de orchestrations/workflows
│   ├── ChatController.cs               # /api/chat/stream, /api/chat/send
│   ├── DocumentController.cs           # Upload y indexación de documentos
│   ├── HomeController.cs               # Vistas web (Chat, AgentChat, etc.)
│   ├── ProactiveController.cs          # Notificaciones push a Teams
│   └── SessionController.cs            # Gestión de sesiones
│
├── ContextProviders/                 # Proveedores de contexto para agentes
│   ├── AzureSearchRAGProvider.cs        # Inyecta resultados de Azure AI Search
│   ├── ConversationMemoryProvider.cs    # Inyecta historial de conversación
│   └── UserPreferencesProvider.cs       # Inyecta preferencias del usuario
│
├── Middleware/                       # Pipeline middleware
│   ├── AuditMiddleware.cs               # Auditoría de tool calls (función, args, resultado)
│   ├── LoggingMiddleware.cs             # Logging estructurado de peticiones
│   ├── MetricsMiddleware.cs             # Métricas de latencia y uso
│   └── MiddlewareExtensions.cs          # Extension methods para registrar middleware
│
├── Models/                           # DTOs y modelos de datos
│   ├── AgentInfo.cs                     # Información pública de un agente
│   ├── AppBrandingSettings.cs           # Configuración de branding/theming
│   ├── ChatRequest.cs                   # Request del chat (message, agent, orchestration)
│   ├── ChatResponse.cs                  # Response del chat (no-streaming)
│   ├── CommonModels.cs                  # OrchestrationInfo, WorkflowInfo, SessionInfo, etc.
│   ├── CreateAgentRequest.cs            # Request para crear agente custom
│   ├── IndexConfiguration.cs            # Config de indexación de documentos
│   ├── StreamEvent.cs                   # Evento SSE (type, agent, content, metadata)
│   └── StructuredOutputModels.cs        # Modelos para EntityExtractor, SentimentAnalyzer
│
├── Orchestrations/                   # Multi-agente orchestration
│   ├── AIGroupChatManager.cs            # Moderador IA que decide quién habla (Structured Output)
│   └── OrchestrationFactory.cs          # Catálogo + ejecución de orquestaciones
│
├── Services/                         # Servicios de negocio
│   ├── AgentOrchestrationService.cs     # Enrutador central (agent/orchestration/workflow/custom)
│   ├── BlobStorageService.cs            # Operaciones con Azure Blob Storage
│   ├── ChatClientFactory.cs             # Factory para Azure OpenAI (API Key o DefaultAzureCredential)
│   ├── DocumentIndexingService.cs       # Indexación de documentos en Azure AI Search
│   ├── DocumentService.cs              # Upload, parsing y procesamiento de documentos
│   ├── SessionService.cs               # Gestión de sesiones con serialización/deserialización
│   └── StreamEventService.cs           # Factory para eventos SSE tipados
│
├── Tools/                            # Tool registry y plugins
│   ├── AIFunctionFactoryExtensions.cs   # Crea AITool[] desde instancias de plugins
│   ├── ToolRegistry.cs                  # Registro central de tool sets por nombre
│   └── Plugins/                         # 16 plugins (ver tabla de plugins arriba)
│
├── Views/                            # Razor views
│   ├── Home/
│   │   ├── Chat.cshtml                  # Chat principal con panel de agentes
│   │   ├── Documents.cshtml             # Gestión de documentos
│   │   ├── Index.cshtml                 # Landing page
│   │   ├── Notifications.cshtml         # Panel de notificaciones proactivas
│   │   └── PublishingDemo.cshtml        # Vista comparativa de canales
│   └── Shared/                          # Layouts y partials
│
├── Workflows/                        # Workflow patterns
│   ├── WorkflowFactory.cs              # Catálogo + ejecución (Iterative, Conditional, FanOut)
│   └── SharedState/
│       └── WorkflowStateModels.cs       # Modelos de estado compartido
│
├── wwwroot/                          # Assets del frontend
│   ├── css/                             # Estilos (theming dinámico con CSS variables)
│   └── js/                              # JavaScript (SSE client, markdown render, etc.)
│
├── docs/                             # Documentación adicional
│   └── TEAMS_INTEGRATION_GUIDE.md       # Guía completa de integración con Teams
│
├── Program.cs                        # Configuración, DI, registro de agentes y pipeline
├── appsettings.json                  # Configuración base (sin secretos)
├── appsettings.Development.json      # Secretos locales (NO se sube al repo)
├── branding-presets.json             # Presets de branding para demos
├── Dockerfile                        # Build multi-stage para contenedor Linux
├── THEMING.md                        # Guía de personalización visual
└── AF-WebChat.csproj                 # Proyecto .NET 9 con todas las dependencias
```

---

## Despliegue con Docker

```bash
# Desde la raíz del repositorio
docker build -t af-webchat -f 02-AFWebChat/Dockerfile .
docker run -p 8080:8080 \
  -e AzureOpenAI__Endpoint="https://tu-recurso.openai.azure.com/" \
  -e AzureOpenAI__ApiKey="tu-api-key" \
  af-webchat
```

> En Docker, pasa los secretos como variables de entorno usando `__` como separador de secciones (convención de ASP.NET Core).

---

## Crear un agente nuevo

### 1. Crear el archivo del agente

```csharp
// Agents/Basic/MiAgenteAgent.cs
using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Basic;

public static class MiAgenteAgent
{
    public const string Name = "MiAgente";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Descripción de lo que hace tu agente",
        Category = "Básico",
        Icon = "🚀",
        Color = "#FF6B35",
        ExamplePrompts = ["Ejemplo 1", "Ejemplo 2"],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: "Tus instrucciones de sistema aquí...");
        }
    };
}
```

### 2. Registrarlo en `Program.cs`

```csharp
registry.Register(MiAgenteAgent.CreateDefinition());
```

### 3. (Opcional) Agregar herramientas

Si tu agente necesita tools, crea un plugin en `Tools/Plugins/` y agrégalo al factory:

```csharp
Factory = sp =>
{
    var factory = sp.GetRequiredService<ChatClientFactory>();
    var chatClient = factory.CreateChatClient();
    var miPlugin = sp.GetRequiredService<MiPlugin>();

    var tools = new List<AITool>();
    tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(miPlugin));

    return chatClient.AsAIAgent(
        name: Name,
        instructions: "...",
        tools: tools);
}
```

---

## Stack tecnológico

| Tecnología | Versión | Propósito |
|---|---|---|
| **.NET** | 9.0 | Runtime |
| **Microsoft 365 Agents SDK** | 1.4.83 | Bot Framework hosting y autenticación |
| **Microsoft.Agents.AI** | 1.0.0-rc5 | Runtime de agentes IA (`AIAgent`, `AgentSession`) |
| **Microsoft.Agents.AI.Workflows** | 1.0.0-rc5 | `GroupChatManager`, `WorkflowBuilder`, `AgentWorkflowBuilder` |
| **Microsoft.Agents.AI.Foundry** | 1.1.0 | Integración Azure AI Foundry (`AsAIAgent(agentRecord)`) |
| **Azure.AI.OpenAI** | 2.9.0 | SDK de Azure OpenAI |
| **Azure.AI.Projects** | 2.0.0 | Foundry Agent Service (`AgentAdministrationClient`) |
| **Azure.Search.Documents** | 11.8.0 | Azure AI Search para RAG |
| **Azure.AI.DocumentIntelligence** | 1.0.0 | OCR y extracción de documentos |
| **Azure.Storage.Blobs** | 12.26.0 | Almacenamiento de documentos |
| **Azure.Identity** | 1.20.0 | `DefaultAzureCredential` |
| **ModelContextProtocol** | 1.0.0 | MCP client para tools externos |
| **AdaptiveCards** | 3.1.0 | UI rica en Teams |
| **Microsoft.Data.SqlClient** | 6.0.1 | Consultas a Azure SQL |
| **Bootstrap** | 5.3 | UI web responsive |
| **marked.js** + **highlight.js** | — | Renderizado de markdown + syntax highlighting |
