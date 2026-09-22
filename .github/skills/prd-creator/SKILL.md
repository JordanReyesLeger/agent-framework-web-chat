---
name: prd-creator
description: Convierte requerimientos crudos (un correo, una minuta, un documento de negocio, una lista de bullets) en un PRD y una cola de tareas ejecutable para el Ralph Loop - .ralph/PRD.md, .ralph/tasks.json y un .ralph/tasks/TASK-XXXX.json por tarea - y completa los comandos de verificacion en .ralph/ralph.config.json detectando el stack del repositorio. Usalo cuando el usuario diga "convierte estos requerimientos en tareas", "genera el PRD", "prepara el plan del loop", "bajame esto a tareas" o pase un archivo de requerimientos.
---

# prd-creator

Tu salida alimenta un loop autónomo que va a correr toda la noche sin supervisión.
**Un plan flojo no produce un resultado flojo: produce una noche entera desperdiciada
y un repositorio que hay que revertir.** Trabaja en consecuencia.

## Paso 1 — Entiende el terreno antes de planear

Antes de escribir una sola tarea, averigua:

1. **Qué clase de entregable es.** No todos los proyectos compilan. Mira qué hay en el
   repositorio y clasifícalo — puede ser más de uno a la vez:

   | Señal en el repositorio | Entregable |
   |---|---|
   | `package.json`, `*.csproj`, `pyproject.toml`, `pom.xml`, `go.mod`, `Cargo.toml` | **código** |
   | `*.bicep`, `*.tf`, `azuredeploy.json`, `k8s/`, `helm/`, `azure.yaml` | **infraestructura** |
   | `docs/`, `*.md`, `mkdocs.yml`, `docusaurus.config.js` | **documentación** |
   | `*.drawio`, `*.mmd`, `diagrams/` | **diagramas** |
   | `*.sql`, `dbt_project.yml`, `*.ipynb` | **datos** |
   | Repositorio vacío | lo dice el usuario, no lo adivines |

   Esta clasificación decide cómo se verifica cada tarea (Paso 4). Si te equivocas aquí,
   el plan entero queda sin freno.
2. **Cómo se verifica hoy.** Los scripts de `package.json`, los targets del `Makefile`, los
   pasos del workflow de CI, los linters configurados. Ahí está la verdad, no en la costumbre.
3. **Qué ya existe.** Módulos, capas, convenciones de nombres, dónde viven las pruebas y los
   documentos. Las tareas deben encajar en lo que ya hay, no en una arquitectura imaginaria.
4. **`/docs`** y cualquier referencia que el usuario mencione.

Si el repositorio está vacío, la primera tarea de la cola tiene que dejar el terreno listo
para que las demás se puedan comprobar: en código, *"andamiaje: crear el proyecto, el proyecto
de pruebas y dejar la verificación en verde"*; en documentación o infraestructura, *"crear la
estructura de carpetas y el índice/esqueleto que las demás tareas van a llenar"*.

## Paso 2 — Escribe `.ralph/PRD.md`

Llena todas las secciones de la plantilla. Dos reglas duras:

- **Alcance fuera, explícito.** Lo que no escribas en "Fuera" el agente lo puede tocar a las 3am.
- **Sección 10, Preguntas abiertas.** Todo lo que el requerimiento no aclara va ahí.
  No inventes la respuesta. Una ambigüedad no resuelta se convierte en un `DECIDE`
  que detiene el loop.

## Paso 3 — Parte en tareas

Esta es la parte que decide si la noche sirve o no.

### Tamaño

Una tarea = **una iteración = un commit**. En concreto:

- Se puede terminar y verificar de forma independiente.
- Toca del orden de 1 a 5 archivos.
- **Trae su propia forma de comprobarse** (ver Paso 4). Si no sabes cómo comprobarla, no está lista.
- Se explica en un título de una línea sin la palabra "y".

Si el título necesita una "y", son dos tareas.

