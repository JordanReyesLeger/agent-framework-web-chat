"""Configuración compartida del workshop.

Carga las credenciales de Azure OpenAI desde:
1. `appsettings.Development.json` (si existe, ignorado por git)
2. `appsettings.json` (valores por defecto / placeholders)
3. Variables de entorno (sobrescriben los anteriores)

Soporta dos modos de autenticación (mismo patrón que el proyecto AF-WebChat):
- **API key**: si `AZURE_OPENAI_API_KEY` (o `AzureOpenAI.ApiKey`) está definido
- **AzureCliCredential**: si no hay API key, usa `az login` (recomendado para dev)

Expone una única función pública: `create_chat_client()` que devuelve un
`AzureOpenAIChatClient` listo para pasar al constructor de `Agent(...)`.
"""

from __future__ import annotations

import json
import os
from functools import lru_cache
from pathlib import Path
from typing import Any

try:
    from dotenv import load_dotenv

    load_dotenv()
except ImportError:  # pragma: no cover
    pass

from agent_framework.azure import AzureOpenAIChatClient

_BASE_DIR = Path(__file__).resolve().parent.parent


@lru_cache(maxsize=1)
def _load_settings() -> dict[str, Any]:
    """Carga `appsettings.json` y, si existe, lo sobreescribe con `appsettings.Development.json`."""
    settings: dict[str, Any] = {}

    base_file = _BASE_DIR / "appsettings.json"
    dev_file = _BASE_DIR / "appsettings.Development.json"

    if base_file.exists():
        settings.update(json.loads(base_file.read_text(encoding="utf-8")))

    if dev_file.exists():
        dev = json.loads(dev_file.read_text(encoding="utf-8"))
        # Merge superficial por sección
        for k, v in dev.items():
            if isinstance(v, dict) and isinstance(settings.get(k), dict):
                settings[k].update(v)
            else:
                settings[k] = v

    return settings


def _get(key: str, env_var: str | None = None, default: str | None = None) -> str:
    """Devuelve `AzureOpenAI:<key>` de settings, o la variable de entorno, o default. Falla si no hay valor."""
    value = _get_optional(key, env_var, default)
    if not value:
        raise RuntimeError(
            f"Falta la configuración 'AzureOpenAI.{key}'. "
            f"Defínela en .env, appsettings.Development.json o en la variable de entorno {env_var}."
        )
    return value


def _get_optional(key: str, env_var: str | None = None, default: str | None = None) -> str | None:
    """Como `_get` pero retorna `None` en lugar de fallar si no hay valor."""
    settings = _load_settings().get("AzureOpenAI", {})
    return settings.get(key) or (os.getenv(env_var) if env_var else None) or default


# ---------- API pública ----------


def get_endpoint() -> str:
    return _get("Endpoint", "AZURE_OPENAI_ENDPOINT")


def get_api_key() -> str | None:
    """API key opcional. Si no está definida usamos AzureCliCredential."""
    return _get_optional("ApiKey", "AZURE_OPENAI_API_KEY")


def get_deployment_name() -> str:
    # Soporta tanto `DeploymentName` (workshop) como `ChatDeployment` (proyecto AF-WebChat)
    settings = _load_settings().get("AzureOpenAI", {})
    value = (
        settings.get("DeploymentName")
        or settings.get("ChatDeployment")
        or os.getenv("AZURE_OPENAI_MODEL")
        or os.getenv("AZURE_OPENAI_DEPLOYMENT")
    )
    if not value:
        raise RuntimeError(
            "Falta la configuración del deployment de Azure OpenAI. "
            "Defínela en .env (AZURE_OPENAI_MODEL) o en appsettings.Development.json."
        )
    return value


def get_api_version() -> str:
    return _get("ApiVersion", "AZURE_OPENAI_API_VERSION", default="2024-10-21")


def create_chat_client() -> AzureOpenAIChatClient:
    """Crea un `AzureOpenAIChatClient` apuntando a tu recurso Azure OpenAI.

    Auth automática:
    - Si hay `AZURE_OPENAI_API_KEY` (o `AzureOpenAI.ApiKey` en settings) → usa API key.
    - Si no → usa `AzureCliCredential` (requiere `az login` previamente).

    Úsalo así desde un notebook::

        from agent_framework import Agent
        from helpers.config import create_chat_client

        agent = Agent(
            client=create_chat_client(),
            instructions="...",
            name="...",
        )
    """
    api_key = get_api_key()

    if api_key:
        return AzureOpenAIChatClient(
            api_key=api_key,
            endpoint=get_endpoint(),
            deployment_name=get_deployment_name(),
            api_version=get_api_version(),
        )

    # Sin API key → AzureCliCredential (requiere `az login`)
    try:
        from azure.identity import AzureCliCredential
    except ImportError as e:  # pragma: no cover
        raise RuntimeError(
            "No hay AZURE_OPENAI_API_KEY definido y `azure-identity` no está instalado. "
            "Ejecuta: pip install azure-identity"
        ) from e

    return AzureOpenAIChatClient(
        credential=AzureCliCredential(),
        endpoint=get_endpoint(),
        deployment_name=get_deployment_name(),
        api_version=get_api_version(),
    )

