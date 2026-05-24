"""Valida que TODOS los notebooks del workshop se ejecuten sin errores.

Estrategia:
1. Para cada `.ipynb` en `notebooks/`, lanza un kernel Python real
2. Ejecuta TODAS las celdas en orden con un timeout por celda
3. Reporta ✅/❌ por notebook y el error específico si falla

Uso:
    python validate_all_notebooks.py
    python validate_all_notebooks.py --only 05_multimodal
    python validate_all_notebooks.py --skip-llm   # solo módulos 13-15 (determinísticos)
"""

from __future__ import annotations

import argparse
import sys
import time
from pathlib import Path

import nbformat
from nbclient import NotebookClient
from nbclient.exceptions import CellExecutionError

NB_DIR = Path(__file__).parent / "notebooks"
# Timeout por celda (en segundos). Algunos notebooks hacen varias llamadas LLM.
CELL_TIMEOUT = 300

# Notebooks determinísticos (no requieren LLM)
DETERMINISTIC = {"13_workflows_executors", "14_workflows_edges", "15_workflows_events"}


def validate_notebook(nb_path: Path) -> tuple[bool, str, float]:
    """Ejecuta el notebook completo. Retorna (ok, error_message, elapsed_seconds)."""
    t0 = time.perf_counter()
    try:
        nb = nbformat.read(nb_path, as_version=4)
        client = NotebookClient(
            nb,
            timeout=CELL_TIMEOUT,
            kernel_name="python3",
            resources={"metadata": {"path": str(NB_DIR)}},
            allow_errors=False,
            skip_cells_with_tag="skip-validation",
        )
        client.execute()
        elapsed = time.perf_counter() - t0
        return True, "", elapsed
    except CellExecutionError as e:
        elapsed = time.perf_counter() - t0
        # Extract first 600 chars of the traceback for readability
        msg = str(e)
        if len(msg) > 600:
            msg = msg[:600] + "...[truncated]"
        return False, msg, elapsed
    except Exception as e:
        elapsed = time.perf_counter() - t0
        return False, f"{type(e).__name__}: {e}", elapsed


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", help="Solo ejecutar este notebook (sin .ipynb)")
    parser.add_argument(
        "--skip-llm", action="store_true",
        help="Skip notebooks que requieren LLM (solo correr los determinísticos)",
    )
    args = parser.parse_args()

    notebooks = sorted(NB_DIR.glob("*.ipynb"))
    if args.only:
        notebooks = [n for n in notebooks if args.only in n.stem]
        if not notebooks:
            print(f"❌ No se encontró notebook con '{args.only}'")
            return 1
    elif args.skip_llm:
        notebooks = [n for n in notebooks if n.stem in DETERMINISTIC]

    print(f"\n🧪 Validando {len(notebooks)} notebook(s) — timeout por celda: {CELL_TIMEOUT}s\n")
    print("=" * 80)

    results: list[tuple[str, bool, str, float]] = []
    for nb_path in notebooks:
        name = nb_path.stem
        marker = "🤖" if name in DETERMINISTIC else "🌐"
        print(f"\n{marker} {name}...", end=" ", flush=True)
        ok, err, elapsed = validate_notebook(nb_path)
        if ok:
            print(f"✅ ({elapsed:.1f}s)")
        else:
            print(f"❌ ({elapsed:.1f}s)")
            # Print just a few lines of the error
            for line in err.split("\n")[:8]:
                print(f"     {line}")
        results.append((name, ok, err, elapsed))

    # Resumen final
    print("\n" + "=" * 80)
    passed = sum(1 for _, ok, _, _ in results if ok)
    failed = len(results) - passed
    total_time = sum(t for _, _, _, t in results)
    print(f"\n📊 RESULTADOS: {passed}/{len(results)} OK · {failed} fallaron · {total_time:.1f}s total\n")

    if failed:
        print("❌ Notebooks que fallaron:")
        for name, ok, _, _ in results:
            if not ok:
                print(f"   - {name}")
        return 1

    print("🎉 Todos los notebooks pasaron.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