| Mal | Bien |
|---|---|
| "Implementar el módulo de facturación" | `Modelo Factura + migración`, `POST /facturas con validación`, `GET /facturas/{id}`, `Cálculo de impuestos`, `Pruebas de integración del flujo` |
| "Agregar autenticación" | `Middleware de validación de JWT`, `Endpoint de login`, `Refresh token`, `Proteger rutas /admin` |
| "Documentar la plataforma" | `Diagrama de topología de red`, `Tabla de costos mensuales`, `Runbook de failover`, `Matriz de RBAC` |
| "Montar la infra en Azure" | `VNet hub con subredes`, `Azure Firewall en el hub`, `App Gateway WAF en el spoke`, `Private Endpoint del App Service` |
| "Arreglar bugs" | Una tarea por bug, con el caso que lo reproduce |

### Orden y dependencias

- Ordena por dependencia real, no por importancia de negocio.
- Lo transversal va primero: andamiaje, configuración, modelo de datos, contratos.
- `dependsOn` solo con dependencias duras. Una dependencia falsa serializa el trabajo
  y desperdicia iteraciones.
- Las primeras 2 o 3 tareas deben ser **fáciles y verificables**: si la primera iteración
  falla, el loop se detiene antes de calentar motores.

### Formato

`.ralph/tasks.json`:

```json
{
  "version": 1,
  "project": "<nombre>",
  "generatedAt": "<ISO-8601>",
  "tasks": [
    {
      "id": "TASK-0001",
      "title": "Titulo de una linea, en imperativo, sin la palabra 'y'",
      "status": "pending",
      "priority": 1,
      "dependsOn": [],
      "spec": ".ralph/tasks/TASK-0001.json",
      "commit": "",
      "completedAt": "",
      "blockedReason": ""
    }
  ]
}
```

Y un `.ralph/tasks/TASK-XXXX.json` por tarea con: `id`, `title`, `why`, `scope.incluye`,
`scope.excluye`, `files`, `steps`, `acceptance`, **`verify`**, `estimate` (`S`/`M`/`L`), `notes`.

**`acceptance` es obligatorio y tiene que ser observable.** Cada criterio se escribe como
*dado X, cuando Y, entonces Z*.

## Paso 4 — Dale a cada tarea su freno: el bloque `verify`

Aquí es donde el plan deja de ser una lista de deseos. **El motor corre este bloque él mismo
antes de aceptar que la corrida terminó**; la palabra del agente no es evidencia. Un plan sin
`verify` no arranca.

Cada tarea lleva al menos **un chequeo objetivo**, de uno de estos dos tipos:

```jsonc
"verify": [
  // el comando debe salir con codigo 0
  { "type": "cmd",  "run": "npm test -- facturas", "cwd": "." },

  // el archivo debe existir y cumplir lo que se le pida
  { "type": "file", "path": "docs/red.md",
    "contains": ["## Topología", "## Costos"],   // fragmentos literales
    "matches":  "SLA de \\d+(\\.\\d+)?%",         // regex
    "minWords": 400,                              // piso de contenido
    "maxBytes": 200000 },

  // se revisa pero NO cuenta como freno: uselo solo para lo que de verdad no se automatiza
  { "type": "manual", "check": "El diagrama se entiende sin explicación verbal" }
]
```

Una cadena suelta es azúcar para `cmd`: `"verify": ["dotnet test --nologo"]`.

### Cómo se verifica cada tipo de entregable

| Entregable | Chequeo que sí sirve |
|---|---|
| **Código** | `cmd` con la prueba **acotada a la tarea** (`npm test -- facturas`, `pytest -q tests/test_facturas.py`, `dotnet test --filter Facturas`). Más rápido y más preciso que correr la suite entera. |
| **Bicep / ARM** | `cmd`: `az bicep build --file infra/main.bicep`. Para validar contra la suscripción real: `az deployment group what-if -g <rg> -f infra/main.bicep`. |
| **Terraform** | `cmd`: `terraform validate` y `terraform plan -detailed-exitcode` con `"cwd": "infra"`. |
| **Kubernetes / Helm** | `cmd`: `kubectl apply --dry-run=server -f k8s/`, `helm template . \| kubeconform -strict`. |
| **Documentación** | `file` con `contains` de los encabezados que el documento debe tener, `matches` para los datos duros que no pueden faltar (una cifra, un SLA, un nombre de recurso) y `minWords` como piso anti-relleno. Súmale `cmd` si hay linter: `markdownlint`, `vale`, `cspell`. |
| **Diagramas** | `file` sobre el fuente (`.drawio`, `.mmd`, `.py` de Diagrams) con `contains` de los nodos que deben aparecer, más `cmd` que lo renderice: si no renderiza, está roto. |
| **Datos / SQL** | `cmd` que corra la consulta y falle si no devuelve lo esperado; o `sqlfluff lint`. |
| **Configuración / YAML** | `cmd`: `yamllint`, `jsonschema`, `az deployment validate`. |
| **Presentaciones / reportes** | `file` sobre el fuente en texto + `cmd` que lo compile o exporte. |

