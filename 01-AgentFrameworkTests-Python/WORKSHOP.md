# 📖 Workshop: Microsoft Agent Framework — Python · Guía detallada

> Esta guía complementa los [18 notebooks](./notebooks/) con un índice rápido de qué se aprende en cada uno y una tabla de equivalencias entre C# y Python.

## 🎓 Plan de aprendizaje

Recorre los notebooks en orden. Cada uno presenta conceptos nuevos basándose en los anteriores y termina con un resumen + link al siguiente.

| # | Notebook | Conceptos principales | ¿Necesita Azure OpenAI? |
|---|----------|------------------------|--------------------------|
| 00 | [00_basic_agent_creation](./notebooks/00_basic_agent_creation.ipynb) | `Agent(client, instructions, name, description)`, `await agent.run(...)` | ✅ |
| 01 | [01_running_agents](./notebooks/01_running_agents.ipynb) | Streaming con `stream=True`, `options={"temperature":..., "max_tokens":...}` | ✅ |
| 02 | [02_structured_output](./notebooks/02_structured_output.ipynb) | Pydantic `BaseModel` + `default_options={"response_format": MyModel}` | ✅ |
| 03 | [03_function_tools](./notebooks/03_function_tools.ipynb) | `@tool` + `Annotated[type, Field(description=...)]`, plugins multi-tool | ✅ |
| 04 | [04_tool_approval](./notebooks/04_tool_approval.ipynb) | `@tool(approval_mode="always_require")`, `result.user_input_requests` | ✅ |
| 05 | [05_multimodal](./notebooks/05_multimodal.ipynb) | `Message` con `Content.from_text` + `Content.from_uri` (URL o base64) | ✅ |
| 06 | [06_conversations_sessions](./notebooks/06_conversations_sessions.ipynb) | `agent.create_session()`, `session.state` como dict de estado | ✅ |
| 07 | [07_context_providers](./notebooks/07_context_providers.ipynb) | Subclase de `ContextProvider`, `before_run`, `context.extend_instructions` | ✅ |
| 08 | [08_agent_pipeline_middleware](./notebooks/08_agent_pipeline_middleware.ipynb) | Middleware función `async def mw(ctx, call_next)`, patrón cebolla | ✅ |
| **09** | [**09_orchestration_sequential**](./notebooks/09_orchestration_sequential.ipynb) | 🆕 `SequentialBuilder(participants=[...])`, `chain_only_agent_responses`, `intermediate_output_from` | ✅ |
| **10** | [**10_orchestration_concurrent**](./notebooks/10_orchestration_concurrent.ipynb) | 🆕 `ConcurrentBuilder(participants=[...])`, `.with_aggregator(...)`, fan-out paralelo | ✅ |
| **11** | [**11_orchestration_handoff**](./notebooks/11_orchestration_handoff.ipynb) | 🆕 `HandoffBuilder`, `.with_start_agent`, `.add_handoff`, `HandoffAgentUserRequest` | ✅ |
| **12** | [**12_orchestration_groupchat**](./notebooks/12_orchestration_groupchat.ipynb) | 🆕 `GroupChatBuilder`, `selection_func`, `orchestrator_agent`, round-robin | ✅ |
| 13 | [13_workflows_executors](./notebooks/13_workflows_executors.ipynb) | `Executor` + `@handler`, `@executor`, `WorkflowBuilder`, `ctx.send_message` / `yield_output` | ❌ |
| 14 | [14_workflows_edges](./notebooks/14_workflows_edges.ipynb) | `add_edge(A, B, condition=lambda msg: ...)` | ❌ |
| 15 | [15_workflows_events](./notebooks/15_workflows_events.ipynb) | `workflow.run(input, stream=True)`, eventos, patrón loop convergente | ❌ |
| 16 | [16_multiple_agents](./notebooks/16_multiple_agents.ipynb) | Coordinación manual de varios agentes con `agent.run()` + `asyncio.gather` | ✅ |
| 17 | [17_agents_in_workflows](./notebooks/17_agents_in_workflows.ipynb) | Agentes como nodos en `WorkflowBuilder` (low-level) | ✅ |

> 💡 Los notebooks **13-15** son completamente determinísticos (no usan LLM) — útiles para entender workflows sin gastar tokens.
>
> 🎯 Los notebooks **09-12** son los patrones de **orchestrations** de alto nivel — el camino recomendado para coordinar varios agentes.

## 🔑 Equivalencias C# ↔ Python

