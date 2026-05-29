# 🧪 Workshop: Microsoft Agent Framework — Python

> Workshop interactivo en **Jupyter notebooks** que cubre las funcionalidades principales del **Microsoft Agent Framework** para Python (`agent-framework`).
>
> Espejo del workshop C# que está en [`../01-AgentFrameworkTests/`](../01-AgentFrameworkTests/).

## 🚀 Inicio rápido

```powershell
# 1. Crear entorno virtual
python -m venv .venv
.\.venv\Scripts\Activate.ps1     # Windows PowerShell
# source .venv/bin/activate      # macOS / Linux

# 2. Instalar dependencias
pip install -r requirements.txt

# 3. Configurar Azure OpenAI — RECOMENDADO: .env
copy .env.example .env
# Edita .env con tu Endpoint, ApiKey y DeploymentName

# 4. Abrir el primer notebook en VS Code
code notebooks/00_basic_agent_creation.ipynb
```

Cuando VS Code te pregunte por el kernel del notebook, elige el intérprete del `.venv` que acabas de crear.

## 🔐 Configurar credenciales Azure OpenAI

El helper [`helpers/config.py`](./helpers/config.py) busca las credenciales en este orden de precedencia:

1. **`appsettings.Development.json`** (si existe, gana sobre `appsettings.json`)
2. **`appsettings.json`** (plantilla con placeholders vacíos)
3. **Variables de entorno** (incluye las cargadas desde `.env` automáticamente)

Puedes elegir cualquiera de las **3 opciones** según tu preferencia:

### ✅ Opción 1 — Archivo `.env` (RECOMENDADO para Python)

Es la forma más idiomática en Python y la que recomendamos:

```powershell
copy .env.example .env
# Edita .env y rellena tus valores
```

Contenido del `.env`:

```env
AZURE_OPENAI_ENDPOINT=https://tu-recurso.openai.azure.com/
AZURE_OPENAI_API_KEY=tu-api-key
AZURE_OPENAI_MODEL=gpt-4o
```

> ⚠️ **Nota:** El workshop usa la **v1 API** de Azure OpenAI (endpoint `/openai/v1/`).
> El helper `config.py` convierte automáticamente el endpoint al formato correcto
> y no requiere `api-version` explícito.

✔️ `.env` está en `.gitignore` (no se sube por accidente).
✔️ `python-dotenv` lo carga automáticamente cuando importas `helpers.config`.
✔️ Funciona igual en Windows, macOS y Linux.

### Opción 2 — `appsettings.Development.json` (paridad con el workshop C#)

Útil si vienes del [workshop C#](../01-AgentFrameworkTests/) y quieres la misma estructura:

```powershell
copy appsettings.json appsettings.Development.json
```

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://tu-recurso.openai.azure.com/",
    "ApiKey": "tu-api-key",
    "DeploymentName": "gpt-4o"
  }
}
```

✔️ `appsettings.Development.json` también está gitignored.
✔️ La misma sintaxis que usa .NET / ASP.NET.

### Opción 3 — Variables de entorno de la shell

Si prefieres no tener ningún archivo de secretos en disco, exporta las variables en tu shell:

```powershell
# PowerShell (sesión actual)
$env:AZURE_OPENAI_ENDPOINT = "https://tu-recurso.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY = "tu-api-key"
$env:AZURE_OPENAI_MODEL = "gpt-4o"