### Reglas que no se negocian

- **Nada de comandos interactivos ni en modo *watch*.** Cuelgan la iteración hasta el timeout.
  Usa `--run`, `--ci`, `--watchAll=false`, `-NonInteractive`.
- **El comando tiene que fallar de verdad cuando algo está mal.** Un `cmd` que siempre sale 0
  es peor que no tener verificación: da falsa confianza. Pruébalo rompiendo algo a propósito.
- **`manual` no es un atajo.** Si todas las tareas son `manual`, el agente se está
  autocalificando y el loop no tiene freno. El motor lo rechaza.
- **No inventes un comando que el repositorio no tiene.** Si no hay linter configurado, no lo
  pongas. Revisa primero `package.json`, `*.csproj`, `Makefile`, `pyproject.toml` y el CI.
- **`contains` va sobre texto que el autor no puede evitar escribir** (un encabezado, el
  nombre de un recurso), no sobre frases que podría redactar de diez maneras.
- Para código, prefiere la prueba **acotada** a la suite completa: la suite global va en
  `ralph.config.json`, que se corre como red de seguridad, no como freno de la tarea.

## Paso 5 — Completa `.ralph/ralph.config.json` (solo si el proyecto compila)

`verify` aquí es la **red de seguridad global**, opcional. Si el proyecto no compila
(documentación, infraestructura, datos), déjalo vacío: el freno son los `verify` de las tareas.

Llena con los comandos **reales** del repositorio, no con los que suelen usarse:

```jsonc
// Node / TypeScript
"install": "npm ci", "build": "npm run build", "typecheck": "npx tsc --noEmit",
"lint": "npm run lint", "test": "npm test -- --run", "e2e": "npx playwright test"

// .NET
"build": "dotnet build -warnaserror", "test": "dotnet test --nologo"

// Python
"install": "uv sync", "lint": "ruff check .", "typecheck": "mypy .", "test": "pytest -q"

// Java
"build": "mvn -B -q compile", "test": "mvn -B test"

// Go
"build": "go build ./...", "lint": "golangci-lint run", "test": "go test ./..."

// Infraestructura
"build": "az bicep build --file infra/main.bicep", "lint": "terraform fmt -check -recursive"

// Documentacion
"lint": "npx markdownlint-cli2 \"docs/**/*.md\""
```

Reglas:
- Solo comandos **no interactivos**, que terminan solos y devuelven código de salida distinto
  de cero cuando fallan. Un comando en modo *watch* cuelga la iteración hasta el timeout.
- Deja vacíos los que el proyecto no tenga. No inventes un linter que no está configurado.
- Si el proyecto necesita servicios (base de datos, cola), ponlos en `install`
  (`docker compose up -d`) y documenta los puertos en `env`.
- Si vas a correr dos proyectos en paralelo, dales **puertos distintos** en `env`.

## Paso 6 — Reporta al humano

Termina con un resumen corto y honesto:

1. Cuántas tareas generaste y en cuántas iteraciones estimas que caben.
2. Los comandos de verificación que configuraste, y cuáles **probaste** que corren.
3. **Las preguntas abiertas**, en una lista numerada, cada una con el impacto de no responderla.
4. Qué tareas marcarías tú como riesgosas para una corrida sin supervisión.

Luego dile explícitamente:

> Revisa **cada tarea individualmente** antes de arrancar el loop. Es más barato corregir
> una spec ahora que revertir cinco commits mañana.
> Prueba primero con `./ralph.ps1 -Project <ruta> -Once`.
