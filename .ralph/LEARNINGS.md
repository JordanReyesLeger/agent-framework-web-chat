# LEARNINGS

Memoria acumulada del loop. El agente lee este archivo en **cada** iteración y le
agrega una línea cuando descubre algo que le ahorraría tiempo a la siguiente vuelta.

Reglas:
- Una línea por aprendizaje. Concreto y accionable.
- No repetir lo que ya está escrito.
- No usarlo como bitácora de trabajo (para eso está `LOG.md`).

Ejemplos del tipo de cosa que va aquí:
- `npm test` necesita que el contenedor de Postgres esté arriba: `docker compose up -d db`.
- Los DTOs viven en `src/contracts/`, no en `src/models/`, aunque el nombre engañe.
- El linter falla si un import de tipo no usa `import type`.

---