# Bash / Zsh
export AZURE_OPENAI_ENDPOINT="https://tu-recurso.openai.azure.com/"
export AZURE_OPENAI_API_KEY="tu-api-key"
export AZURE_OPENAI_MODEL="gpt-4o"
```

✔️ Recomendado para **CI/CD** y entornos cloud (App Service, Azure Container Apps, etc.) donde inyectas secretos vía variables de entorno gestionadas (Key Vault, secretos de pipeline).

### 🛡️ Buenas prácticas de seguridad

- ❌ **NUNCA** subas API keys a git — ya están en `.gitignore` pero verifica antes de cada commit.
- ✅ En **producción** prefiere **Managed Identity** + `AzureCliCredential` (sin API key). Mira el sample [`built_in_chat_clients.py`](https://github.com/microsoft/agent-framework/blob/main/python/samples/02-agents/chat_client/built_in_chat_clients.py) en el repo oficial para el patrón con credentials.
- ✅ Para equipos de varios desarrolladores, usa **Azure Key Vault** + variables de entorno inyectadas por el pipeline en lugar de archivos locales.
- ✅ Rota API keys periódicamente desde el portal Azure.

## 📚 Notebooks del workshop

Sigue el orden numérico — cada notebook explica conceptos nuevos basándose en los anteriores.

| # | Notebook | Tema |
|---|----------|------|
| 00 | [00_basic_agent_creation.ipynb](notebooks/00_basic_agent_creation.ipynb) | Creación básica de agentes |
| 01 | [01_running_agents.ipynb](notebooks/01_running_agents.ipynb) | Ejecución (completa / streaming / opciones) |
| 02 | [02_structured_output.ipynb](notebooks/02_structured_output.ipynb) | Salida estructurada con Pydantic |
| 03 | [03_function_tools.ipynb](notebooks/03_function_tools.ipynb) | Function tools |
| 04 | [04_tool_approval.ipynb](notebooks/04_tool_approval.ipynb) | Tool approval (Human-in-the-Loop) |
| 05 | [05_multimodal.ipynb](notebooks/05_multimodal.ipynb) | Multimodal (análisis de imágenes) |
| 06 | [06_conversations_sessions.ipynb](notebooks/06_conversations_sessions.ipynb) | Conversaciones y sesiones |
| 07 | [07_context_providers.ipynb](notebooks/07_context_providers.ipynb) | Context providers |
| 08 | [08_agent_pipeline_middleware.ipynb](notebooks/08_agent_pipeline_middleware.ipynb) | Pipeline y middleware |
| 09 | [09_orchestration_sequential.ipynb](notebooks/09_orchestration_sequential.ipynb) | 🆕 Orchestration: **Sequential** |
| 10 | [10_orchestration_concurrent.ipynb](notebooks/10_orchestration_concurrent.ipynb) | 🆕 Orchestration: **Concurrent** |
| 11 | [11_orchestration_handoff.ipynb](notebooks/11_orchestration_handoff.ipynb) | 🆕 Orchestration: **Handoff** |
| 12 | [12_orchestration_groupchat.ipynb](notebooks/12_orchestration_groupchat.ipynb) | 🆕 Orchestration: **Group Chat** |
| 13 | [13_workflows_executors.ipynb](notebooks/13_workflows_executors.ipynb) | Workflows: ejecutores (low-level) |
| 14 | [14_workflows_edges.ipynb](notebooks/14_workflows_edges.ipynb) | Workflows: edges condicionales |
| 15 | [15_workflows_events.ipynb](notebooks/15_workflows_events.ipynb) | Workflows: eventos y loops |
| 16 | [16_multiple_agents.ipynb](notebooks/16_multiple_agents.ipynb) | Múltiples agentes coordinados manualmente |
| 17 | [17_agents_in_workflows.ipynb](notebooks/17_agents_in_workflows.ipynb) | Agentes como nodos en `WorkflowBuilder` |
| 18 | [18_foundry_agents.ipynb](notebooks/18_foundry_agents.ipynb) | 🆕 Agentes con **Microsoft Foundry** |

> 💡 Los **Módulos 13-15** (workflows determinísticos) no necesitan Azure OpenAI — puedes probarlos sin credenciales.
> 💡 El **Módulo 18** (Foundry) requiere un proyecto de Microsoft Foundry configurado.

## 📋 Requisitos

- **Python 3.10+**
- **Azure OpenAI** con un deployment de `gpt-4o` (o compatible con tool-calling y visión multimodal)
- VS Code con la extensión **Jupyter** (para abrir los notebooks interactivamente)
- `python-dotenv` para carga automática de `.env`
- (Opcional) **Microsoft Foundry** para el módulo 18 (`pip install agent-framework-foundry`)

### ⚠️ Versiones importantes

| Paquete | Versión mínima | Notas |
|---------|---------------|-------|
| `agent-framework` | `>=1.7.0` | Usa la v1 Responses API de Azure OpenAI |
| `agent-framework-orchestrations` | `>=1.0.0rc2` | Requerido para módulos 09-12 (orquestaciones) |
| `agent-framework-foundry` | `>=1.0.0` | Solo para el módulo 18 (Foundry) |
| `openai` | `>=2.14.0` | Instalado automáticamente como dependencia |

## 🗂️ Estructura

```
01-AgentFrameworkTests-Python/
├── README.md                            # esta guía
├── WORKSHOP.md                          # tabla de equivalencias C# ↔ Python
├── requirements.txt
├── appsettings.json                     # plantilla (placeholders)
├── appsettings.Development.json         # tus credenciales (gitignored)
├── helpers/
│   └── config.py                        # create_chat_client() — carga Azure OpenAI desde appsettings
└── notebooks/                           # 19 notebooks del workshop ⭐
    ├── 00_basic_agent_creation.ipynb
    ├── 01_running_agents.ipynb
    └── ...
```
