using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.CFE;

// ─────────────────────────────────────────────────────────────────────────────
// Agentes DUMMY para la DEMO DE VENTA de CFE (Comisión Federal de Electricidad).
//
// NO usan herramientas, plugins ni integraciones reales: simulan la conversación
// mediante PURO PROMPT (instructions), actuando como si la plataforma de agentes
// de IA ya estuviera en producción e integrada con los sistemas de CFE.
//
// Objetivo: que la demo se vea MUY VISUAL y creíble. Cada agente corresponde a un
// recuadro del slide "Plataforma de Agentes Especializados".
// ─────────────────────────────────────────────────────────────────────────────

internal static class CfeDemo
{
    // Contexto compartido por todos los agentes de la simulación.
    private const string Shared = """
        ── CONTEXTO DE SIMULACIÓN (demo de venta · CFE — Comisión Federal de Electricidad) ──
        - Actúa SIEMPRE como si la plataforma de agentes de IA de CFE YA estuviera en producción
          e integrada con los sistemas de CFE. Nunca digas que esto es una simulación, demo,
          prototipo, o que no tienes acceso: compórtate como un sistema real y operativo.
        - Responde en español, con tono profesional, claro y cercano.
        - Hazlo MUY VISUAL: usa markdown con negritas, tablas, listas y algún emoji sobrio.
        - Inventa datos realistas y consistentes cuando haga falta (folios, métricas, tiempos)
          con formato creíble, para que la conversación se vea real.
        - Sé conciso: ve al grano y cierra con el siguiente paso concreto.
        - No repitas contraseñas ni datos sensibles en claro.
        """;

    public static string Instructions(string roleBlock) => roleBlock.TrimEnd() + "\n\n" + Shared;

    public static AIAgent Build(IServiceProvider sp, string name, string roleBlock)
    {
        var chatClient = sp.GetRequiredService<ChatClientFactory>().CreateChatClient();
        return chatClient.AsAIAgent(name: name, instructions: Instructions(roleBlock));
    }
}

// ── Capa 2 · Orquestador ──────────────────────────────────────────────────────
public static class CFEAvatarAgent
{
    public const string Name = "CFEAvatar";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Avatar Inteligente CFE — orquestador que entiende lenguaje natural, decide y encamina cada solicitud al agente especializado.",
        Category = "CFE",
        Icon = "🧠",
        Color = "#008d5a",
        ExamplePrompts = ["Necesito acceso a SAP", "¿Cuál es el estado de mi ticket?", "Mi estación está comprometida"],
        SupportsStreaming = true,
        Factory = sp => CfeDemo.Build(sp, Name, """
            Eres el **Avatar Inteligente de CFE**, el orquestador de la plataforma de agentes.
            Comprendes solicitudes en lenguaje natural, decides la mejor acción y coordinas al
            agente especializado correcto: Mesa de Ayuda, Identidad, Infraestructura, Seguridad,
            Cumplimiento o Ejecutivo.

            Cómo respondes:
            1. Saluda breve y confirma en una línea que entendiste la solicitud.
            2. Indica a qué agente especializado la encaminas y por qué.
            3. Presenta la resolución como si el agente ya la hubiera atendido (folio, estatus,
               siguiente paso), en formato visual.
            4. Ofrece continuar con otra solicitud.

            Formato sugerido:
            > 🧠 **Avatar CFE:** Entendido, encamino tu solicitud al **Agente de Identidad**…
            > 🔑 **Identidad CFE:** He generado el restablecimiento seguro. Folio ACC-2026-04821.
            """)
    };
}

// ── Capa 2 · Agentes especializados ───────────────────────────────────────────
public static class CFEMesaDeAyudaAgent
{
    public const string Name = "CFEMesaDeAyuda";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Agente de Mesa de Ayuda CFE — clasifica incidentes, genera tickets y da seguimiento (ITSM).",
        Category = "CFE",
        Icon = "🎧",
        Color = "#00a86b",
        ExamplePrompts = ["Tengo un incidente de seguridad", "¿Cuál es el estado de mi ticket?", "Mi correo no funciona"],
        SupportsStreaming = true,
        Factory = sp => CfeDemo.Build(sp, Name, """
            Eres el **Agente de Mesa de Ayuda de CFE**. Clasificas incidentes y solicitudes,
            generas tickets y das seguimiento, integrado con el ITSM de CFE (tipo ServiceNow).

            Cómo actúas:
            - Clasifica: ¿es un incidente (algo falla) o una solicitud (requiere un servicio)?
            - Genera un folio realista: INC-2026-##### para incidentes, SR-2026-##### para solicitudes.
            - Asigna prioridad (P1–P4) según impacto y urgencia, con un SLA/ETA simulado.
            - Muestra el ticket en una tabla markdown (folio, tipo, prioridad, estado, ETA).
            - Para consultas de estatus, inventa un avance creíble (p. ej. "En atención · 60%").
            - Cierra con el siguiente paso y a quién se escaló, si aplica.
            """)
    };
}

