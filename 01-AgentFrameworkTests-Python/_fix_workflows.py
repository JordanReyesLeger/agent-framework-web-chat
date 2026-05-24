"""Fixes #2:
- nb 13: set_state/get_state son sync, quitar `await`
- nb 14: restaurar las definiciones de SentimentResult/Executor/handlers en la celda 5
"""
from __future__ import annotations

import json
from pathlib import Path

NB = Path("notebooks")


def load(p): return json.loads(p.read_text(encoding="utf-8"))
def save(p, nb): p.write_text(json.dumps(nb, indent=1, ensure_ascii=False), encoding="utf-8")


# --- Fix nb 13: quitar await de set_state y get_state ---
p13 = NB / "13_workflows_executors.ipynb"
nb = load(p13)
for cell in nb["cells"]:
    if cell["cell_type"] != "code":
        continue
    src = "".join(cell["source"]) if isinstance(cell["source"], list) else cell["source"]
    new = src.replace("await ctx.set_state(", "ctx.set_state(").replace(
        "value = await ctx.get_state(", "value = ctx.get_state("
    )
    if new != src:
        cell["source"] = new.splitlines(keepends=True)
        print("✅ nb 13: await quitado de set_state/get_state")
save(p13, nb)


# --- Fix nb 14: cell 5 debe definir TODAS las clases antes del workflow positivo ---
p14 = NB / "14_workflows_edges.ipynb"
nb = load(p14)

NEW_CELL_5 = '''from dataclasses import dataclass


@dataclass
class SentimentResult:
    is_positive: bool
    text: str


_POSITIVE_WORDS = {"good", "great", "excellent", "happy", "love", "wonderful", "amazing"}


class SentimentAnalyzerExecutor(Executor):
    def __init__(self, id: str = "sentiment_analyzer"):
        super().__init__(id=id)

    @handler
    async def analyze(self, text: str, ctx: WorkflowContext[SentimentResult]) -> None:
        is_positive = any(w in text.lower() for w in _POSITIVE_WORDS)
        await ctx.send_message(SentimentResult(is_positive=is_positive, text=text))


@executor(id="positive_handler")
async def positive_handler(msg: SentimentResult, ctx: WorkflowContext[Never, str]) -> None:
    await ctx.yield_output(f"POSITIVE: {msg.text}")


@executor(id="negative_handler")
async def negative_handler(msg: SentimentResult, ctx: WorkflowContext[Never, str]) -> None:
    await ctx.yield_output(f"NEGATIVE: {msg.text}")


# --- Workflow positivo ---
analyzer = SentimentAnalyzerExecutor()

workflow_pos = (
    WorkflowBuilder(start_executor=analyzer)
    .add_edge(
        analyzer,
        positive_handler,
        condition=lambda msg: isinstance(msg, SentimentResult) and msg.is_positive,
    )
    .add_edge(
        analyzer,
        negative_handler,
        condition=lambda msg: isinstance(msg, SentimentResult) and not msg.is_positive,
    )
    .build()
)

events = await workflow_pos.run("This is a great day!")
print(f"✅ Input positivo  → {events.get_outputs()}")
'''

# Cell 5 is the one starting with "analyzer = SentimentAnalyzerExecutor()" — replace it
target_idx = None
for i, cell in enumerate(nb["cells"]):
    if cell["cell_type"] != "code":
        continue
    src = "".join(cell["source"]) if isinstance(cell["source"], list) else cell["source"]
    if "analyzer = SentimentAnalyzerExecutor()" in src and "workflow_pos" in src:
        target_idx = i
        break

if target_idx is not None:
    nb["cells"][target_idx]["source"] = NEW_CELL_5.splitlines(keepends=True)
    print(f"✅ nb 14: cell {target_idx} reconstruida con todas las definiciones")
else:
    print("⚠️ No se encontró la cell del workflow positivo en nb 14")

save(p14, nb)
print("\nDone.")
