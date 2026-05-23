# Azure Speech Pricing — VoiceLive vs. Servicios Separados

> **Fuente oficial:** [Azure Speech pricing](https://azure.microsoft.com/en-us/pricing/details/speech/) · [Voice Live pricing docs](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/voice-live#pricing) · [Voice Live overview](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/voice-live)
> **Última actualización:** 22 de mayo, 2026 (doc oficial actualizada 01/29/2026)
> **Moneda:** USD · **Modalidad:** Pay-as-You-Go (sin commitment tier)
> **Pricing oficial vigente desde:** 1 de julio, 2025

---

## 📋 Tabla de contenidos

- [Resumen ejecutivo](#-resumen-ejecutivo)
- [Opción A — VoiceLive (todo-en-uno)](#-opción-a--voicelive-todo-en-uno)
  - [BYOM — Costo detallado, PTU y ejemplo de cálculo](#-costo-real-con-byom--análisis-detallado-y-ejemplo-de-cálculo)
- [Opción B — Servicios separados](#-opción-b--servicios-separados)
- [Comparación práctica](#-comparación-práctica--conversación-de-5-minutos)
- [¿Qué son los tipos de tokens de texto?](#-qué-son-los-tipos-de-tokens-de-texto-text-input-fresh--cached--output)
- [Matriz de decisión](#-matriz-de-decisión)
- [Free Tier (F0)](#-free-tier-f0)
- [Commitment Tiers](#-commitment-tiers-volumen-alto)
- [Notas importantes](#️-notas-importantes)

---

## 🎯 Resumen ejecutivo

Azure ofrece dos caminos para construir un agente de voz conversacional:

| | **VoiceLive** | **Servicios separados** |
|---|---|---|
| **Modelo de precios** | Múltiples líneas por sesión: **Text** + **Audio input** + **Audio output** + extras | Por hora (STT) + por carácter (TTS) + por token (LLM) |
| **Latencia** | Ultra-baja (~300ms RTT) | Mayor (~800-1500ms pipeline) |
| **Barge-in / VAD / Echo cancel** | Nativo, server-side | Tienes que implementarlo |
| **Modelos LLM** | Catálogo fijo por tier (o BYO) | Cualquier modelo de Azure OpenAI |
| **Costo (conversación 5 min, 5 turnos, Pro native S2S)** | **~$0.37** | **~$0.10** |
| **Avatar visual** | Add-on (vía TTS Avatar) | TTS Avatar standalone |
| **SDK** | `Azure.AI.VoiceLive` (un solo WebSocket) | Speech SDK + Azure OpenAI SDK |

---

## 🎙️ Opción A — VoiceLive (todo-en-uno)

VoiceLive empaqueta **STT + LLM + TTS + (opcional) Avatar** en un solo servicio WebSocket. Se factura **por millón de tokens**, separando texto vs. audio, y entre input / cached input / output.

### ⚠️ Aclaración importante sobre modelos

VoiceLive tiene **dos modos de operación** respecto a los modelos:

#### 1. **Managed mode** (modelos hospedados por Microsoft) — Pro / Basic / Lite
La doc oficial dice textualmente:
> *"All natively supported models are fully managed, so you don't need to deploy models, worry about capacity planning, or provision throughput."*

- ❌ No puedes usar tu deployment de Foundry con PTU.
- ❌ No aplica tu PTU.
- ✅ Solo eliges el **nombre del modelo** del catálogo soportado — Microsoft hace el resto.

#### 2. **BYOM mode** (Bring Your Own Model) — usa TU deployment
- ✅ **Usa TU deployment de Azure OpenAI / Foundry**, incluido **PTU**.
- ✅ Soporta modelos de partners: **Claude (Anthropic), Grok (xAI), Fireworks, model router**.
- ✅ Soporta **modelos fine-tuned** y **content safety personalizado**.
- 💰 Tarifa de voz aparte: **$12.50 / $23 por 1M tokens de audio Standard** (más barato que managed).
- 📖 Ver sección [Voice Live BYO (BYOM)](#voice-live-byo-bring-your-own-model--byom---disponible-ga--preview) más abajo.

### Tiers disponibles (nombres oficiales: Pro / Basic / Lite)

> 📌 La página de pricing usa `Standard`, pero la doc del producto usa `Basic`. **Son el mismo tier.** Aquí usamos los nombres de la doc oficial.

No eliges un tier — eliges un **modelo** y el tier correspondiente se aplica automáticamente.

| Tier | Modelos soportados |
|---|---|
| **Voice Live Pro** | `gpt-realtime`, `gpt-4o`, `gpt-4.1`, `gpt-5`, `gpt-5-chat` |
| **Voice Live Basic** (= Standard) | `gpt-realtime-mini`, `gpt-4o-mini`, `gpt-4.1-mini`, `gpt-5-mini` |
| **Voice Live Lite** | `gpt-5-nano`, `phi4-mm-realtime`, `phi4-mini` |

**¿Cómo funciona cada modelo?**
- `gpt-realtime` / `gpt-realtime-mini` → **audio nativo** (S2S, modelo entiende tono/emoción directamente).
- `gpt-4o`, `gpt-4.1`, `gpt-5`, etc. → **modo cascada** internamente: audio in vía Azure Speech STT → modelo de texto → audio out vía Azure TTS. Puedes elegir voz neural o custom voice.
- `phi4-mm-realtime` → audio nativo (Phi multimodal).
- `phi4-mini` → modo cascada con Phi pequeño.

### 🆚 Diferencias Pro vs Basic vs Lite

| Característica | **Voice Live Pro** | **Voice Live Basic** | **Voice Live Lite** |
|---|---|---|---|
| **Modelos LLM** | GPT-Realtime, GPT-4o, GPT-4.1, GPT-5, GPT-5-chat | GPT-Realtime-mini, GPT-4o-mini, GPT-4.1-mini, GPT-5-mini | GPT-5-nano, Phi4-mm-realtime, Phi4-mini |
| **Calidad de razonamiento** | 🟢 Máxima — modelos flagship | 🟡 Buena — modelos mid-tier | 🟠 Básica — SLMs / nano |
| **Conocimiento general** | Amplio, contextual, multilingüe profundo | Sólido, suficiente para mayoría de tareas | Limitado, mejor para tareas focalizadas |
| **Function calling / tools** | Excelente, complejo multi-tool | Bueno, single/few-tool | Limitado (Phi soporta básico) |
| **Latencia (audio nativo S2S)** | ~300-500ms | ~250-400ms (más rápido por modelo más pequeño) | ~200-350ms (el más rápido) |
| **Comprensión de matices (tono, emoción)** | Excelente | Buena | Limitada |
| **Idiomas soportados** | 140+ (audio in) / 600+ voces TTS | 140+ / 600+ | 140+ / 600+ (mismo pipeline Speech) |
| **Multimodal (visión)** | ✅ Sí (GPT-4o, GPT-5) | ✅ Sí (GPT-4o-mini, GPT-5-mini) | ⚠️ Solo Phi4-mm |
| **Custom voice / Custom STT** | ✅ Sí | ✅ Sí | ✅ Sí |
| **Avatar integration** | ✅ Sí | ✅ Sí | ✅ Sí |
| **Costo audio Standard (in/out por 1M tokens)** | $17 / $31 | $15 / $26 | $15 / $25 |
| **Costo audio nativo (in/out por 1M tokens)** | $32 / $64 | $11 / $22 | $4 / — |
| **Costo texto (in/out por 1M tokens)** | $4 / $16 | $0.66 / $2.64 | $0.11 / $0.44 |
| **Costo conversación 5 min, 5 turnos (escenario típico)²** | **~$0.24-$0.37** | **~$0.13-$0.18** | **~$0.03** (Phi native, sin custom voice) |
| **Cached input discount** | $0.40/1M (~98% off) | $0.33/1M (~98% off) | $0.04/1M (~99% off) |
| **Voz/personalidad (modo nativo)** | Más expresivo, mejor prosodia | Natural, fluida | Funcional, menos matiz |
| **Ideal para** | Asistentes premium, contact centers enterprise, agentes complejos con tools | Voicebots productivos, asistentes generales, MVPs en producción | Edge/IoT, asistentes en auto, dispositivos embebidos, alta concurrencia barata |
| **NO ideal para** | Alto volumen low-cost | Razonamiento profundo / multi-step complejo | Conversaciones largas con conocimiento general amplio |

#### 📊 Costo relativo (línea base = Lite)

```
Lite      ███ 1x (Phi native, sin custom voice)        ($0.03 / 5 min)
Basic     ████████████ 5x   ($0.13 / 5 min)
Pro       ████████████████████████████████ 12x  ($0.37 / 5 min, native S2S)
```

> ² **Cálculo validado contra el [Pricing Calculator oficial de Microsoft](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/voice-live#pricing)**. Considera acumulación de contexto por turno (audio fresh + cached) y system prompt 500 tokens, 5 min, 5 turnos. Ver [sección de cálculo](#-comparación-práctica--conversación-de-5-minutos) para detalle.

#### 🎯 Cómo elegir

- **Empieza con Basic** → es el sweet spot precio/calidad para casi cualquier voicebot productivo. Cambia a Pro solo si necesitas razonamiento de GPT-5/4o flagship o function-calling muy complejo.
- **Salta a Pro** cuando: el agente toma decisiones críticas, maneja tools complejas, requiere comprensión profunda de contexto largo, o representas marca premium.
- **Baja a Lite** cuando: el caso de uso es estrecho (ej. "asistente del auto solo responde comandos"), tienes muchísimo volumen y el costo importa, o despliegas en edge/dispositivos.

### Precios por 1M tokens (USD)

#### Voice Live Pro
| Modalidad | Input | Cached Input | Output |
|---|---:|---:|---:|
| Texto | $4.00 | $0.40 | $16.00 |
| Audio - Standard | $17.00 | $0.40 | $31.00 |
| Audio - Custom¹ | $40.00 | $0.40 | $55.00 |
| Native Audio (S2S real-time) | $32.00 | $0.40 | $64.00 |

#### Voice Live Standard
| Modalidad | Input | Cached Input | Output |
|---|---:|---:|---:|
| Texto | $0.66 | $0.33 | $2.64 |
| Audio - Standard | $15.00 | $0.33 | $26.00 |
| Audio - Custom¹ | $39.00 | $0.33 | $50.00 |
| **Native Audio (S2S real-time)** | **$11.00** | **$0.33** | **$22.00** |

> 💡 **Esta app** (`gpt-4o-mini-realtime-preview`) usa **Voice Live Standard, Native Audio**.

#### Voice Live Lite
| Modalidad | Input | Cached Input | Output |
|---|---:|---:|---:|
| Texto | $0.11 | $0.04 | $0.44 |
| Audio - Standard | $15.00 | $0.04 | $25.00 |
| Audio - Custom¹ | — | $0.04 | $50.00 |
| Native Audio (S2S real-time) | $4.00 | $0.04 | — |

#### Voice Live BYO (Bring Your Own Model — BYOM) — ✅ **Disponible (GA + Preview)**

| Modalidad | Input | Output |
|---|---:|---:|
| Audio - Standard | $12.50 | $23.00 |
| Audio - Custom¹ | $36.00 | $47.00 |

> ¹ **Custom** = se cobra aparte el training y hosting del custom STT/voice. Ver tabla de servicios separados.

**📚 Fuente oficial:** [Bring Your Own Model (BYOM) with Voice Live API](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/how-to-bring-your-own-model) (actualizado 15/mayo/2026)

### ✅ Modos BYOM disponibles

VoiceLive BYOM tiene **3 modos** según el tipo de modelo que traigas:

| Modo (`profile`) | Modelos soportados | Estado |
|---|---|---|
| `byom-azure-openai-realtime` | `gpt-realtime`, `gpt-realtime-mini` (tu deployment de Foundry/Azure OpenAI) | ✅ GA |
| `byom-azure-openai-chat-completion` | `gpt-5.4`, `gpt-5.3-chat`, **`grok-4`**, y cualquier modelo de chat completion de Azure OpenAI o Foundry (incluido modelos fine-tuned y model router) | ✅ GA |
| `byom-foundry-anthropic-messages` | **Claude Sonnet 4.6, Claude Haiku 4.5** vía Foundry Messages API | ⚠️ Preview |

### 🎯 Casos de uso reales que habilita BYOM

1. **Modelos fine-tuned** propios de Azure OpenAI o Foundry.
2. **Modelos de partners** del catálogo de Foundry: **Anthropic Claude, xAI Grok, Fireworks** (custom weights), **model router**.
3. **PTU (Provisioned Throughput)** — ✅ **SÍ soporta tus deployments con PTU comprado**. Doc oficial textual: *"Use your PTU (Provisioned Throughput Units) deployments for consistent performance"*.
4. **Content safety personalizado** con tu propia configuración de filtros.

### 🔌 Cómo se conecta (endpoint)

```text
wss://<tu-foundry-resource>.cognitiveservices.azure.com/voice-live/realtime
    ?api-version=2025-10-01
    &profile=byom-azure-openai-chat-completion
    &model=<nombre-de-tu-deployment>
```

- `<tu-foundry-resource>` → nombre del recurso Foundry/Cognitive Services que hospeda Voice Live.
- `<nombre-de-tu-deployment>` → nombre del deployment (como aparece en el portal de Foundry).
- Si el modelo está en **otro Foundry resource diferente**, añade `&foundry-resource-override=<otro-recurso>` y configura **cross-resource authentication**.

### 🔐 Autenticación

Dos opciones:
- **API Key** del recurso Voice Live, o
- **Microsoft Entra ID** (recomendado): el recurso Foundry necesita una **system-assigned managed identity** con el role **Foundry User** (`53ca6127-db72-4b80-b1b0-d745d6d5456d`) sobre el recurso del modelo.

```powershell
# 1. Habilita managed identity en el recurso Foundry de Voice Live
az cognitiveservices account identity assign `
  --name <foundry-voice-live> `
  --resource-group <rg-voice-live>

# 2. Obtén el principal ID
$pid = az cognitiveservices account show `
  --name <foundry-voice-live> `
  --resource-group <rg-voice-live> `
  --query "identity.principalId" -o tsv

# 3. Asigna "Foundry User" role sobre el recurso del modelo
az role assignment create `
  --assignee-object-id $pid `
  --role "53ca6127-db72-4b80-b1b0-d745d6d5456d" `
  --scope "/subscriptions/<sub>/resourceGroups/<rg-model>/providers/Microsoft.CognitiveServices/accounts/<foundry-model>"
```

### 💰 Costo real con BYOM — Análisis detallado y ejemplo de cálculo

#### ¿Cómo se reparten los costos en BYOM?

Con BYOM, el costo de una sesión se divide en **dos partes independientes**:

| Componente | ¿Quién cobra? | ¿Qué pagas? |
|---|---|---|
| **Audio processing** (STT + TTS) | **Voice Live** | Tarifa BYO: $12.50 in / $23 out por 1M tok (Audio Standard) |
| **LLM inference** (razonamiento, respuestas) | **Tu deployment** | PTU (fijo mensual) o PAYG (por token) |

> 📌 **Clave:** Voice Live BYO **NO cobra tokens de texto** — solo el procesamiento de audio. Todo el cobro de texto (system prompt, contexto, respuestas) va por tu deployment.

> 📌 **Sin cached input en BYO:** A diferencia de los tiers managed (Pro/Basic/Lite) que cobran audio acumulado por turno con descuento de cached input, **BYOM solo cobra el audio real** (la voz del usuario y la voz sintetizada del asistente). El manejo de contexto acumulado lo hace tu deployment — por eso la tabla de precios BYO no tiene columna "Cached Input".

#### Tipos de deployment y costo del LLM

| Tipo | Cómo pagas el LLM | Costo marginal por sesión | Ideal para |
|---|---|---|---|
| **PTU** (Provisioned Throughput Units) | Fijo mensual por PTU reservado | **$0** (ya pagaste la reserva) | Alto volumen, latencia predecible |
| **PAYG** (Pay-As-You-Go) | Por token procesado | Variable según modelo | Volumen bajo/medio, experimentación |

#### 📐 Qué es PTU y cómo funciona

PTU te da **capacidad reservada** con latencia garantizada y predecible — crítico para agentes de voz donde la latencia impacta directamente la experiencia del usuario.

| Aspecto | Detalle |
|---|---|
| **Precio** | Varía por modelo, región y tipo de reserva. Consulta [Azure OpenAI pricing](https://azure.microsoft.com/en-us/pricing/details/cognitive-services/openai-service/) |
| **Tipos de SKU** | `ProvisionedManaged` (regional), `DataZoneProvisionedManaged` (US/EU), `GlobalProvisionedManaged` (global, mayor throughput) |
| **Reservas compartidas** | Las PTU son **compartidas entre modelos** del mismo pool — puedes usar GPT-4o, GPT-4.1, DeepSeek, Llama del mismo pool de PTU |
| **Overage** | Si excedes la capacidad reservada, el excedente se cobra a tarifa PAYG del modelo |
| **Compromisos** | 1 mes, 6 meses o 12 meses (mayor descuento a mayor plazo) |
| **Cross-resource** | Con el parámetro `foundry-resource-override` puedes apuntar a un deployment en otro recurso Foundry |

> 💡 **¿Cuándo conviene PTU?** Cuando tienes **alto volumen constante** (ej. contact center 24/7, miles de sesiones diarias) y necesitas **latencia predecible**. Para volumen bajo o variable, PAYG suele ser más económico.

#### 🧮 Ejemplo de cálculo: BYOM con PTU vs PAYG vs Managed

**Escenario:** Misma conversación de referencia — **5 min, 5 turnos** (30s user + 30s assistant), system prompt 500 tokens.

##### Paso 1: Tokens de audio (Voice Live BYO)

En BYOM, Voice Live solo procesa el **audio real** (sin acumulación de contexto por turno):

| Concepto | Cálculo | Tokens |
|---|---|---:|
| Audio input (voz del usuario) | 150 seg × 10 tok/seg | 1,500 |
| Audio output (voz sintetizada) | 150 seg × 20 tok/seg | 3,000 |

> ⚠️ **Diferencia clave vs managed:** En managed mode, VoiceLive cobra ~10,600 tokens de audio input (4,000 fresh + 6,600 cached) porque re-procesa el contexto acumulado cada turno. En **BYOM, solo cobra los 1,500 tokens de audio real** porque el contexto lo maneja tu deployment. Esto explica gran parte del ahorro.

##### Paso 2: Costo Voice Live (audio processing)

| Línea | Tokens | Tarifa BYO (/1M) | Costo |
|---|---:|---:|---:|
| Audio Standard input | 1,500 | $12.50 | $0.01875 |
| Audio Standard output | 3,000 | $23.00 | $0.06900 |
| **Total Voice Live** | | | **$0.08775** |

##### Paso 3: Costo del LLM (tu deployment)

El LLM procesa **texto** con contexto acumulado por turno (system prompt + historial):

| Turno | Tokens input (acumulado) | Tokens output | Notas |
|---|---:|---:|---|
| 1 | 600 | 80 | System prompt + primer mensaje |
| 2 | 780 | 80 | + turno 1 completo |
| 3 | 960 | 80 | + turno 2 completo |
| 4 | 1,140 | 80 | + turno 3 completo |
| 5 | 1,320 | 80 | + turno 4 completo |
| **Total** | **4,800** | **400** | |

##### Paso 4: Costo total por escenario

**🅰 BYOM + PTU (gpt-4o):**

| Línea | Cálculo | Costo |
|---|---|---:|
| Voice Live BYO (audio) | 1,500 × $12.50/1M + 3,000 × $23/1M | $0.088 |
| LLM (PTU — ya pagado mensualmente) | $0 marginal | $0.000 |
| **TOTAL por sesión** | | **$0.088** |

**🅱 BYOM + PAYG gpt-4o ($2.50/$10 por 1M tokens):**

| Línea | Cálculo | Costo |
|---|---|---:|
| Voice Live BYO (audio) | (ver arriba) | $0.088 |
| LLM input | 4,800 × ($2.50/1M) | $0.012 |
| LLM output | 400 × ($10.00/1M) | $0.004 |
| **TOTAL por sesión** | | **$0.104** |

**🅲 BYOM + PAYG gpt-4o-mini ($0.15/$0.60 por 1M tokens):**

| Línea | Cálculo | Costo |
|---|---|---:|
| Voice Live BYO (audio) | (ver arriba) | $0.088 |
| LLM input | 4,800 × ($0.15/1M) | $0.001 |
| LLM output | 400 × ($0.60/1M) | $0.000 |
| **TOTAL por sesión** | | **$0.089** |

**🅳 BYOM + PAYG Claude Sonnet ($3/$15 por 1M tokens):**

| Línea | Cálculo | Costo |
|---|---|---:|
| Voice Live BYO (audio) | (ver arriba) | $0.088 |
| LLM input | 4,800 × ($3.00/1M) | $0.014 |
| LLM output | 400 × ($15.00/1M) | $0.006 |
| **TOTAL por sesión** | | **$0.108** |

#### 📊 Comparación BYOM vs Managed (misma sesión de 5 min, 5 turnos)

| Configuración | Costo/sesión | Δ vs Pro cascada | Δ vs Basic native |
|---|---:|---|---|
| **BYOM + PTU** (gpt-4o) | **$0.088** | **-50%** ✅ | **-23%** ✅ |
| **BYOM + PAYG gpt-4o-mini** | **$0.089** | **-49%** ✅ | **-23%** ✅ |
| **BYOM + PAYG gpt-4o** | **$0.104** | **-41%** ✅ | **-10%** ✅ |
| **BYOM + PAYG Claude Sonnet** | **$0.108** | **-38%** ✅ | **-6%** ✅ |
| Managed Lite — Híbrido (phi4-mm) | $0.096 | -45% | -17% |
| Managed Basic — Full Native (realtime-mini) | $0.115 | -34% | — (baseline) |
| Managed Pro — Cascada (gpt-4.1) | $0.175 | — (baseline) | +52% |
| Managed Pro — Full Native S2S (gpt-realtime) | $0.334 | +91% | +191% |

> 🔑 **Insight:** BYOM + PTU es la **opción más económica** para modelos de gama alta (gpt-4o/gpt-4.1), incluso más barato que managed Lite. La clave: Voice Live BYO no cobra tokens de texto ni audio acumulado — solo el audio real procesado.

#### 📊 Costo relativo (línea base = BYOM + PTU)

```
BYOM + PTU (gpt-4o)          ██                 $0.088 / sesión  (1.0×)
BYOM + PAYG (gpt-4o-mini)    ██                 $0.089 / sesión  (1.0×)
Lite Híbrido (phi4-mm)        ███                $0.096 / sesión  (1.1×)
BYOM + PAYG (gpt-4o)         ███                $0.104 / sesión  (1.2×)
BYOM + PAYG (Claude Sonnet)  ███                $0.108 / sesión  (1.2×)
Basic Full Native             ████               $0.115 / sesión  (1.3×)
Pro Cascada                   ████████           $0.175 / sesión  (2.0×)
Pro Full Native               ████████████████   $0.334 / sesión  (3.8×)
```

#### 💡 ¿Cuándo elegir BYOM vs Managed?

| Criterio | Managed | BYOM + PAYG | BYOM + PTU |
|---|---|---|---|
| **Setup** | ✅ Zero config | ⚠️ Deploy modelo en Foundry | ⚠️ Deploy + comprar PTU |
| **Costo por sesión** | $$$ (Pro) / $$ (Basic) | $ (más barato que managed) | $ (el más barato por sesión) |
| **Costo fijo mensual** | $0 | $0 | $$$ (PTU mensual) |
| **Latencia LLM** | Managed (variable) | PAYG (variable) | **PTU (predecible, baja)** ✅ |
| **Modelos disponibles** | Solo catálogo fijo por tier | Cualquier modelo de Foundry ✅ | Cualquier modelo de Foundry ✅ |
| **Content safety custom** | Default | ✅ Tu configuración | ✅ Tu configuración |
| **Fine-tuned models** | ❌ | ✅ | ✅ |
| **Modelos de partners** | ❌ | ✅ Claude, Grok, Fireworks, etc. | ✅ Claude, Grok, Fireworks, etc. |
| **Ideal para** | MVPs, pruebas rápidas, bajo volumen | Volumen medio, modelos custom | Contact centers, alto volumen, SLA estricto |

#### 💰 Break-even PTU: ¿cuántas sesiones necesitas?

El PTU tiene un costo fijo mensual. El ahorro vs PAYG por sesión es pequeño (~$0.016 con gpt-4o). La fórmula:

```
Break-even (sesiones/mes) = Costo PTU mensual / Ahorro por sesión vs PAYG

Ejemplo con gpt-4o:
  - Supongamos PTU mensual = $2,000 (varía por modelo/región)
  - Ahorro por sesión vs PAYG = $0.104 - $0.088 = $0.016
  - Break-even = $2,000 / $0.016 = 125,000 sesiones/mes

  Pero el valor real de PTU NO es solo costo — es LATENCIA PREDECIBLE.
  Para un agente de voz, la diferencia entre 200ms y 800ms de latencia del LLM
  se siente directamente en la conversación.
```

> 💡 **Conclusión PTU:** Si tu motivación principal es **reducir costo**, PAYG con gpt-4o-mini ($0.089/sesión) puede ser más eficiente. Si necesitas **latencia predecible + garantía de capacidad** con modelos premium (gpt-4o/gpt-4.1), PTU vale la pena aunque el break-even de costo sea alto.

#### 📊 Proyección mensual a 10,000 sesiones

| Configuración | Costo/sesión | Costo/mes (10K sesiones) | Notas |
|---|---:|---:|---|
| **BYOM + PTU (gpt-4o)** | $0.088 | **$880** + PTU fijo | Requiere amortizar PTU |
| **BYOM + PAYG (gpt-4o-mini)** | $0.089 | **$890** | Sin costo fijo ✅ |
| **BYOM + PAYG (gpt-4o)** | $0.104 | **$1,040** | Sin costo fijo |
| Managed Lite Híbrido | $0.096 | **$960** | Zero config |
| Managed Basic Full Native | $0.115 | **$1,150** | Zero config |
| Managed Pro Cascada | $0.175 | **$1,750** | Zero config |
| Managed Pro Full Native | $0.334 | **$3,340** | Zero config |

> 💡 **Tip oficial:** Configura el content filter de tu deployment en **Asynchronous filtering** para reducir latencia con VoiceLive.

### Voice Live Avatar (add-on)

Se factura aparte como `interactive avatar (real-time)` bajo TTS Avatar. Consulta la página de pricing para precios actualizados (varias filas estaban marcadas N/A al momento de esta captura, indicando transición/preview).

---

## 🧩 Opción B — Servicios separados

Construyes el pipeline tú: STT → LLM → TTS (→ Avatar opcional). Cada componente se factura independientemente.

### 1️⃣ Speech-to-Text (por segundo, facturado por hora)

| Tipo | Real-time | Batch | Notas |
|---|---:|---:|---|
| **Standard** | **$1.00 / hr** | $0.18 / hr | Modelo base prebuilt |
| **Custom** | $1.20 / hr | $0.225 / hr | + $0.0538 / modelo / hora hosting |
| Custom Speech Training | — | — | $10 / hora de cómputo |
| Fast Transcription | N/A | — | (LLM Speech preview) |

**Add-ons (real-time, por feature):** +$0.30 / hora
- Continuous Language Identification
- Diarization
- Pronunciation Assessment (prosody)

> En batch, Language ID y Diarization están **incluidos** sin cargo extra.

### 2️⃣ Text-to-Speech (por carácter)

| Tipo | Precio | Notas |
|---|---:|---|
| **Neural / Neural HD** (real-time o batch) | **$15 / 1M chars** | Voces prebuilt |
| Custom Neural Voice Pro (real-time/batch) | $24 / 1M chars | + training + hosting |
| Custom Neural Voice HD | $48 / 1M chars | |
| Personal Voice | Free creation | Acceso limitado (requiere aprobación) |

**Custom Voice — training & hosting:**
- Voice model training: **$52 / hora de cómputo** (máx. $936 por entrenamiento)
- Endpoint hosting: **$4.04 / modelo / hora**

### 3️⃣ Speech Translation (opcional)

| Servicio | Precio |
|---|---|
| Real-time Speech Translation | $2.50 / hora de audio |
| Live Interpreter — Input audio | $1.00 / hora |
| Live Interpreter — Output text | $10 / 1M chars |
| Live Interpreter — Output audio (Standard) | $1.50 / hora |
| Video Translation (Standard voice) | $15 / hora |

### 4️⃣ Avatar (add-on de TTS, por segundo)

Standard Interactive Avatar (real-time) y 4K avatar — consulta la página de pricing oficial para precios actualizados (filas en transición).

### 5️⃣ LLM (Azure OpenAI, facturado por separado)

No incluido en Speech pricing — consulta [Azure OpenAI pricing](https://azure.microsoft.com/en-us/pricing/details/cognitive-services/openai-service/).

Ejemplo: GPT-4o-mini ≈ $0.15 / 1M input tokens, $0.60 / 1M output tokens.

---

## 🧮 Comparación práctica — conversación de 5 minutos

### 🔑 Cómo VoiceLive cobra realmente (basado en los 4 [escenarios oficiales](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/voice-live#example-pricing-scenarios))

VoiceLive **NO cobra una sola tarifa**. En cada sesión se cobran **varias líneas en paralelo**:

| Línea de cobro | Cuándo aplica | Qué incluye |
|---|---|---|
| **Text** | **SIEMPRE** | System prompt, contexto, transcripts internos, function-call args, cached input |
| **Native audio (input + output)** | Cuando usas `gpt-realtime`, `gpt-realtime-mini`, `phi4-mm-realtime` | Audio que el modelo procesa directamente (sin pasar por STT/TTS separado) |
| **Audio - Standard** | Modo cascada (`gpt-4o`, `gpt-4.1`, `gpt-5`, etc.) o output Azure Speech voz neural | STT + TTS Neural prebuilt |
| **Audio - Custom** | Cuando usas custom STT o custom voice | STT custom o voz neural custom (+ training/hosting aparte) |
| **Avatar (real-time)** | Si activas avatar | Facturado como TTS Avatar |
| **Extras** | Custom voice/STT training, hosting, avatar model | Cargos fijos aparte |

> ⚠️ **Importante:** los tiers (Pro/Basic/Lite) pueden **mezclarse en la misma sesión**. Ej: si usas Phi (Lite) + custom voice, el LLM va a tarifa Lite pero la voz custom va a tarifa **Pro**.

### 📜 Los 4 escenarios oficiales de Microsoft

#### Scenario 1: Agente customer service — GPT-4.1 + Standard Azure Speech in + Custom Azure Speech out + custom avatar

**Líneas cobradas:**
- **Voice Live Pro** rate: Text + Audio Standard (input) + Audio Custom (output)
- **Aparte**: training/hosting custom voice + custom avatar

**Cálculo (5 min, 5 turnos):**

| Línea | Tier | Cálculo | Costo |
|---|---|---|---:|
| Text input fresh | Pro | 860 × ($4/1M) | $0.0034 |
| Text input cached | Pro | 2,540 × ($0.40/1M) | $0.0010 |
| Text output | Pro | 450 × ($16/1M) | $0.0072 |
| Audio Standard input fresh | Pro | 4,000 × ($17/1M) | $0.0680 |
| Audio Standard input cached | Pro | 6,600 × ($0.40/1M) | $0.0026 |
| Audio Custom output | Pro | 3,000 × ($55/1M) | $0.1650 |
| **Subtotal sesión** | | | **≈ $0.247** |
| Custom voice training (one-time) | aparte | $52/hr (máx $936) | +$$$ |
| Custom voice hosting | aparte | $4.04/modelo/hr | +$$$/mes |
| Custom avatar training (one-time) | aparte | (limited access) | +$$$ |
| Custom avatar hosting | aparte | (limited access) | +$$$/mes |

#### Scenario 2: Agente educativo — `gpt-realtime` (native audio in) + Azure Speech Standard out

**Líneas cobradas:**
- **Voice Live Pro** rate: Text + Native audio + Audio Standard

**Cálculo (5 min, 5 turnos):**

| Línea | Tier | Cálculo | Costo |
|---|---|---|---:|
| Text input fresh | Pro | 860 × ($4/1M) | $0.0034 |
| Text input cached | Pro | 2,540 × ($0.40/1M) | $0.0010 |
| Text output | Pro | 450 × ($16/1M) | $0.0072 |
| Native audio input fresh | Pro | 4,000 × ($32/1M) | $0.1280 |
| Native audio input cached | Pro | 6,600 × ($0.40/1M) | $0.0026 |
| Audio Standard output | Pro | 3,000 × ($31/1M) | $0.0930 |
| **TOTAL sesión** | | | **≈ $0.235** |

> 💡 El [calculator oficial](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/voice-live#pricing) muestra **$0.37** para este escenario con `gpt-realtime` **native input + native output** (no Azure Speech Standard out). La diferencia: native audio output cuesta $64/1M en lugar de $31/1M Standard → $0.21 vs $0.093, +$0.13 más.

#### Scenario 3: Agente entrevistas — `gpt-realtime-mini` (native audio in) + Azure Speech Standard out + standard avatar

**Líneas cobradas:**
- **Voice Live Basic** rate: Text + Native audio + Audio Standard
- **Aparte**: TTS Avatar standard (real-time)

**Cálculo (5 min, 5 turnos):**

| Línea | Tier | Cálculo | Costo |
|---|---|---|---:|
| Text input fresh | Basic | 860 × ($0.66/1M) | $0.0006 |
| Text input cached | Basic | 2,540 × ($0.33/1M) | $0.0008 |
| Text output | Basic | 450 × ($2.64/1M) | $0.0012 |
| Native audio input fresh | Basic | 4,000 × ($11/1M) | $0.0440 |
| Native audio input cached | Basic | 6,600 × ($0.33/1M) | $0.0022 |
| Audio Standard output | Basic | 3,000 × ($26/1M) | $0.0780 |
| **Subtotal Voice Live** | | | **≈ $0.127** |
| Interactive Avatar standard (real-time) | aparte | ~5 min × tarifa/min | +$$ |
| **TOTAL estimado** | | | **≈ $0.13 + avatar** |

#### Scenario 4: Asistente in-car — `phi4-mm-realtime` + Azure custom voice

**Líneas cobradas:**
- **Voice Live Lite** rate: Text + Native audio (Phi)
- **Voice Live Pro** rate: Audio Custom (¡la custom voice va a Pro aunque el LLM sea Lite!)
- **Aparte**: training/hosting custom voice

**Cálculo (5 min, 5 turnos; Phi usa ~12.5 tokens/seg input → audio fresh ~5,000):**

| Línea | Tier | Cálculo | Costo |
|---|---|---|---:|
| Text input fresh | Lite | 860 × ($0.11/1M) | $0.0001 |
| Text input cached | Lite | 2,540 × ($0.04/1M) | $0.0001 |
| Text output | Lite | 450 × ($0.44/1M) | $0.0002 |
| Native audio input fresh (Phi) | Lite | 5,000 × ($4/1M) | $0.0200 |
| Native audio input cached | Lite | 8,250 × ($0.04/1M) | $0.0003 |
| Audio Custom output | **Pro** | 3,000 × ($55/1M) | $0.1650 |
| **Subtotal sesión** | | | **≈ $0.186** |
| Custom voice training (one-time) | aparte | $52/hr (máx $936) | +$$$ |
| Custom voice hosting | aparte | $4.04/modelo/hr (~$2,910/mes) | +$$$/mes |

> 📊 **Resumen comparativo de los 4 escenarios oficiales (por sesión de 5 min, 5 turnos):**
>
> | Escenario | Costo sesión | Costos aparte |
> |---|---:|---|
> | 1 — GPT-4.1 cascada + custom voice + custom avatar | **~$0.25** | Custom voice + custom avatar |
> | 2 — gpt-realtime native in + Azure Speech Standard out | **~$0.24** | — |
> | 2b — gpt-realtime native in + **native out** (caso calculator) | **~$0.37** | — |
> | 3 — gpt-realtime-mini native + Azure Standard + std avatar | **~$0.13** | TTS Avatar real-time |
> | 4 — Phi-mm native + custom voice | **~$0.19** | Custom voice training/hosting |
> | **5a — BYOM + PTU (gpt-4o, Audio Standard)** | **~$0.09** | **PTU mensual (fijo)** |
> | **5b — BYOM + PAYG (gpt-4o, Audio Standard)** | **~$0.10** | — |
> | **5c — BYOM + PAYG (gpt-4o-mini, Audio Standard)** | **~$0.09** | — |
>
> ⚠️ Los **costos aparte** (training, hosting de custom voice/avatar) son **cargos fijos one-time o mensuales** — NO se cobran por sesión. Si haces 10,000 sesiones al mes, los amortizas; si haces 100, te salen carisimas por sesión.
>
> 🔍 **Validado** contra el [Azure Voice Live Pricing Calculator](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/voice-live#pricing) oficial: Scenario 2b (Pro + native S2S in/out, 5 min, 5 turnos, prompt 500 tok) → **$0.37**.

---

## 🔄 ÉNFASIS: Modo Cascada vs Audio Nativo — Análisis detallado de precios

> **Pregunta clave:** ¿Cómo varía el precio de una sesión según el modo de procesamiento de audio?
> La respuesta depende del **tier** — y el resultado es sorprendente.

### ¿Qué es cada modo de procesamiento?

Voice Live ofrece **tres modos** de procesar audio, determinados por el modelo elegido y la configuración de `audio_input_path` / `audio_output_path`:

```
🅰 CASCADA (Azure Speech → LLM texto → Azure Speech)
┌──────────┐    ┌──────────────┐    ┌──────────┐    ┌──────────────┐    ┌──────────┐
│ 🎤 Mic   │───▶│ Azure Speech │───▶│ LLM      │───▶│ Azure Speech │───▶│ 🔊 Audio │
│ (audio)  │    │ STT          │    │ (texto)  │    │ TTS          │    │ output   │
└──────────┘    │ audio→texto  │    │ gpt-4.1  │    │ texto→audio  │    └──────────┘
                └──────────────┘    └──────────┘    └──────────────┘
                Se cobran tarifas "Audio Standard" para tokens de audio
```

> 🍳 **En simple — "Doble traductor":** `Tu voz → [Azure Speech traduce a texto] → [LLM piensa en texto] → [Azure Speech traduce a audio]`
> El modelo (ej. `gpt-4.1-mini`) **no entiende audio** — solo texto. Azure Speech hace TODO el trabajo de audio (STT + TTS). Se cobran tarifas **"Audio Standard"** para input Y output.

> 📝 **¿Qué meters se cobran?**
> | Meter | Input | Output | ¿Por qué? |
> |-------|:-----:|:------:|:-----------|
> | 🔤 **Text** | ✅ | ✅ | STT transcribe tu voz, LLM lee/genera texto, system prompt, function calls |
> | 🔊 **Audio Standard** | ✅ | ✅ | Azure Speech STT (input) + Azure Speech TTS (output) |
> | 🎧 **Native S2S** | ❌ | ❌ | No aplica — el modelo no toca audio directamente |
>
> 💰 Texto pesa solo **~3.5%** del costo total · El grueso es **Audio Standard**

```
🅱 HÍBRIDO (Audio nativo input → LLM → Azure Speech TTS output)
┌──────────┐    ┌──────────────────────┐    ┌──────────────┐    ┌──────────┐
│ 🎤 Mic   │───▶│ Modelo multimodal    │───▶│ Azure Speech │───▶│ 🔊 Audio │
│ (audio)  │    │ (gpt-realtime)       │    │ TTS          │    │ output   │
└──────────┘    │ entiende audio       │    │ texto→audio  │    └──────────┘
                │ directamente         │    └──────────────┘
                └──────────────────────┘
                Input: tarifas "Native S2S"   Output: tarifas "Audio Standard"
```

> 🍳 **En simple — "Un oído nativo, una boca prestada":** `Tu voz → [Modelo entiende tu audio directamente] → [Azure Speech genera la voz de salida]`
> El modelo (ej. `gpt-realtime-mini`) **SÍ entiende audio** de entrada, pero la salida pasa por Azure Speech TTS (porque eliges una voz específica como Dragon HD). Input: tarifas **"Native S2S"** / Output: tarifas **"Audio Standard"**.

> 📝 **¿Qué meters se cobran?**
> | Meter | Input | Output | ¿Por qué? |
> |-------|:-----:|:------:|:-----------|
> | 🔤 **Text** | ✅ | ✅ | Transcript interno del audio, respuesta en texto, system prompt, function calls |
> | 🔊 **Audio Standard** | ❌ | ✅ | Solo output — Azure Speech TTS genera la voz de salida |
> | 🎧 **Native S2S** | ✅ | ❌ | Solo input — el modelo entiende tu audio directamente |
>
> 💰 Texto pesa solo **~2–3.5%** del costo total · El grueso es **Native S2S (in) + Audio Standard (out)**

```
🅲 FULL NATIVE S2S (Audio nativo input → LLM → Audio nativo output)
┌──────────┐    ┌────────────────────────────────────┐    ┌──────────┐
│ 🎤 Mic   │───▶│ Modelo multimodal                  │───▶│ 🔊 Audio │
│ (audio)  │    │ (gpt-realtime / gpt-realtime-mini) │    │ output   │
└──────────┘    │ input y output nativos             │    └──────────┘
                │ — máxima naturalidad de voz         │
                └────────────────────────────────────┘
                Se cobran tarifas "Native S2S" para input Y output
```

> 🍳 **En simple — "Todo en el modelo":** `Tu voz → [Modelo entiende Y genera audio directamente] → Altavoz`
> El modelo hace TODO — entrada y salida de audio. No se usa Azure Speech ni para STT ni para TTS. Se cobran tarifas **"Native S2S"** para input Y output. ❌ No puedes elegir voz (solo la voz nativa del modelo).

> 📝 **¿Qué meters se cobran?**
> | Meter | Input | Output | ¿Por qué? |
> |-------|:-----:|:------:|:-----------|
> | 🔤 **Text** | ✅ | ✅ | ⚠️ SÍ se cobra — transcripts internos, system prompt, function calls, historial cached |
> | 🔊 **Audio Standard** | ❌ | ❌ | No aplica — no se usa Azure Speech en ninguna dirección |
> | 🎧 **Native S2S** | ✅ | ✅ | Input Y output — el modelo procesa audio directamente en ambas direcciones |
>
> 💰 Texto pesa solo **~2–3.5%** del costo total · El grueso es **Native S2S (in + out)**

> 🍳 **¿Cuál es más barato? Depende del tier — y es contraintuitivo:**
> **PRO:** Cascada = barato, Native = caro → Usa cascada (`gpt-4.1`) |
> **BASIC:** Cascada = caro, Native = barato → Usa native (`gpt-realtime-mini`) |
> **LITE:** Híbrido gana (native input baratísimo) → Usa `phi4-mm-realtime`
>
> ¿Por qué? Porque `gpt-realtime` (Pro) es un modelo **premium flagship** — Microsoft cobra mucho por su audio nativo. Pero `gpt-realtime-mini` (Basic) es más ligero y eficiente — cuesta **menos** que usar Azure Speech por separado.

### ¿Qué modelos soportan cada modo?

| Modelo | Tier | 🅰 Cascada | 🅱 Híbrido | 🅲 Full Native | 🅳 BYOM |
|---|---|:---:|:---:|:---:|:---:|
| gpt-4o | Pro | ✅ | — | — | — |
| gpt-4.1 | Pro | ✅ | — | — | — |
| gpt-5, gpt-5-chat | Pro | ✅ | — | — | — |
| **gpt-realtime** | **Pro** | — | **✅** | **✅** | — |
| gpt-4o-mini | Basic | ✅ | — | — | — |
| gpt-4.1-mini | Basic | ✅ | — | — | — |
| gpt-5-mini | Basic | ✅ | — | — | — |
| **gpt-realtime-mini** | **Basic** | — | **✅** | **✅** | — |
| gpt-5-nano | Lite | ✅ | — | — | — |
| phi4-mini | Lite | ✅ | — | — | — |
| **phi4-mm-realtime** | **Lite** | — | **✅** ¹ | — | — |
| Tu gpt-4o / gpt-4.1 / gpt-5 (deployment propio) | **BYOM** | — | — | — | **✅** ² |
| Tu gpt-realtime / gpt-realtime-mini (deployment propio) | **BYOM** | — | — | — | **✅** ² |
| Claude Sonnet/Haiku vía Foundry | **BYOM** | — | — | — | **✅** ³ |
| Grok, fine-tuned, model router | **BYOM** | — | — | — | **✅** ² |

> ¹ phi4-mm-realtime solo soporta audio nativo de **entrada**. La salida SIEMPRE pasa por Azure Speech TTS (modo híbrido forzado).
> ² BYOM: Voice Live hace STT/TTS (Audio Standard BYO $12.50/$23), tu deployment ejecuta el LLM. Perfiles: `byom-azure-openai-chat-completion` o `byom-azure-openai-realtime`.
> ³ Claude vía Foundry Messages API con perfil `byom-foundry-anthropic-messages` (⚠️ Preview).

**Regla general:**
- **Modelos de solo texto** (gpt-4o, gpt-4.1, phi4-mini, etc.) → solo cascada
- **Modelos multimodales** (gpt-realtime, phi4-mm-realtime) → híbrido y/o full native
- **Tu propio deployment** (BYOM) → siempre cascada con Azure Speech STT/TTS de Voice Live
- La elección de modo **determina qué tarifas de audio se aplican**

### Tarifas de audio: comparación lado a lado (por 1M tokens)

| Tier | Línea | Audio Standard (Cascada) | Native S2S | Δ Diferencia |
|---|---|---:|---:|---|
| **Pro** | Input | $17.00 | $32.00 | Native es **88% más caro** |
| | Cached Input | $0.40 | $0.40 | Igual |
| | Output | $31.00 | $64.00 | Native es **106% más caro** |
| **Basic** | Input | $15.00 | $11.00 | Native es **27% más barato** ✅ |
| | Cached Input | $0.33 | $0.33 | Igual |
| | Output | $26.00 | $22.00 | Native es **15% más barato** ✅ |
| **Lite** | Input | $15.00 | $4.00 | Native es **73% más barato** ✅ |
| | Cached Input | $0.04 | $0.04 | Igual |
| | Output | $25.00 | — ⁴ | N/A |
| **BYOM** | Input | $12.50 | — | BYO es **17–26% más barato** que managed Audio Std ✅ |
| | Cached Input | — ⁵ | — | Sin cached input en BYO |
| | Output | $23.00 | — | BYO es **8–12% más barato** que managed Audio Std ✅ |

> ⁴ phi4-mm-realtime no genera audio nativo de salida — se usa Azure Speech TTS (Audio Standard output).
> ⁵ BYOM no cobra cached input — solo procesa el audio real (sin re-procesar contexto acumulado).

> 🔑 **Insight clave:** En **Pro**, las tarifas Native S2S son **2× más caras** que Audio Standard.
> En **Basic** y **Lite**, las tarifas Native S2S son **más baratas** que Audio Standard.
> En **BYOM**, las tarifas Audio Standard son **las más bajas** de todos los tiers, y además no hay cached input.
> Esto invierte completamente la ecuación de costos según el tier.

### 📝 Resumen: Facturación de texto vs audio por sesión

> Cada modo de procesamiento arriba explica de dónde vienen los tokens de texto. Aquí resumimos cuánto pesan en el costo total:

```
Factura de cada sesión de voz:
┌─────────────────────────────────────────────────┐
│  METER 1: "Voice Live [Tier] — Text"            │ ← SIEMPRE se cobra (excepto BYOM)
│  (system prompt + transcripts + respuestas)      │
├─────────────────────────────────────────────────┤
│  METER 2: "Audio Standard" ó "Native S2S"       │ ← Varía según el modo
│  (el audio procesado: entrada y salida de voz)   │
├─────────────────────────────────────────────────┤
│  METER 3 (solo BYOM): Tu deployment LLM         │ ← Factura separada (PTU o PAYG)
│  (tokens de texto procesados por tu modelo)      │
└─────────────────────────────────────────────────┘
```

| Tier + Modo | Costo texto | Costo audio | % texto del total |
|---|---:|---:|---:|
| Pro Cascada | $0.012 | $0.164 | ~3.5% |
| Pro Full Native | $0.012 | $0.323 | ~3.5% |
| Basic Full Native | $0.003 | $0.112 | ~2.3% |
| Lite Híbrido | $0.000 | $0.095 | ~0.4% |
| BYOM + PTU | $0.000 (VL) | $0.088 | 0% (VL) ⁶ |
| BYOM + PAYG gpt-4o | $0.016 (tu dep.) | $0.088 | ~15% ⁶ |
| BYOM + PAYG mini | $0.001 (tu dep.) | $0.088 | ~1% ⁶ |

> ⁶ En BYOM, Voice Live **no cobra texto** — el costo de texto es la factura de tu propio deployment. Con PTU el texto es $0 marginal. El "% texto del total" incluye el costo LLM de tu deployment como proxy.

> 💡 **Conclusión:** El texto siempre se cobra pero **pesa solo 0.4–3.5% del total** en managed tiers. En BYOM con PTU, el costo de LLM es $0 marginal — el 100% del costo Voice Live es **audio**. La diferencia entre modos se determina por las **tarifas de audio**, no por las de texto.

### Cálculo completo: misma sesión de 5 min en cada modo

> Sesión estándar: 5 min, 5 turnos, 30s user + 30s assistant cada turno, system prompt 500 tokens.
> Token counts validados contra calculator oficial (ver sección de estimación de tokens más abajo).

---

#### PRO TIER

##### 🅰 Pro — Cascada (gpt-4.1 + Azure Speech STT/TTS)

| Línea | Tokens | Tarifa (/1M) | Costo |
|---|---:|---:|---:|
| Text input fresh | 860 | $4.00 | $0.00344 |
| Text input cached | 2,540 | $0.40 | $0.00102 |
| Text output | 450 | $16.00 | $0.00720 |
| Audio Standard input fresh | 4,000 | $17.00 | $0.06800 |
| Audio Standard input cached | 6,600 | $0.40 | $0.00264 |
| Audio Standard output | 3,000 | $31.00 | $0.09300 |
| **TOTAL** | | | **$0.1753** |

##### 🅱 Pro — Híbrido (gpt-realtime nativo in + Azure Speech Standard out)

| Línea | Tokens | Tarifa (/1M) | Costo |
|---|---:|---:|---:|
| Text input fresh | 860 | $4.00 | $0.00344 |
| Text input cached | 2,540 | $0.40 | $0.00102 |
| Text output | 450 | $16.00 | $0.00720 |
| **Native** audio input fresh | 4,000 | **$32.00** | $0.12800 |
| **Native** audio input cached | 6,600 | $0.40 | $0.00264 |
| Audio Standard output | 3,000 | $31.00 | $0.09300 |
| **TOTAL** | | | **$0.2353** |

##### 🅲 Pro — Full Native S2S (gpt-realtime nativo in + nativo out)

| Línea | Tokens | Tarifa (/1M) | Costo |
|---|---:|---:|---:|
| Text input fresh | 860 | $4.00 | $0.00344 |
| Text input cached | 2,540 | $0.40 | $0.00102 |
| Text output | 450 | $16.00 | $0.00720 |
| **Native** audio input fresh | 4,000 | **$32.00** | $0.12800 |
| **Native** audio input cached | 6,600 | $0.40 | $0.00264 |
| **Native** audio output | 3,000 | **$64.00** | **$0.19200** |
| **TOTAL** | | | **$0.3343** |

> 📊 **Resultado Pro:** Cascada ($0.18) < Híbrido ($0.24) < Full Native ($0.33)
> ➡️ En Pro, **cascada ahorra hasta 48%** vs full native S2S.
> *(El calculator oficial reporta ~$0.37 para Full Native Pro — la diferencia se debe a token counts ligeramente más altos en la estimación oficial.)*

---

#### BASIC TIER

##### 🅰 Basic — Cascada (gpt-4.1-mini + Azure Speech STT/TTS)

| Línea | Tokens | Tarifa (/1M) | Costo |
|---|---:|---:|---:|
| Text input fresh | 860 | $0.66 | $0.00057 |
| Text input cached | 2,540 | $0.33 | $0.00084 |
| Text output | 450 | $2.64 | $0.00119 |
| Audio Standard input fresh | 4,000 | $15.00 | $0.06000 |
| Audio Standard input cached | 6,600 | $0.33 | $0.00218 |
| Audio Standard output | 3,000 | $26.00 | $0.07800 |
| **TOTAL** | | | **$0.1428** |

##### 🅱 Basic — Híbrido (gpt-realtime-mini nativo in + Azure Speech Standard out)

| Línea | Tokens | Tarifa (/1M) | Costo |
|---|---:|---:|---:|
| Text input fresh | 860 | $0.66 | $0.00057 |
| Text input cached | 2,540 | $0.33 | $0.00084 |
| Text output | 450 | $2.64 | $0.00119 |
| **Native** audio input fresh | 4,000 | **$11.00** | $0.04400 |
| **Native** audio input cached | 6,600 | $0.33 | $0.00218 |
| Audio Standard output | 3,000 | $26.00 | $0.07800 |
| **TOTAL** | | | **$0.1268** |

##### 🅲 Basic — Full Native S2S (gpt-realtime-mini nativo in + nativo out)

| Línea | Tokens | Tarifa (/1M) | Costo |
|---|---:|---:|---:|
| Text input fresh | 860 | $0.66 | $0.00057 |
| Text input cached | 2,540 | $0.33 | $0.00084 |
| Text output | 450 | $2.64 | $0.00119 |
| **Native** audio input fresh | 4,000 | **$11.00** | $0.04400 |
| **Native** audio input cached | 6,600 | $0.33 | $0.00218 |
| **Native** audio output | 3,000 | **$22.00** | $0.06600 |
| **TOTAL** | | | **$0.1148** |

> 📊 **Resultado Basic:** Full Native ($0.115) < Híbrido ($0.127) < Cascada ($0.143)
> ➡️ En Basic, **full native S2S ahorra 20%** vs cascada.
> Esto es **contraintuitivo** — en Basic, las tarifas native son menores que Audio Standard.

---

#### LITE TIER

##### 🅰 Lite — Cascada (phi4-mini + Azure Speech STT/TTS)

| Línea | Tokens | Tarifa (/1M) | Costo |
|---|---:|---:|---:|
| Text input fresh | 860 | $0.11 | $0.00009 |
| Text input cached | 2,540 | $0.04 | $0.00010 |
| Text output | 450 | $0.44 | $0.00020 |
| Audio Standard input fresh | 5,000 ¹ | $15.00 | $0.07500 |
| Audio Standard input cached | 8,250 ¹ | $0.04 | $0.00033 |
| Audio Standard output | 3,000 | $25.00 | $0.07500 |
| **TOTAL** | | | **$0.1507** |

> ¹ Phi tokeniza a 12.5 tok/seg (vs 10 tok/seg de Azure OpenAI), por eso más tokens de input.

##### 🅱 Lite — Híbrido (phi4-mm-realtime nativo in + Azure Speech Standard out)

| Línea | Tokens | Tarifa (/1M) | Costo |
|---|---:|---:|---:|
| Text input fresh | 860 | $0.11 | $0.00009 |
| Text input cached | 2,540 | $0.04 | $0.00010 |
| Text output | 450 | $0.44 | $0.00020 |
| **Native** audio input fresh | 5,000 | **$4.00** | $0.02000 |
| **Native** audio input cached | 8,250 | $0.04 | $0.00033 |
| Audio Standard output | 3,000 | $25.00 | $0.07500 |
| **TOTAL** | | | **$0.0957** |

##### 🅲 Lite — Full Native S2S: NO DISPONIBLE

> phi4-mm-realtime **no genera audio nativo de salida** → no existe modo full native en Lite.
> La salida SIEMPRE pasa por Azure Speech TTS.

> 📊 **Resultado Lite:** Híbrido ($0.096) < Cascada ($0.151)
> ➡️ En Lite, **híbrido ahorra 36%** vs cascada, gracias a que native input cuesta $4/1M vs $15/1M.

---

#### BYOM TIER (Bring Your Own Model)

> En BYOM, Voice Live solo cobra **Audio Standard** ($12.50/$23 por 1M tokens) — el procesamiento de audio (STT + TTS). Tu deployment paga el LLM por separado.
>
> **Diferencia clave vs managed:** No hay cached input — BYOM solo procesa el audio real (voz del usuario y voz sintetizada), no re-procesa contexto acumulado. Esto reduce drásticamente los tokens de audio facturados.

```
🅳 BYOM (Tu modelo → Azure Speech STT/TTS de Voice Live)
┌──────────┐    ┌──────────────┐    ┌──────────────────┐    ┌──────────────┐    ┌──────────┐
│ 🎤 Mic   │───▶│ Azure Speech │───▶│ TU deployment    │───▶│ Azure Speech │───▶│ 🔊 Audio │
│ (audio)  │    │ STT (VL)     │    │ (PTU o PAYG)     │    │ TTS (VL)     │    │ output   │
└──────────┘    │ audio→texto  │    │ gpt-4o, Claude,  │    │ texto→audio  │    └──────────┘
                └──────────────┘    │ Grok, fine-tuned  │    └──────────────┘
                                    └──────────────────┘
                Voice Live cobra Audio Standard          Tu deployment cobra el LLM
```

> 🍳 **En simple — "Cascada con tu propio cerebro":** `Tu voz → [Azure Speech STT de Voice Live] → [TU modelo lo procesa] → [Azure Speech TTS de Voice Live] → Altavoz`
> Voice Live solo hace el audio (STT + TTS). El razonamiento lo hace tu deployment. Se cobran **dos facturas separadas**.

> 📝 **¿Qué meters se cobran?**
> | Meter | Input | Output | ¿Por qué? |
> |-------|:-----:|:------:|:-----------|
> | 🔤 **Text (Voice Live)** | ❌ | ❌ | Voice Live BYO NO cobra texto — lo cobra tu deployment |
> | 🔊 **Audio Standard (Voice Live)** | ✅ | ✅ | Azure Speech STT (input) + Azure Speech TTS (output) a tarifa BYO |
> | 🎧 **Native S2S** | ❌ | ❌ | No aplica — BYOM siempre usa Azure Speech para audio |
> | 🧠 **LLM (tu deployment)** | ✅ | ✅ | Tu factura de Azure OpenAI / Foundry (PTU o PAYG) |
>
> 💰 Audio Standard de Voice Live: **~$0.088** · LLM: **$0** (PTU) a **~$0.020** (PAYG) · Total: **$0.088–$0.108**

##### 🅳 BYOM + PTU (gpt-4o, LLM marginal = $0)

| Línea | Tokens | Tarifa (/1M) | Costo |
|---|---:|---:|---:|
| Audio Standard input (VL BYO) | 1,500 | $12.50 | $0.01875 |
| Audio Standard output (VL BYO) | 3,000 | $23.00 | $0.06900 |
| LLM input (PTU — ya pagado) | 4,800 | $0.00 | $0.00000 |
| LLM output (PTU — ya pagado) | 400 | $0.00 | $0.00000 |
| **TOTAL** | | | **$0.0878** |

##### 🅳 BYOM + PAYG gpt-4o ($2.50/$10 por 1M tokens)

| Línea | Tokens | Tarifa (/1M) | Costo |
|---|---:|---:|---:|
| Audio Standard input (VL BYO) | 1,500 | $12.50 | $0.01875 |
| Audio Standard output (VL BYO) | 3,000 | $23.00 | $0.06900 |
| LLM input (PAYG) | 4,800 | $2.50 | $0.01200 |
| LLM output (PAYG) | 400 | $10.00 | $0.00400 |
| **TOTAL** | | | **$0.1038** |

##### 🅳 BYOM + PAYG gpt-4o-mini ($0.15/$0.60 por 1M tokens)

| Línea | Tokens | Tarifa (/1M) | Costo |
|---|---:|---:|---:|
| Audio Standard input (VL BYO) | 1,500 | $12.50 | $0.01875 |
| Audio Standard output (VL BYO) | 3,000 | $23.00 | $0.06900 |
| LLM input (PAYG) | 4,800 | $0.15 | $0.00072 |
| LLM output (PAYG) | 400 | $0.60 | $0.00024 |
| **TOTAL** | | | **$0.0887** |

##### 🅳 BYOM + PAYG Claude Sonnet ($3/$15 por 1M tokens)

| Línea | Tokens | Tarifa (/1M) | Costo |
|---|---:|---:|---:|
| Audio Standard input (VL BYO) | 1,500 | $12.50 | $0.01875 |
| Audio Standard output (VL BYO) | 3,000 | $23.00 | $0.06900 |
| LLM input (PAYG) | 4,800 | $3.00 | $0.01440 |
| LLM output (PAYG) | 400 | $15.00 | $0.00600 |
| **TOTAL** | | | **$0.1082** |

> 📊 **Resultado BYOM:** PTU ($0.088) ≈ PAYG mini ($0.089) < PAYG gpt-4o ($0.104) < Claude ($0.108)
> ➡️ BYOM es **el modo más barato en todos los escenarios** — entre 25–74% más barato que managed.
> ¿Por qué? (1) Tarifa Audio Standard BYO más baja ($12.50/$23 vs $15–17/$25–31), (2) sin cached input billing, (3) PTU elimina costo marginal de LLM.

---

### 📊 Resumen comparativo: costo por sesión de 5 min según modo

| Modo | Pro | Basic | Lite | BYO (Audio Std) |
|---|---:|---:|---:|---:|
| 🅰 Cascada (AzSpeech in + AzSpeech out) | **$0.175** ✅ | $0.143 | $0.151 | N/A |
| 🅱 Híbrido (Native in + AzSpeech out) | $0.235 | $0.127 | **$0.096** ✅ | N/A |
| 🅲 Full Native S2S (Native in + Native out) | $0.334 | **$0.115** ✅ | N/A | N/A |
| 🅳 BYOM (Audio Standard, tu deployment) | N/A | N/A | N/A | **$0.088–$0.108** ✅ |
| **Modo más económico** | **🅰 Cascada** | **🅲 Full Native** | **🅱 Híbrido** | **🅳 BYOM+PTU** |
| **Ahorro vs modo más caro** | 48% | 20% | 36% | 50–74%² |

> ² BYOM ahorro calculado comparando $0.088 (PTU) vs $0.175 (Pro cascada) = 50% y vs $0.334 (Pro Full Native) = 74%. Rango depende de la configuración managed contra la que compares.

### ⚡ ¿Por qué el modo más barato varía según el tier?

La respuesta está en cómo Microsoft estructura las tarifas de audio:

```
PRO:  Audio Standard ($17/$31) << Native S2S ($32/$64)
      ─────────────────────────────────────────────────
      Native cuesta 2× más → Cascada gana en precio

BASIC: Audio Standard ($15/$26) >> Native S2S ($11/$22)
       ─────────────────────────────────────────────────
       Native cuesta 27% MENOS → Full native gana

LITE:  Audio Standard input ($15) >> Native input ($4)
       ─────────────────────────────────────────────────
       Native input cuesta 73% MENOS → Híbrido gana
       (no hay native output, así que full S2S no existe)

BYOM:  Audio Standard BYO ($12.50/$23) + sin cached input
       ─────────────────────────────────────────────────
       Tarifa BYO más baja + solo audio real (1,500 vs ~10,600 tok input)
       + PTU puede eliminar costo LLM → BYOM gana en todos los escenarios
```

**¿Por qué?** Los modelos Pro (gpt-realtime) son modelos premium de frontera. Microsoft cobra un premium enorme por su capacidad de audio nativo ($32/$64 por 1M tokens). En cambio, Audio Standard (Azure Speech STT/TTS) es un servicio maduro y eficiente con tarifas más bajas ($17/$31).

En los tiers Basic y Lite, los modelos nativos (gpt-realtime-mini, phi4-mm-realtime) son más ligeros y eficientes. Microsoft les pone tarifas native **inferiores** a Audio Standard porque el costo computacional es menor.

### 🎯 ¿Cuándo elegir cada modo?

| Criterio | 🅰 Cascada | 🅱 Híbrido | 🅲 Full Native S2S | 🅳 BYOM |
|---|---|---|---|---|
| **Precio óptimo** | ✅ Pro tier | ✅ Lite tier | ✅ Basic tier | ✅✅ **Todos** (el más barato) |
| **Latencia** | ⚠️ Más alta (3 servicios en pipeline) | Moderada | ✅ Más baja (modelo único) | ⚠️ Similar a Cascada (STT→LLM→TTS) |
| **Naturalidad de voz** | Buena (Azure Speech TTS) | Input nativo + TTS controlable | ✅ Máxima (modelo genera voz directa) | Buena (Azure Speech TTS) |
| **Voces personalizadas** | ✅ Custom voice profiles | ✅ Custom voice en output | ❌ Solo voz del modelo | ✅ Custom voice profiles |
| **Idiomas soportados** | ✅ 140+ idiomas (Azure Speech) | Input: idiomas del modelo / Output: 140+ | Limitado a idiomas del modelo | ✅ 140+ idiomas (Azure Speech) |
| **Control de voz** | ✅ SSML, pitch, rate, style | Solo en output | ❌ Mínimo control | ✅ SSML, pitch, rate, style |
| **Emoción en input** | ❌ Se pierde en transcripción | ✅ El modelo "oye" emoción | ✅ El modelo "oye" emoción | ❌ Se pierde en transcripción |
| **Modelo a tu elección** | ❌ Solo modelos managed | ❌ Solo modelos managed | ❌ Solo modelos managed | ✅ Cualquier modelo, fine-tuned, partner |
| **Casos de uso ideales** | Contact centers, IVR, soporte multilingüe | Balance costo-calidad en Lite | Asistentes conversacionales naturales | Alto volumen con PTU, modelos custom, multi-proveedor |

### 💡 Recomendación práctica

```
¿Tienes tu propio deployment (PTU, fine-tuned, Claude, etc.)?
└── SÍ → 🅳 BYOM                                  ($0.088–$0.108/sesión) ✨✨ MÁS BARATO

¿Necesitas Pro (gpt-4.1, gpt-realtime)?
├── ¿Prioridad = precio?        → 🅰 Cascada con gpt-4.1      ($0.18/sesión)
├── ¿Prioridad = naturalidad?   → 🅲 Full native gpt-realtime ($0.33/sesión)
└── ¿Balance?                   → 🅱 Híbrido gpt-realtime     ($0.24/sesión)

¿Necesitas Basic (gpt-4.1-mini, gpt-realtime-mini)?
├── ¿Prioridad = precio?        → 🅲 Full native realtime-mini ($0.12/sesión) ✨
├── ¿Prioridad = control de voz?→ 🅰 Cascada con gpt-4.1-mini ($0.14/sesión)
└── ¿Balance?                   → 🅱 Híbrido realtime-mini    ($0.13/sesión)

¿Necesitas Lite (phi4-mini, phi4-mm-realtime)?
├── ¿Prioridad = precio?        → 🅱 Híbrido phi4-mm-realtime ($0.10/sesión) ✨
└── ¿Prioridad = control?       → 🅰 Cascada phi4-mini        ($0.15/sesión)
    (Full native no disponible en Lite)
```

> 📌 **Dato clave para 10,000 sesiones/mes:**
> | Config | Costo/mes |
> |---|---:|
> | Pro cascada (gpt-4.1) | **$1,753** |
> | Pro full native (gpt-realtime) | **$3,343** |
> | Basic full native (gpt-realtime-mini) | **$1,148** ← mejor relación calidad/precio |
> | Lite híbrido (phi4-mm-realtime) | **$957** ← más barato absoluto |

---

---

### 🅳 ¿Qué pasa con Voice Live BYO (BYOM)? — El "cuarto modo"

BYOM **no es un tier** — es un **modo de operación** donde tú traes tu propio modelo desplegado en Azure OpenAI / Foundry. VoiceLive solo se encarga del audio (STT + TTS) y tú pagas el LLM por separado.

#### ¿Cómo funciona BYOM?

```
🅳 BYOM (Bring Your Own Model)
┌──────────┐    ┌──────────────────────────────────┐    ┌──────────┐
│ 🎤 Mic   │───▶│ VoiceLive (solo audio pipeline)  │───▶│ 🔊 Audio │
│ (audio)  │    │ Azure Speech STT ──→ TU modelo ──│    │ output   │
└──────────┘    │ ──→ Azure Speech TTS             │    └──────────┘
                └──────────────────────────────────┘
                Audio: tarifa BYO (más barata que managed)
                LLM: TU tarifa de Azure OpenAI / Foundry / Partner
```

#### ¿Qué tarifas de audio se cobran?

VoiceLive BYO tiene **sus propias tarifas de audio** — más baratas que cualquier tier managed:

| Línea | BYO Audio Standard | Managed Basic (para comparar) | Δ Diferencia |
|---|---:|---:|---|
| **Input** | **$12.50** | $15.00 | BYO es **17% más barato** ✅ |
| **Output** | **$23.00** | $26.00 | BYO es **12% más barato** ✅ |
| Audio Custom Input | $36.00 | $39.00 | BYO es 8% más barato |
| Audio Custom Output | $47.00 | $50.00 | BYO es 6% más barato |

> ⚠️ **Diferencia clave:** BYO **NO cobra tokens de texto** — esos tokens los cobra tu deployment de Azure OpenAI directamente. Solo pagas el audio a VoiceLive.

> ⚠️ **BYO NO tiene native S2S** — siempre usa cascada (Azure Speech STT → tu modelo → Azure Speech TTS). No puedes usar `gpt-realtime` en modo nativo con BYO.

#### Cálculo: sesión de 5 min con BYOM

Misma sesión de 5 min, 5 turnos:

##### 🅳 BYOM + PAYG gpt-4o-mini ($0.15/$0.60 por 1M tokens)

| Línea | Tokens | Tarifa (/1M) | Costo |
|---|---:|---:|---:|
| BYO Audio Standard input | 4,000 | $12.50 | $0.05000 |
| BYO Audio Standard output | 3,000 | $23.00 | $0.06900 |
| LLM input (tu deployment) | 4,800 | $0.15 | $0.00072 |
| LLM output (tu deployment) | 400 | $0.60 | $0.00024 |
| **TOTAL** | | | **$0.120** |

##### 🅳 BYOM + PAYG gpt-4o ($2.50/$10.00 por 1M tokens)

| Línea | Tokens | Tarifa (/1M) | Costo |
|---|---:|---:|---:|
| BYO Audio Standard input | 4,000 | $12.50 | $0.05000 |
| BYO Audio Standard output | 3,000 | $23.00 | $0.06900 |
| LLM input (tu deployment) | 4,800 | $2.50 | $0.01200 |
| LLM output (tu deployment) | 400 | $10.00 | $0.00400 |
| **TOTAL** | | | **$0.135** |

##### 🅳 BYOM + PTU (gpt-4o, costo pre-pagado amortizado a ~$0 variable)

| Línea | Tokens | Tarifa (/1M) | Costo |
|---|---:|---:|---:|
| BYO Audio Standard input | 4,000 | $12.50 | $0.05000 |
| BYO Audio Standard output | 3,000 | $23.00 | $0.06900 |
| LLM (PTU pre-pagado) | — | $0 variable | $0.00000 |
| **TOTAL variable** | | | **$0.119** |

> 💰 Con PTU, el costo del LLM es un **cargo fijo mensual** — no se cobra por token. Esto hace que el costo variable por sesión sea solo el audio de VoiceLive.

#### 📊 Resumen comparativo actualizado: costo por sesión de 5 min (todos los modos)

| Modo | Costo/sesión | Notas |
|---|---:|---|
| 🅳 **BYOM + PTU** (gpt-4o) | **$0.119** | Solo audio BYO + PTU fijo mensual aparte |
| 🅳 **BYOM + PAYG** (gpt-4o-mini) | **$0.120** | El más barato sin costo fijo |
| 🅱 Lite Híbrido (phi4-mm-realtime) | $0.096 | Managed, zero config |
| 🅲 **Basic Full Native** (gpt-realtime-mini) | $0.115 | Managed, zero config |
| 🅱 **Basic Híbrido** (gpt-realtime-mini + Dragon HD) | $0.127 | ← **TU CONFIG ACTUAL** |
| 🅳 **BYOM + PAYG** (gpt-4o) | $0.135 | Modelo premium, sin PTU |
| 🅰 Basic Cascada (gpt-4.1-mini) | $0.143 | Managed, solo texto |
| 🅰 **Pro Cascada** (gpt-4.1) | $0.175 | El más barato en Pro |
| 🅱 Pro Híbrido (gpt-realtime) | $0.235 | Native in + Standard out |
| 🅲 **Pro Full Native** (gpt-realtime) | $0.334 | El más caro |

#### 💡 ¿Cuándo elegir BYOM vs Managed?

```
¿Tienes un modelo ya desplegado en Azure OpenAI / Foundry?
├── SÍ → ¿Necesitas audio nativo (S2S, modelo "oye" emoción)?
│   ├── SÍ  → ❌ BYO no soporta native → usa Managed (Basic/Pro)
│   └── NO  → ✅ BYO es más barato para audio ($12.50/$23 vs $15/$26)
│       ├── ¿Alto volumen (>50K sesiones/mes)?  → BYO + PTU
│       ├── ¿Volumen medio?                     → BYO + PAYG
│       └── ¿Bajo volumen / MVP?                → Managed (zero config)
└── NO → Managed (zero config, sin deploy propio)
    ├── ¿Presupuesto bajo?  → Lite (phi4-mm) o Basic (realtime-mini)
    └── ¿Calidad máxima?    → Pro (gpt-realtime)
```

> 📌 **Trade-off principal de BYOM:** Ahorras ~15% en audio y ganas flexibilidad de modelo (Claude, Grok, fine-tuned, etc.), **pero pierdes native S2S** (el modelo no "oye" emoción directamente).

---

### 📖 ¿Qué son los tipos de tokens de texto? (Text input fresh / cached / output)

Cuando VoiceLive factura, cobra **texto** además del audio. Esto confunde porque la conversación es de **voz** — ¿de dónde sale el texto? La respuesta: **internamente siempre hay texto involucrado**, sin importar el modo de procesamiento.

#### Los 3 tipos de tokens de texto

| Tipo de token | ¿Qué es? | Ejemplo | ¿Quién lo genera? |
|---|---|---|---|
| **Text input fresh** 🆕 | Texto **nuevo** que el modelo ve por primera vez en este turno | System prompt (1er turno), instrucciones nuevas, transcripción de lo que acabas de decir | Tú (system prompt) + Speech STT (transcribe tu voz) |
| **Text input cached** ♻️ | Texto que el modelo **ya vio** en turnos anteriores y se re-envía como contexto | System prompt (turnos 2-5), transcripciones de turnos previos | Re-envío automático del historial |
| **Text output** 📤 | Texto que el **modelo genera** como respuesta | La respuesta del asistente antes de convertirse en voz, function calls, metadata | El LLM (GPT-4o, GPT-realtime, etc.) |

#### ¿Por qué hay texto en una conversación de voz?

```
                          FLUJO INTERNO DE VOICELIVE
  ┌─────────────────────────────────────────────────────────────────┐
  │                                                                 │
  │  🎤 TU VOZ ──→ [Speech STT] ──→ 📝 TRANSCRIPCIÓN (texto)      │
  │                                     │                           │
  │                    ┌────────────────┘                           │
  │                    ▼                                            │
  │              ┌──────────┐                                       │
  │              │   LLM    │ ← Recibe TODO como texto:            │
  │              │(GPT-4o,  │   • System prompt (text input)       │
  │              │ realtime │   • Tu transcripción (text input)    │
  │              │  etc.)   │   • Historial previo (text cached)   │
  │              └────┬─────┘                                       │
  │                   │                                             │
  │                   ▼                                             │
  │              📝 RESPUESTA (texto) ← Esto es "Text output"     │
  │                   │                                             │
  │                   ▼                                             │
  │              [Speech TTS] ──→ 🔊 VOZ DEL ASISTENTE            │
  │                                                                 │
  └─────────────────────────────────────────────────────────────────┘
```

> **Nota sobre modo nativo (S2S):** Incluso en modo nativo donde el modelo recibe audio directamente, VoiceLive **genera texto internamente** (transcripciones, metadata, function calls). Por eso **siempre** hay cobro de tokens de texto, sin importar si usas cascada o nativo.

#### Ejemplo concreto: Turno 3 de una conversación

Imagina que ya van 2 turnos previos y el usuario dice *"¿Cuál es el horario de atención?"*:

| Lo que se envía al LLM en el Turno 3 | Tipo de token | ¿Por qué? |
|---|---|---|
| System prompt: "Eres un asistente de servicio al cliente de Contoso..." | **Text input cached** ♻️ | Ya se envió en turnos 1 y 2 |
| Transcripción turno 1: "Hola, necesito ayuda con mi cuenta" | **Text input cached** ♻️ | Ya se procesó en turno 1 |
| Respuesta turno 1: "¡Claro! ¿En qué puedo ayudarte?" | **Text input cached** ♻️ | Respuesta previa, ahora es contexto |
| Transcripción turno 2: "Quiero saber mi saldo" | **Text input cached** ♻️ | Ya se procesó en turno 2 |
| Respuesta turno 2: "Tu saldo es $1,500 pesos" | **Text input cached** ♻️ | Respuesta previa, ahora es contexto |
| Transcripción turno 3: **"¿Cuál es el horario de atención?"** | **Text input fresh** 🆕 | ¡Es nuevo! Primera vez que el modelo lo ve |
| → LLM genera: "Nuestro horario es lunes a viernes de 9am a 6pm" | **Text output** 📤 | Lo que el modelo responde |

#### Impacto en el costo

| Tipo | Precio Pro/1M tok | Precio Basic/1M tok | Precio Lite/1M tok | Notas |
|---|---:|---:|---:|---|
| **Text input fresh** 🆕 | $4.00 | $0.66 | $0.11 | Precio completo — texto nuevo |
| **Text input cached** ♻️ | $0.40 | $0.33 | $0.04 | **90-99% más barato** — texto repetido |
| **Text output** 📤 | $16.00 | $2.64 | $0.44 | El más caro — lo que genera el modelo |

> 💡 **¿Por qué importa?** En una conversación de 5 turnos, los tokens **cached** son ~3× más que los **fresh** porque cada turno re-envía todo el historial. Gracias al descuento de ~98%, el costo real de esos tokens repetidos es mínimo. Pero el **text output** ($16/1M en Pro) es el más caro por token — por eso respuestas largas del asistente cuestan más.

---

### Estimación oficial de tokens

| Familia de modelo | Tokens input por seg audio | Tokens output por seg audio |
|---|---:|---:|
| Azure OpenAI (gpt-4o, gpt-realtime, gpt-5…) | ~10 | ~20 |
| Phi | ~12.5 | ~20 |

### 🔄 Importante: contexto acumulativo por turno

**Cada turno re-procesa todo el contexto previo de la conversación** (system prompt + audio previo + transcripts), lo cual multiplica el conteo de tokens de input. Microsoft mitiga esto con **cached input** (~98% de descuento), pero aún así los tokens reales son **2-3× mayores** que el cálculo simple `segundos × 10`.

**Modelo realista (validado contra calculator oficial de Microsoft):**

Para una conversación de **5 min, 5 turnos (30s user + 30s assistant cada turno), system prompt 500 tokens**:

| Línea | Tokens fresh | Tokens cached | Notas |
|---|---:|---:|---|
| Audio input | ~4,000 | ~6,600 | Crece por turno (re-procesa audio previo) |
| Audio output | ~3,000 | — | 150 seg × 20 tok/seg |
| Text input | ~860 | ~2,540 | System prompt cacheado + transcripts |
| Text output | ~450 | — | Function calls, metadata |

> Estos números son lo que produce el [pricing calculator oficial](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/voice-live#pricing) para una conversación Pro con `gpt-realtime` native S2S.

**Supuestos para los cálculos:**
- Conversación de 5 min, 5 turnos (30s user + 30s assistant cada turno)
- System prompt: 500 tokens
- **Audio input** (acumulado entre turnos): ~4,000 fresh + ~6,600 cached
- **Audio output**: ~3,000 tokens (150s × 20)
- **Text input**: ~860 fresh + ~2,540 cached
- **Text output**: ~450 tokens
- TTS char count para Opción B: ~3,750 caracteres

### Opción B — Servicios separados

| Componente | Cálculo | Costo |
|---|---|---:|
| STT (Real-time Standard) | 2.5 min × ($1/hr ÷ 60) | $0.042 |
| LLM (gpt-4o-mini, ~4K tokens) | (~$0.15-$0.60 / 1M) | $0.002 |
| TTS Neural | 3,750 chars × ($15 / 1M) | $0.056 |
| **TOTAL (sin avatar)** | | **≈ $0.10** |

### Veredicto corregido (validado con calculator oficial)

| Solución | Costo 5 min (5 turnos) | Notas |
|---|---:|---|
| Pipeline manual (STT + LLM + TTS) | ~$0.10 | Construyes tú, sin barge-in nativo |
| VoiceLive Basic native + Azure Standard out | ~$0.13 | +30% sobre manual, gana en latencia + features |
| VoiceLive Pro native + Azure Standard out | ~$0.24 | ~2.4× manual, modelos flagship |
| **VoiceLive Pro native in + native out** | **~$0.37** | ~3.7× manual, máxima calidad voz/emoción |
| VoiceLive Lite + custom voice | ~$0.19 + custom hosting | Custom voice hosting ~$2,910/mes fijo |

> 💡 **Conclusión correcta:** VoiceLive Basic cuesta **~30% más** que pipeline manual ($0.13 vs $0.10) por la acumulación de contexto. La prima de Pro full-native es **~3.7×**. **Native audio in + out** (que es lo que da máxima naturalidad y emoción) es donde más se nota el costo.

> ⚠️ **Variables que afectan el costo real:**
> - **Número de turnos**: más turnos → más contexto acumulado por turno → más tokens audio re-procesados
> - **System prompt largo**: cada turno re-envía el prompt (cacheado, pero suma)
> - **Function calls**: cada call agrega tokens text in/out
> - **Pausas largas**: NO consumen tokens audio
> - **Cached input**: descuento ~98%, esencial en sesiones largas
> - **Native S2S in+out vs cascada**: native cuesta ~2-3× más pero da emoción nativa

---

## 🎯 Matriz de decisión

| Escenario | Recomendación |
|---|---|
| Voicebot conversacional premium (baja latencia, emoción, barge-in) | **VoiceLive Standard / Pro** |
| Alto volumen, sensible al costo, conversaciones simples | **STT + LLM + TTS separados** |
| Solo transcribir llamadas (sin respuesta hablada) | **Solo STT** — Batch a $0.18/hr es lo más barato |
| Quieres tu propio LLM hospedado, pero infra de voz integrada | **Voice Live BYO** ($12.50 in / $23 out) |
| Necesitas avatar visual sincronizado con voz | **VoiceLive + Avatar** o **TTS Avatar standalone** |
| App educativa con pronunciación/feedback | **STT + Pronunciation Assessment add-on** |
| Traducción simultánea en vivo | **Live Interpreter** o **Speech Translation** |
| Generar audio offline (audiolibros, IVR) | **TTS Neural batch** |

---

## 🆓 Free Tier (F0)

Útil para desarrollo y pruebas:

| Servicio | Cuota mensual gratuita |
|---|---|
| Speech to Text — Real-time (Standard o Custom) | 5 horas de audio |
| Speech to Text — Custom — Endpoint hosting | 1 modelo |
| Text to Speech — Neural | 0.5 millones de caracteres |
| Speech Translation | 5 horas de audio |

> Las 5 horas STT se comparten entre Standard y Custom. Batch no soportado en F0.
> VoiceLive **no aparece** explícitamente en F0 — verifica con la consola de Azure.

---

## 💰 Commitment Tiers (volumen alto)

Disponibles para STT y TTS (no para VoiceLive). Pre-pagas un compromiso mensual a cambio de mejor tarifa.

### Speech to Text — Standard
| Compromiso | Tarifa efectiva |
|---|---:|
| $1,600 / 2,000 hr | $0.80 / hr |
| $6,500 / 10,000 hr | $0.65 / hr |
| $25,000 / 50,000 hr | $0.50 / hr |

### Text to Speech — Neural
| Compromiso | Tarifa efectiva |
|---|---:|
| $960 / 80M chars | $12.00 / 1M chars |
| $3,900 / 400M chars | $9.75 / 1M chars |
| $15,000 / 2,000M chars | $7.50 / 1M chars |

> También hay Connected Container y Disconnected Container tiers con descuentos adicionales (~5%).
> HD voices, AOAI voices, Custom Neural Voice y Personal Voice **no** entran en commitment tiers.

---

## ⚠️ Notas importantes

1. **VoiceLive managed (Pro/Basic/Lite) es 100% managed** — en estos tiers, los modelos los hospeda Microsoft, no se conectan a tu Foundry con PTU. Tu PTU no se usa, no se descuenta, no se acredita. **Esto NO aplica a BYOM** (ver punto 2).
2. **Voice Live BYOM SÍ está disponible (GA)** y soporta **PTU** + modelos de partners (Claude, Grok). Modo `byom-foundry-anthropic-messages` está en preview. Ver [doc oficial BYOM](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/how-to-bring-your-own-model).
3. **Custom voice / custom STT** siempre suman cargos de training + hosting por encima del precio base.
4. **Cached input** (mucho más barato) aplica cuando el contexto/system prompt se reutiliza entre turnos — VoiceLive aprovecha esto automáticamente en sesiones largas.
5. **Token estimation** (de la doc oficial): Azure OpenAI models ≈ 10 tokens input / 20 tokens output por segundo de audio. Phi models ≈ 12.5 / 20 tokens por segundo.
6. **Avatar** está marcado N/A en varias filas de la tabla pública — probablemente en transición de precios; consulta directo con Azure.
7. **Region pricing** puede variar — los precios mostrados son USD East US. Usa el [pricing calculator](https://azure.microsoft.com/en-us/pricing/calculator/?service=cognitive-services) para tu región.
8. **Pricing oficial vigente desde 1 de julio de 2025** — verifica cambios recientes en la página oficial.

---

## 📚 Referencias

- [Azure Speech pricing (página oficial)](https://azure.microsoft.com/en-us/pricing/details/speech/)
- [Voice Live API docs](https://docs.microsoft.com/en-us/azure/ai-services/speech-service/voice-live)
- [Azure OpenAI pricing](https://azure.microsoft.com/en-us/pricing/details/cognitive-services/openai-service/)
- [Pricing Calculator](https://azure.microsoft.com/en-us/pricing/calculator/?service=cognitive-services)
- [Speech service quotas & limits](https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/speech-services-quotas-and-limits)
- [Commitment Tiers documentation](https://learn.microsoft.com/azure/cognitive-services/commitment-tier)

---