Esta tabla te ayuda si vienes del [workshop C#](../01-AgentFrameworkTests/WORKSHOP.md) y quieres mapear los conceptos.

| Concepto | C# (`Microsoft.Agents.AI`) | Python (`agent-framework`) |
|----------|----------------------------|----------------------------|
| Cliente Azure OpenAI | `AzureOpenAIClient(...).GetChatClient(...)` + `.AsAIAgent()` | `OpenAIChatClient(api_key=..., azure_endpoint=..., model=...)` |
| Agente | `AIAgent` (`.AsAIAgent()` / `ChatClientAgentOptions`) | `Agent(client=..., instructions=..., tools=..., context_providers=..., middleware=...)` |
| Ejecución | `agent.RunAsync(prompt, session)` | `await agent.run(prompt, session=session)` |
| Streaming | `await foreach (var update in agent.RunStreamingAsync(...))` | `async for update in agent.run(..., stream=True)` |
| Salida estructurada | `agent.RunAsync<T>(prompt, session)` → `response.Result` | `default_options={"response_format": MyModel}` + `MyModel.model_validate_json(response.text)` |
| Function tools | `AIFunctionFactory.Create(method)` + `[Description]` | `@tool` + `Annotated[type, Field(description=...)]` |
| Aprobación de tool | `ApprovalRequiredAIFunction` | `@tool(approval_mode="always_require")` + bucle de `user_input_requests` |
| Sesión | `agent.CreateSessionAsync()` + `session.StateBag.SetValue/GetValue` | `agent.create_session()` + `session.state[key]` |
| Multimodal | `ChatMessage(ChatRole.User, [TextContent(...), UriContent(uri, "image/png")])` | `Message(role="user", contents=[Content.from_text(...), Content.from_uri(uri, "image/png")])` |
| Context provider | Subclase de `AIContextProvider` + `AIContextProviders` en `ChatClientAgentOptions` | Subclase de `ContextProvider` + `context_providers=[...]` en `Agent(...)` |
| Middleware | `agent.AsBuilder().Use((msgs, session, options, next, ct) => ...)` | `middleware=[async def mw(ctx, call_next): ...]` en `Agent(...)` |
| Workflow builder | `WorkflowBuilder + Executor<TIn, TOut> + AddEdge` | `WorkflowBuilder + Executor + @handler + add_edge` |
| `[YieldsOutput]` | Atributo .NET | `WorkflowContext[Never, T_W_Out]` + `ctx.yield_output(...)` |
| Edge condicional | `AddEdge<T>(from, to, condition: ...)` | `add_edge(from, to, condition=lambda msg: ...)` |
| Orquestación secuencial | `AgentWorkflowBuilder.BuildSequential(a, b)` | `SequentialBuilder(participants=[a, b]).build()` |
| Orquestación concurrente | `AgentWorkflowBuilder.BuildConcurrent([a, b])` | `ConcurrentBuilder(participants=[a, b]).build()` |
| Group chat | `AgentWorkflowBuilder.CreateGroupChatBuilderWith(...)` | `GroupChatBuilder(participants=[...], termination_condition=..., selection_func=...)` |
| Handoff (delegación dinámica) | — *(usar workflow manual)* | `HandoffBuilder(name, participants).with_start_agent(...).add_handoff(from, [to]).build()` |
| Handoff (modo autónomo) | — | `.with_autonomous_mode(turn_limits={agent_name: N})` |
| Handoff (input usuario) | — | `HandoffAgentUserRequest.create_response("...")` |

## 📦 Paquetes pip utilizados

| Paquete | Para qué sirve |
|---------|----------------|
| `agent-framework` | Microsoft Agent Framework (core, OpenAI/Azure OpenAI, workflows, orchestrations) |
| `pydantic` | Modelos tipados para salida estructurada (Módulo 02) |
| `azure-identity` | (Opcional) `AzureCliCredential` para auth sin API key |
| `ipykernel` | Kernel Jupyter para ejecutar los notebooks en VS Code |
| `python-dotenv` | (Opcional) Carga de variables desde `.env` |

## ⚙️ Configuración

El helper [`helpers/config.py`](./helpers/config.py) carga las credenciales desde, en este orden de precedencia:

1. **`appsettings.Development.json`** (gitignored — pon aquí tus secretos reales)
2. **`appsettings.json`** (plantilla, valores vacíos por defecto)
3. **Variables de entorno**: `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY`, `AZURE_OPENAI_MODEL`, `AZURE_OPENAI_API_VERSION` (también soporta `.env`)

Expone una única función: `create_chat_client()`. Los notebooks construyen el agente de forma explícita
para que veas la API real del SDK:

```python
from agent_framework import Agent
from helpers.config import create_chat_client

agent = Agent(
    client=create_chat_client(),
    instructions="...",
    name="...",
    tools=[...],
    context_providers=[...],
    middleware=[...],
    default_options={...},
)
```

## ⚠️ Notas importantes

1. Los notebooks **00-08** y **12-13** requieren Azure OpenAI (llaman al modelo real).
2. Los notebooks **09-11** (workflows) son determinísticos y no consumen tokens.
3. **Pydantic** se usa para los modelos de salida estructurada — el framework convierte automáticamente el modelo a JSON Schema para el parámetro `response_format`.
4. La estructura mantiene paridad 1:1 con el [workshop C#](../01-AgentFrameworkTests/WORKSHOP.md) para facilitar la comparación entre ambas versiones.

## 📚 Referencias

- 🐍 [Samples oficiales del SDK Python](https://github.com/microsoft/agent-framework/tree/main/python/samples)
- 📘 [Repo principal microsoft/agent-framework](https://github.com/microsoft/agent-framework)
- 🔷 [Workshop equivalente en C#](../01-AgentFrameworkTests/WORKSHOP.md)
