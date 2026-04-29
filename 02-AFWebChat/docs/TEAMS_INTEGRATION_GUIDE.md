# Integración de AF-WebChat con Microsoft Teams

Guía técnica para integrar una aplicación AF-WebChat existente con Microsoft Teams y M365 Copilot.  
Dirigida a desarrolladores que **ya tienen una app AF-WebChat funcionando** via web y quieren publicarla como bot en Teams.

---

## Tabla de Contenidos

1. [Visión General](#1-visión-general)
2. [Arquitectura — Cómo Encaja Teams](#2-arquitectura--cómo-encaja-teams)
3. [El Modelo de Activity](#3-el-modelo-de-activity)
4. [Componentes de la Integración](#4-componentes-de-la-integración)
5. [TeamsBotAgent — El Puente](#5-teamsbotagenel-puente)
6. [Adaptive Cards — UI Rica en Teams](#6-adaptive-cards--ui-rica-en-teams)
7. [Autenticación y Seguridad](#7-autenticación-y-seguridad)
8. [Manifest de Teams](#8-manifest-de-teams)
9. [M365 Copilot — Custom Engine Agent](#9-m365-copilot--custom-engine-agent)
10. [Mensajes Proactivos](#10-mensajes-proactivos)
11. [Configuración Paso a Paso](#11-configuración-paso-a-paso)
12. [Referencia de Archivos](#12-referencia-de-archivos)

---

## 1. Visión General

AF-WebChat ya funciona como una app web con múltiples agentes AI. La integración con Teams **no reemplaza ni duplica** el código existente — agrega un **nuevo canal de entrada** que reutiliza la misma orquestación, los mismos agentes y las mismas sesiones.

### Antes (solo Web)

```
Browser → /api/chat/stream → AgentOrchestrationService → Azure OpenAI
```

### Después (Web + Teams + Copilot)

```
Browser    → /api/chat/stream  ─┐
Teams      → /api/messages     ─┼→ AgentOrchestrationService → Azure OpenAI
M365 Copilot → /api/messages   ─┘
```

**Principio clave:** Un solo backend, múltiples canales de entrada, cero duplicación de lógica.

---

## 2. Arquitectura — Cómo Encaja Teams

```
┌─────────────────────────────────────────────────────────────────┐
│                     Canales de Entrada                         │
│                                                                 │
│  ┌──────────┐   ┌──────────────┐   ┌────────────────────────┐  │
│  │ Browser  │   │ Teams/Copilot│   │  WebChat (Azure Portal)│  │
│  │ /chat/   │   │ /api/messages│   │  /api/messages         │  │
│  │ stream   │   │              │   │                        │  │
│  └────┬─────┘   └──────┬───────┘   └───────────┬────────────┘  │
│       │                │                        │               │
│       │     ┌──────────▼────────────┐          │               │
│       │     │  Azure Bot Service    │          │               │
│       │     │  (Auth + Routing)     ◄──────────┘               │
│       │     └──────────┬────────────┘                          │
│       │                │                                        │
│       │     ┌──────────▼────────────┐                          │
│       │     │   TeamsBotAgent       │                          │
│       │     │   (Bridge class)      │                          │
│       │     └──────────┬────────────┘                          │
│       │                │                                        │
│       └────────┬───────┘                                        │
│                ▼                                                │
│    ┌───────────────────────────┐                               │
│    │  AgentOrchestrationService│  ← Misma instancia            │
│    │  (Pipeline compartido)    │                               │
│    └─────────────┬─────────────┘                               │
│                  ▼                                              │
│    ┌───────────────────────────┐                               │
│    │     Agent Registry        │                               │
│    │  GeneralAssistant, SQL,   │                               │
│    │  Legal, Translator, etc.  │                               │
│    └─────────────┬─────────────┘                               │
│                  ▼                                              │
│    ┌───────────────────────────┐                               │
│    │      Azure OpenAI         │                               │
│    └───────────────────────────┘                               │
└─────────────────────────────────────────────────────────────────┘
```

### ¿Qué es Azure Bot Service?

Azure Bot Service es un servicio de Microsoft que actúa como **proxy de autenticación y routing** entre los canales (Teams, WebChat, Slack, etc.) y tu app. No ejecuta lógica — solo valida tokens, enruta mensajes y gestiona la conectividad.

Tu app nunca habla directamente con Teams. El flujo es:

```
Usuario Teams → Microsoft Teams Cloud → Azure Bot Service → POST /api/messages → Tu App
```

La respuesta viaja en sentido inverso por el mismo camino.

---

## 3. El Modelo de Activity

Toda comunicación entre Teams y tu bot se basa en **Activities** — objetos JSON que representan eventos.

### Tipos de Activity

| `Activity.Type` | Cuándo se dispara | Ejemplo |
|---|---|---|
| `message` | El usuario envía un mensaje de texto | "Hola, ¿qué puedes hacer?" |
| `conversationUpdate` | Alguien se une/sale del chat | El bot fue instalado, nuevo miembro |
| `invoke` | Acción de Adaptive Card (submit) | Click en botón "Cambiar agente" |
| `messageReaction` | Reacción a un mensaje | 👍 en una respuesta del bot |
| `installationUpdate` | Bot instalado/desinstalado | Admin añade el bot al canal |
| `typing` | Indicador de "escribiendo..." | Bot está procesando |

### Anatomía de una Activity de mensaje

Cuando un usuario escribe "Hola" en Teams, tu bot recibe:

```json
{
  "type": "message",
  "id": "1776229621877",
  "timestamp": "2026-04-15T02:00:21.877Z",
  "channelId": "msteams",
  "from": {
    "id": "29:1abc...",
    "name": "Jordan Reyes"
  },
  "conversation": {
    "id": "19:abc...@thread.v2",
    "tenantId": "{{YOUR_TENANT_ID}}"
  },
  "recipient": {
    "id": "28:{{YOUR_CLIENT_ID}}",
    "name": "AF-WebChat"
  },
  "text": "Hola",
  "serviceUrl": "https://smba.trafficmanager.net/amer/...",
  "channelData": { /* datos específicos de Teams */ }
}
```

### Propiedades clave

| Propiedad | Descripción | Uso en AF-WebChat |
|---|---|---|
| `channelId` | Canal de origen (`msteams`, `m365extensions`, `webchat`) | Decidir si enviar Adaptive Card o texto plano |
| `from.name` | Nombre del usuario | Logging |
| `conversation.id` | ID único de la conversación | Clave para sesión y selección de agente |
| `serviceUrl` | URL para enviar respuestas de vuelta | Usado internamente por el SDK |
| `text` | El mensaje del usuario | Se pasa al `AgentOrchestrationService` |
| `value` | Datos de Adaptive Card submit | Se extrae el comando del botón pulsado |

---

## 4. Componentes de la Integración

### Paquetes NuGet requeridos

```xml
<!-- En tu .csproj -->
<PackageReference Include="Microsoft.Agents.Hosting.AspNetCore" Version="1.4.83" />
<PackageReference Include="Microsoft.Agents.AI" Version="1.0.0-rc5" />
<PackageReference Include="AdaptiveCards" Version="3.1.0" />
```

### Registros en `Program.cs`

```csharp
// ---- Bot Framework (Teams / WebChat channel) ----
builder.Services.AddHttpClient();
builder.AddAgentApplicationOptions();        // Lee config de "AgentApplication" section
builder.AddAgent<TeamsBotAgent>();            // Registra tu bot como el handler
builder.Services.AddSingleton<IStorage, MemoryStorage>();
builder.AddAgentAspNetAuthentication(builder.Configuration);  // JWT validation

// ...

// Endpoint que Teams llama
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());
```

### ¿Qué hace cada línea?

| Método | Qué hace |
|---|---|
| `AddAgentApplicationOptions()` | Lee `appsettings.json` → sección `"AgentApplication"` (opciones del bot como remove mentions) |
| `AddAgent<TeamsBotAgent>()` | Registra `TeamsBotAgent` como singleton. El SDK detecta el atributo `[Agent]` |
| `AddSingleton<IStorage, MemoryStorage>()` | Storage para el state del bot (en memoria) |
| `AddAgentAspNetAuthentication()` | Configura JWT validation para validar tokens de Azure Bot Service |
| `MapAgentApplicationEndpoints()` | Registra `POST /api/messages` y lo conecta al bot |

### Archivos nuevos necesarios

```
AF-WebChat/
├── Bot/
│   ├── TeamsBotAgent.cs              ← Clase principal (bridge)
│   ├── AdaptiveCardBuilder.cs        ← Constructor de Adaptive Cards
│   ├── ConversationReferenceStore.cs ← Storage para proactive messaging
│   └── AspNetAuthExtensions.cs       ← JWT token validation
├── appManifest/
│   ├── manifest.json                 ← Manifest de Teams
│   ├── color.png                     ← Ícono 192x192
│   └── outline.png                   ← Ícono 32x32 (outline)
```

---

## 5. TeamsBotAgent — El Puente

`TeamsBotAgent` es la **única clase** que necesitas escribir para conectar Teams con tu pipeline de agentes. Hereda de `AgentApplication` (del Microsoft 365 Agents SDK).

### Estructura

```csharp
[Agent(name: "AFWebChatBot", description: "Multi-agent bot", version: "1.0")]
public class TeamsBotAgent : AgentApplication
{
    // Constructor: registra handlers de eventos
    public TeamsBotAgent(AgentApplicationOptions options, ...) : base(options)
    {
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }
}
```

### El atributo `[Agent]`

```csharp
[Agent(name: "AFWebChatBot", description: "...", version: "1.0")]
```

Este atributo le dice al SDK que esta clase es el bot. Cuando `MapAgentApplicationEndpoints()` registra `/api/messages`, sabe que debe rutear las Activities a esta clase.

### Registro de handlers

En el constructor registras qué Activities quieres escuchar:

```csharp
// Cuando alguien se une al chat → enviar bienvenida
OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

// Cuando llega un mensaje de texto → procesar con el pipeline
OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
```

`RouteRank.Last` significa: "ejecuta este handler solo si ningún otro handler más específico lo procesó primero".

### Flujo de `OnMessageAsync`

```
1. Guardar ConversationReference (para proactive messaging futuro)
2. Verificar duplicados (Teams puede re-enviar el mismo mensaje)
3. Entregar notificaciones proactivas pendientes
4. Extraer texto (del mensaje o de un Adaptive Card submit)
5. ¿Es slash command? → HandleCommandAsync (/agents, /agent, /help, /clear, /new)
6. Determinar qué agente usar (por conversación, o default GeneralAssistant)
7. Generar sessionId: "teams-{conversationId}-{agentName}"
8. Enviar typing indicator ("escribiendo...")
9. Crear scope de DI → obtener AgentOrchestrationService
10. Crear ChatRequest con sessionId, texto y agentName
11. Consumir RunStreamingAsync() acumulando tokens
12. Según channelId:
    - "m365extensions" → enviar texto plano (Copilot no soporta cards)
    - otro → enviar Adaptive Card con la respuesta formateada
```

### Sesiones por agente

Cada combinación de conversación + agente tiene su propia sesión:

```csharp
private static string GetSessionId(ITurnContext turnContext, string agentName)
    => $"teams-{turnContext.Activity.Conversation.Id}-{agentName}";
```

Esto significa que si un usuario usa `GeneralAssistant` y luego cambia a `LegalAdvisor`, cada uno tiene su propio historial. Al volver a `GeneralAssistant`, el historial previo sigue ahí.

### Selección de agente por conversación

El agente activo se almacena en un `Dictionary<string, string>` estático:

```csharp
// Clave: conversationId → Valor: nombre del agente
private static readonly Dictionary<string, string> _conversationAgents = new();
```

- El usuario escribe `/agent LegalAdvisor` → se guarda en el diccionario
- Todos los mensajes siguientes en esa conversación van a `LegalAdvisor`
- `/new` resetea al agente default y borra el historial

### Protección contra duplicados

Teams puede re-enviar un mensaje si tu bot tarda en responder (timeout ~15s). Para evitar procesar dos veces:

```csharp
private static readonly ConcurrentDictionary<string, byte> _processedActivities = new();

// En OnMessageAsync:
var activityId = turnContext.Activity.Id;
if (!string.IsNullOrEmpty(activityId) && !_processedActivities.TryAdd(activityId, 0))
{
    _logger.LogWarning("Duplicate activity {ActivityId} — skipping", activityId);
    return;
}
```

---

## 6. Adaptive Cards — UI Rica en Teams

Teams soporta **Adaptive Cards** — un formato JSON para UI rica con texto, imágenes, tablas, botones y formularios. En AF-WebChat usamos `AdaptiveCardBuilder` para construirlas con el SDK de C#.

### Cards que usamos

| Card | Método | Cuándo se envía |
|---|---|---|
| Welcome | `CreateWelcomeCard()` | Cuando el usuario instala el bot |
| Response | `CreateResponseCard(agent, icon, text)` | Cada respuesta del agente |
| Agent List | `CreateAgentListCard(agents)` | Comando `/agents` |
| Agent Switch | `CreateAgentSwitchCard(name, icon, desc)` | Comando `/agent <name>` |
| Help | `CreateHelpCard()` | Comando `/help` |
| Notification | `CreateNotificationCard(title, msg, severity)` | Notificaciones proactivas |

### Ejemplo: Response Card

```csharp
var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 5))
{
    Body =
    [
        new AdaptiveColumnSet
        {
            Columns =
            [
                new AdaptiveColumn
                {
                    Width = "auto",
                    Items = [new AdaptiveTextBlock { Text = "🤖" }]
                },
                new AdaptiveColumn
                {
                    Width = "stretch",
                    Items =
                    [
                        new AdaptiveTextBlock { Text = "**GeneralAssistant**", IsSubtle = true },
                        new AdaptiveTextBlock { Text = responseText, Wrap = true }
                    ]
                }
            ]
        }
    ]
};
```

### Envío condicional por canal

M365 Copilot no renderiza Adaptive Cards — solo muestra texto plano. Por eso detectamos el canal:

```csharp
var channelId = turnContext.Activity.ChannelId;
if (channelId == "m365extensions")
{
    // Copilot → solo texto
    await turnContext.SendActivityAsync(MessageFactory.Text(finalText), cancellationToken);
}
else
{
    // Teams/WebChat → Adaptive Card rica
    var card = AdaptiveCardBuilder.CreateResponseCard(agentName, agentIcon, finalText);
    await turnContext.SendActivityAsync(MessageFactory.Attachment(card), cancellationToken);
}
```

### ChannelId por canal

| Canal | `ChannelId` |
|---|---|
| Microsoft Teams | `msteams` |
| M365 Copilot | `m365extensions` |
| WebChat (Azure Portal) | `webchat` |
| Slack | `slack` |
| Direct Line | `directline` |

---

## 7. Autenticación y Seguridad

### ¿Cómo valida el bot que el mensaje viene de Teams?

Cada POST a `/api/messages` incluye un **JWT token** en el header `Authorization`. Este token es firmado por Azure Bot Service. Tu app lo valida con `AspNetAuthExtensions.cs`:

```csharp
builder.AddAgentAspNetAuthentication(builder.Configuration);
```

### Configuración en `appsettings.json`

```json
{
  "TokenValidation": {
    "Enabled": true,
    "Audiences": ["tu-client-id"],
    "TenantId": "tu-tenant-id"
  },
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "ClientSecret",
        "AuthorityEndpoint": "https://login.microsoftonline.com/{TenantId}",
        "ClientId": "{ClientId}",
        "ClientSecret": "{ClientSecret}",
        "Scopes": ["https://api.botframework.com/.default"]
      }
    }
  },
  "ConnectionsMap": [
    { "ServiceUrl": "*", "Connection": "ServiceConnection" }
  ]
}
```

### ¿Qué es cada valor?

| Valor | Qué es | De dónde sale |
|---|---|---|
| `ClientId` | ID de tu App Registration en Entra ID | Azure Portal → App Registrations |
| `ClientSecret` | Secret de la app | Azure Portal → Certificates & Secrets |
| `TenantId` | ID de tu tenant de Azure AD | Azure Portal → Entra ID → Overview |
| `Audiences` | Quién puede enviar tokens a tu bot | Debe coincidir con tu ClientId |

### En desarrollo

Para desarrollo local, `TokenValidation.Enabled` puede ser `false` y `MapAgentApplicationEndpoints(requireAuth: false)` desactiva la validación:

```csharp
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());
```

---

## 8. Manifest de Teams

El manifest (`manifest.json`) es un archivo JSON que describe tu app ante Teams. Se empaqueta en un ZIP junto con los íconos y se sube a Teams.

### Estructura del ZIP

```
af-webchat-teams.zip
├── manifest.json     ← Configuración de la app
├── color.png         ← Ícono 192x192 (color, cualquier fondo)
└── outline.png       ← Ícono 32x32 (blanco sobre transparente)
```

### Manifest completo explicado

```json
{
  // Schema y versión — usar v1.22+ para soporte de copilotAgents
  "$schema": "https://developer.microsoft.com/json-schemas/teams/v1.22/MicrosoftTeams.schema.json",
  "manifestVersion": "1.22",

  // Versión de TU app (MAJOR.MINOR.PATCH) — súbela en cada actualización
  "version": "1.0.2",

  // ID único — debe coincidir con tu App Registration en Entra ID
  "id": "<<AAD_APP_CLIENT_ID>>",

  // Info del desarrollador
  "developer": {
    "name": "AF-WebChat",
    "websiteUrl": "https://<<BOT_DOMAIN>>",
    "privacyUrl": "https://<<BOT_DOMAIN>>/privacy",
    "termsOfUseUrl": "https://<<BOT_DOMAIN>>/termsofuse"
  },

  // Nombre visible en Teams
  "name": {
    "short": "AF-WebChat",
    "full": "AF-WebChat Multi-Agent Assistant"
  },

  // Descripción (aparece en el store)
  "description": {
    "short": "Multi-agent AI assistant powered by Microsoft Agent Framework",
    "full": "AF-WebChat is a multi-agent AI assistant..."
  },

  // Íconos
  "icons": { "color": "color.png", "outline": "outline.png" },
  "accentColor": "#C9A227",

  // Identificación de la web app (mismo ID que la App Registration)
  "webApplicationInfo": {
    "id": "<<AAD_APP_CLIENT_ID>>",
    "resource": "api://example.com"
  },

  // Configuración del bot
  "bots": [{
    "botId": "<<AAD_APP_CLIENT_ID>>",
    "scopes": ["personal", "team", "groupChat", "copilot"],
    "supportsFiles": true,
    "isNotificationOnly": false,
    "commandLists": [{
      "scopes": ["personal"],
      "commands": [
        { "title": "agents", "description": "List all available AI agents" },
        { "title": "agent",  "description": "Switch to a specific agent" },
        { "title": "clear",  "description": "Clear chat history" },
        { "title": "new",    "description": "Start a fresh conversation" },
        { "title": "help",   "description": "Show available commands" }
      ]
    }]
  }],

  // Custom Engine Agent — habilita el bot en M365 Copilot
  "copilotAgents": {
    "customEngineAgents": [{
      "id": "<<AAD_APP_CLIENT_ID>>",
      "type": "bot",
      "disclaimer": {
        "text": "This agent uses AI. Please verify important information."
      }
    }]
  },

  "permissions": ["identity", "messageTeamMembers"],
  "validDomains": ["*.ngrok-free.app", "*.devtunnels.ms"]
}
```

### Scopes del bot

| Scope | Dónde aparece el bot | Descripción |
|---|---|---|
| `personal` | Chat 1:1 con el usuario | Chat privado directo con el bot |
| `team` | Canal de un equipo | El bot se @menciona en canales |
| `groupChat` | Chat grupal (sin canal) | El bot participa en chats grupales |
| `copilot` | M365 Copilot Chat | Aparece en el panel de Agentes de Copilot |

### Aplicación personalizada vs Agente

| | Sin `copilotAgents` | Con `copilotAgents` |
|---|---|---|
| Cómo aparece | "Aplicación personalizada" | "Agente y aplicación personal" |
| Dónde se ve | Solo en Teams Apps | Teams Apps + panel de Agentes Copilot |
| Manifest mínimo | v1.19 | v1.21+ |
| Licencia | Microsoft 365 con Teams | M365 Copilot Chat (gratis) |

---

## 9. M365 Copilot — Custom Engine Agent

Un **Custom Engine Agent** es un bot que usa su propio modelo de AI (en nuestro caso Azure OpenAI con Semantic Kernel) y se publica en M365 Copilot.

### Requisitos del manifest

1. Schema v1.21 o superior
2. Scope `"copilot"` en `bots[].scopes`
3. Sección `copilotAgents.customEngineAgents` con el mismo `botId`

### Diferencias de comportamiento por canal

| Funcionalidad | Teams | M365 Copilot |
|---|---|---|
| Adaptive Cards | ✅ Renderiza | ❌ Ignora (usar texto) |
| Notificaciones proactivas | ✅ Funciona | ❌ No soportado |
| Streaming | Typing indicator | No disponible |
| Chat grupal | ✅ Sí | ❌ Solo personal |
| Historial persistente | ✅ Por conversación | Sin persistencia |

### Licencia del usuario

| Tipo de usuario | ¿Puede usar el agente? |
|---|---|
| Con M365 Copilot add-on license | ✅ Sin cargos adicionales |
| Con M365 Copilot Chat (gratis) | ✅ Funciona (billing por uso si accede datos tenant) |
| Sin M365 | ❌ No tiene acceso a Copilot |

**Documentación:** [Custom Engine Agents](https://learn.microsoft.com/en-us/microsoft-365-copilot/extensibility/overview-custom-engine-agent) · [Costos y licencias](https://learn.microsoft.com/en-us/microsoft-365-copilot/extensibility/cost-considerations)

---

## 10. Mensajes Proactivos

Los mensajes proactivos permiten al bot enviar mensajes **sin que el usuario escriba primero**. Solo funcionan en Teams.

### Cómo funciona

1. El usuario interactúa con el bot → se guarda `ConversationReference`
2. Un trigger externo (API, timer, evento) quiere notificar al usuario
3. Se usa `ContinueConversationAsync()` con la referencia guardada
4. El mensaje llega al chat del usuario en Teams

### ConversationReferenceStore

```csharp
// Se guarda automáticamente en cada OnMessageAsync:
_conversationStore.AddOrUpdate(turnContext.Activity);

// Luego, para enviar proactivamente:
adapter.ContinueConversationAsync(clientId, reference, async (ctx, ct) =>
{
    await ctx.SendActivityAsync(notification, ct);
}, cancellationToken);
```

### API de notificaciones

```bash
# Ver conversaciones conectadas
GET /api/proactive/conversations

# Enviar notificación dirigida
POST /api/proactive/notify
{ "conversationKey": "19:abc...", "title": "Deploy", "message": "v2.1 live", "severity": "success" }

# Broadcast a todos
POST /api/proactive/broadcast
{ "message": "Mantenimiento a las 10pm" }
```

---

## 11. Configuración Paso a Paso

### Prerequisitos

- App AF-WebChat funcionando con agentes
- Cuenta de Azure con permisos para crear recursos
- Acceso a Entra ID (App Registrations)
- Teams con permiso de sideload habilitado

### Paso 1: App Registration en Entra ID

1. Azure Portal → **Entra ID** → **App Registrations** → **New Registration**
2. Nombre: `AF-WebChat-Bot`
3. Tipo: **Single Tenant**
4. Anotar: **Application (client) ID** y **Directory (tenant) ID**
5. Ir a **Certificates & secrets** → **New client secret** → copiar el **Value**

### Paso 2: Azure Bot Resource

1. Azure Portal → **Create resource** → buscar **Azure Bot**
2. Handle: `af-webchat-bot`
3. Tipo de app: **Single Tenant**
4. App ID: el Client ID del paso 1
5. Una vez creado → **Settings** → **Channels** → agregar **Microsoft Teams**
6. **Configuration** → **Messaging endpoint**: `https://{tu-url}/api/messages`

### Paso 3: Configurar `appsettings.json`

Agregar las secciones `TokenValidation`, `Connections` y `ConnectionsMap` en `appsettings.Development.json` con los valores reales de `ClientId`, `TenantId` y `ClientSecret`.

### Paso 4: Crear manifest y ZIP

1. Editar `appManifest/manifest.json` con tu Client ID
2. Crear ZIP:

```powershell
cd appManifest
Compress-Archive -Path manifest.json, color.png, outline.png -DestinationPath ../af-webchat-teams.zip
```

### Paso 5: Publicar en Teams

- **Desarrollo**: Teams → Apps → Manage your apps → Upload a custom app
- **Producción**: M365 Admin Center → Teams apps → Upload

### Paso 6: Dev Tunnel (para desarrollo local)

```bash
devtunnel host -p 5001 --allow-anonymous
```

Copiar la URL del tunnel y configurarla como Messaging Endpoint en Azure Bot.

---

## 12. Referencia de Archivos

| Archivo | Responsabilidad |
|---|---|
| `Bot/TeamsBotAgent.cs` | Clase principal — bridge entre Teams y orquestación |
| `Bot/AdaptiveCardBuilder.cs` | Construye todas las Adaptive Cards |
| `Bot/ConversationReferenceStore.cs` | Almacena referencias para proactive messaging |
| `Bot/AspNetAuthExtensions.cs` | Configuración de JWT validation |
| `Controllers/ProactiveController.cs` | API para enviar notificaciones push |
| `appManifest/manifest.json` | Manifest de la app de Teams |
| `appManifest/color.png` | Ícono a color 192x192 |
| `appManifest/outline.png` | Ícono outline 32x32 |
| `Program.cs` | Registro de servicios del bot (líneas 77-83, 182-183) |
| `appsettings.json` | Configuración de auth, connections, y tunnel |

---

## Documentación Oficial

| Tema | Link |
|---|---|
| Custom Engine Agents | [learn.microsoft.com](https://learn.microsoft.com/en-us/microsoft-365-copilot/extensibility/overview-custom-engine-agent) |
| Manifest Schema | [learn.microsoft.com](https://learn.microsoft.com/en-us/microsoft-365/extensibility/schema/root-copilot-agents) |
| Proactive Messages | [learn.microsoft.com](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/how-to/conversations/send-proactive-messages) |
| Agents SDK | [learn.microsoft.com](https://learn.microsoft.com/en-us/microsoft-365-copilot/extensibility/create-deploy-agents-sdk) |
| Costos y Licencias | [learn.microsoft.com](https://learn.microsoft.com/en-us/microsoft-365-copilot/extensibility/cost-considerations) |
| Prerequisites | [learn.microsoft.com](https://learn.microsoft.com/en-us/microsoft-365-copilot/extensibility/prerequisites) |
| Deploy a Azure | [learn.microsoft.com](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/deploy-azure-bot-service-manually) |