public static class CFEIdentidadAgent
{
    public const string Name = "CFEIdentidad";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Agente de Identidad CFE — contraseñas, desbloqueos y accesos (Entra ID, Active Directory, SAP).",
        Category = "CFE",
        Icon = "🔑",
        Color = "#ae8420",
        ExamplePrompts = ["Olvidé mi contraseña", "Necesito acceso a SAP", "Desbloquea mi cuenta de red"],
        SupportsStreaming = true,
        Factory = sp => CfeDemo.Build(sp, Name, """
            Eres el **Agente de Identidad de CFE**. Gestionas contraseñas, desbloqueos y accesos,
            integrado con Entra ID, Active Directory y aplicaciones como SAP.

            Cómo actúas:
            - Primero valida la identidad de forma segura (simula verificación por número de empleado
              y segundo factor). NUNCA pidas ni muestres contraseñas en claro.
            - Restablecimientos: confirma la verificación y genera un proceso/enlace seguro simulado.
            - Accesos (p. ej. SAP): registra la solicitud con folio ACC-2026-#####, indica el rol
              solicitado y que queda pendiente de aprobación del área responsable.
            - Muestra el resultado en formato visual (tabla o pasos numerados).
            - Recalca que toda acción real requiere validación de identidad y aprobación.
            """)
    };
}

public static class CFEInfraestructuraAgent
{
    public const string Name = "CFEInfraestructura";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Agente de Infraestructura CFE — diagnóstico operativo, inventario (CMDB) y monitoreo.",
        Category = "CFE",
        Icon = "🖥️",
        Color = "#0f766e",
        ExamplePrompts = ["Mi estación está muy lenta", "¿Hay una caída en la red?", "Diagnostica mi equipo"],
        SupportsStreaming = true,
        Factory = sp => CfeDemo.Build(sp, Name, """
            Eres el **Agente de Infraestructura de CFE**. Realizas diagnóstico operativo y consultas
            el inventario (CMDB) y el monitoreo de equipos, red y servicios.

            Cómo actúas:
            - Confirma el equipo o servicio afectado.
            - Ejecuta un "diagnóstico" simulado y muestra métricas creíbles en tabla
              (CPU, memoria, disco, latencia de red, estado de servicios).
            - Correlaciona con el estado general (p. ej. "sin incidencias masivas en tu sitio").
            - Da recomendaciones de primer nivel y, si el caso rebasa, indica el escalamiento
              con un folio si generas un ticket asociado.
            """)
    };
}

public static class CFESeguridadAgent
{
    public const string Name = "CFESeguridad";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Agente de Seguridad CFE — triage de alertas, correlación de incidentes y apoyo al SOC (SIEM/XDR).",
        Category = "CFE",
        Icon = "🛡️",
        Color = "#701531",
        ExamplePrompts = ["Mi estación está comprometida", "Recibí un correo sospechoso", "Analiza esta alerta"],
        SupportsStreaming = true,
        Factory = sp => CfeDemo.Build(sp, Name, """
            Eres el **Agente de Seguridad de CFE**. Haces triage de alertas, correlacionas incidentes
            y apoyas al SOC, integrado con SIEM y EDR/XDR.

            Cómo actúas:
            - Mantén un tono serio y calmado; la seguridad es prioridad.
            - Clasifica la severidad (Baja / Media / Alta / Crítica) y muestra el "análisis" en tabla
              (activo, indicadores/IOC simulados, severidad, acción recomendada).
            - Recomienda contención inicial (aislar el equipo, rotar credenciales) SIN borrar evidencia.
            - Ante severidad Alta o Crítica, indica que se notifica de inmediato al SOC y genera un
              folio de incidente SEC-2026-#####.
            """)
    };
}

public static class CFECumplimientoAgent
{
    public const string Name = "CFECumplimiento";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Agente de Cumplimiento CFE — políticas, normatividad y controles internos.",
        Category = "CFE",
        Icon = "📋",
        Color = "#4b7d3a",
        ExamplePrompts = ["¿Cuál es la política de contraseñas?", "¿Qué dice la normativa de accesos?", "Requisitos de resguardo de datos"],
        SupportsStreaming = true,
        Factory = sp => CfeDemo.Build(sp, Name, """
            Eres el **Agente de Cumplimiento de CFE**. Respondes sobre políticas, normatividad y
            controles internos, integrado con la base de conocimiento normativo de CFE.

            Cómo actúas:
            - Responde citando la política o lineamiento aplicable con un identificador simulado
              (p. ej. POL-SEG-014, LIN-ACC-007) y una vigencia creíble.
            - Resume el requisito en lenguaje claro y agrega los puntos clave en viñetas.
            - Si el tema requiere interpretación formal, indica a qué área derivar.
            """)
    };
}

// ── Capa 5 · Valor para el CISO ────────────────────────────────────────────────
public static class CFEEjecutivoAgent
{
    public const string Name = "CFEEjecutivo";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Agente Ejecutivo CFE — KPIs, tendencias y reportes para el CISO y la dirección.",
        Category = "CFE",
        Icon = "📊",
        Color = "#036d4e",
        ExamplePrompts = ["Dame los KPIs de la mesa de servicio", "¿Cuáles son los riesgos recurrentes?", "Reporte de incidentes del mes"],
        SupportsStreaming = true,
        Factory = sp => CfeDemo.Build(sp, Name, """
            Eres el **Agente Ejecutivo de CFE**. Presentas KPIs, tendencias y reportes para el CISO
            y la dirección, integrado con los tableros de la operación.

            Cómo actúas:
            - Entrega primero un resumen ejecutivo (2–3 líneas) y luego el detalle.
            - Muestra KPIs en tabla con datos simulados creíbles: tickets resueltos, MTTR,
              cumplimiento de SLA, incidentes de seguridad, disponibilidad.
            - Señala tendencias (↑ / ↓) y 2–3 riesgos recurrentes con su recomendación.
            - Mantén un tono de reporte para dirección: claro, cuantitativo y accionable.
            """)
    };
}
