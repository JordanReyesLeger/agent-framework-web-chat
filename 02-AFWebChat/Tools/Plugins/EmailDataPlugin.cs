using System.ComponentModel;

namespace AFWebChat.Tools.Plugins;

/// <summary>
/// Simulated email database plugin for demo purposes.
/// Provides realistic corporate email data for the multi-agent orchestration demo.
/// </summary>
public class EmailDataPlugin
{
    private static readonly List<EmailRecord> _emails =
    [
        // ── Urgentes ──────────────────────────────────────────
        new("EM-001", "Carlos Mendoza", "carlos.mendoza@acme.com", "Caída total del sistema ERP",
            "El sistema ERP dejó de funcionar desde las 6:00 AM. Ningún usuario puede ingresar. " +
            "Los pedidos del día no se están procesando y los clientes están llamando para quejarse. " +
            "URGENTE: necesitamos una solución INMEDIATA, estamos perdiendo ventas.",
            DateTime.Now.AddHours(-2), "Soporte", "abierto", 5),

        new("EM-002", "Laura Vega", "laura.vega@acme.com", "Facturación incorrecta a cliente VIP",
            "El cliente GlobalTech recibió una factura por $150,000 cuando el monto correcto es $15,000. " +
            "Ya están amenazando con cancelar el contrato. Necesitamos corregir esto HOY. " +
            "El director comercial está furioso.",
            DateTime.Now.AddHours(-5), "Finanzas", "abierto", 5),

        new("EM-003", "Roberto Silva", "roberto.silva@acme.com", "Brecha de seguridad detectada",
            "El equipo de ciberseguridad detectó accesos no autorizados al servidor de base de datos. " +
            "Se identificaron 3 IPs sospechosas accediendo a datos de clientes desde las 3 AM. " +
            "Necesitamos contener el incidente URGENTE y notificar al oficial de cumplimiento.",
            DateTime.Now.AddHours(-1), "Seguridad", "abierto", 5),

        // ── Alta prioridad ────────────────────────────────────
        new("EM-004", "Ana García", "ana.garcia@acme.com", "Retraso en entrega de proyecto Delta",
            "El proyecto Delta lleva 2 semanas de retraso. El cliente solicita una reunión de emergencia " +
            "para revisar el cronograma. Si no entregamos este viernes, aplican penalizaciones del 10%.",
            DateTime.Now.AddDays(-1), "Proyectos", "abierto", 4),

        new("EM-005", "Miguel Torres", "miguel.torres@acme.com", "Error en cálculo de nómina",
            "Se detectó un error en el cálculo de nómina de 45 empleados. Los depósitos salen mañana " +
            "y varios empleados recibirían montos incorrectos. Necesitamos corregir antes de las 5 PM.",
            DateTime.Now.AddHours(-8), "RRHH", "en_progreso", 4),

        // ── Media prioridad ───────────────────────────────────
        new("EM-006", "Patricia Ruiz", "patricia.ruiz@acme.com", "Solicitud de nuevo módulo de reportes",
            "El área de ventas necesita un módulo de reportes que muestre tendencias mensuales. " +
            "No es urgente pero lo necesitamos para la junta de directivos del próximo mes.",
            DateTime.Now.AddDays(-3), "Ventas", "abierto", 3),

        new("EM-007", "Fernando López", "fernando.lopez@acme.com", "Actualización de licencias Office 365",
            "Tenemos 20 licencias de Office 365 que vencen el próximo mes. Necesitamos renovar " +
            "y además agregar 5 licencias nuevas para los nuevos ingresos.",
            DateTime.Now.AddDays(-5), "IT", "abierto", 3),

        new("EM-008", "Diana Martínez", "diana.martinez@acme.com", "Capacitación equipo nuevo CRM",
            "El nuevo CRM se implementa en 3 semanas. Necesitamos programar la capacitación para " +
            "los 30 usuarios del área comercial. ¿Pueden coordinar las sesiones?",
            DateTime.Now.AddDays(-2), "Capacitación", "abierto", 3),

        // ── Baja prioridad ────────────────────────────────────
        new("EM-009", "Jorge Hernández", "jorge.hernandez@acme.com", "Sugerencia de mejora en intranet",
            "Sería útil agregar un buscador más potente en la intranet. Actualmente es difícil " +
            "encontrar documentos de políticas internas. Solo es una sugerencia para cuando tengan tiempo.",
            DateTime.Now.AddDays(-7), "IT", "abierto", 2),

        new("EM-010", "Sofía Ramírez", "sofia.ramirez@acme.com", "Invitación a evento de networking",
            "Los invitamos al evento de networking tecnológico el próximo 15 de mayo. " +
            "Habrá ponencias sobre IA y transformación digital. Confirmen asistencia.",
            DateTime.Now.AddDays(-4), "General", "abierto", 1),

        // ── Correos resueltos (históricos) ────────────────────
        new("EM-011", "Carlos Mendoza", "carlos.mendoza@acme.com", "RE: Caída del servidor web",
            "El servidor web se cayó ayer pero ya lo restauramos. Fue un problema de memoria. " +
            "Incrementamos la RAM de 16GB a 32GB. Monitorearemos las próximas 48 horas.",
            DateTime.Now.AddDays(-15), "Soporte", "resuelto", 5),

        new("EM-012", "Laura Vega", "laura.vega@acme.com", "RE: Facturación duplicada febrero",
            "Corregimos la facturación duplicada del mes de febrero. Se emitieron notas de crédito " +
            "a los 12 clientes afectados y se implementó una validación para evitar duplicados.",
            DateTime.Now.AddDays(-30), "Finanzas", "resuelto", 4),
    ];

    [Description("Searches emails by keyword in subject or body. Returns matching emails with their details.")]
    public Task<string> SearchEmails(
        [Description("The keyword or phrase to search for in email subjects and bodies")] string keyword)
    {
        var kw = keyword.ToLowerInvariant();
        var matches = _emails
            .Where(e => e.Subject.Contains(kw, StringComparison.OrdinalIgnoreCase)
                     || e.Body.Contains(kw, StringComparison.OrdinalIgnoreCase)
                     || e.Department.Contains(kw, StringComparison.OrdinalIgnoreCase)
                     || e.Sender.Contains(kw, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.Priority)
            .ThenByDescending(e => e.Date)
            .ToList();

        if (matches.Count == 0)
            return Task.FromResult($"No se encontraron correos con el término '{keyword}'.");

        var lines = new List<string> { $"Se encontraron {matches.Count} correos con '{keyword}':\n" };
        foreach (var e in matches)
        {
            lines.Add($"📧 [{e.Id}] {e.Subject}");
            lines.Add($"   De: {e.Sender} ({e.Email})");
            lines.Add($"   Fecha: {e.Date:dd/MM/yyyy HH:mm}");
            lines.Add($"   Departamento: {e.Department} | Estado: {e.Status} | Prioridad: {e.Priority}/5");
            lines.Add($"   Contenido: {e.Body}");
            lines.Add("");
        }

        return Task.FromResult(string.Join("\n", lines));
    }

    [Description("Lists all open (unresolved) emails, optionally filtered by department.")]
    public Task<string> ListOpenEmails(
        [Description("Optional department filter (e.g., 'Soporte', 'Finanzas', 'IT'). Leave empty for all.")] string? department = null)
    {
        var open = _emails.Where(e => e.Status != "resuelto");
        if (!string.IsNullOrWhiteSpace(department))
            open = open.Where(e => e.Department.Equals(department, StringComparison.OrdinalIgnoreCase));

        var list = open.OrderByDescending(e => e.Priority).ThenByDescending(e => e.Date).ToList();
        if (list.Count == 0)
            return Task.FromResult("No hay correos abiertos" + (department != null ? $" en {department}" : "") + ".");

        var lines = new List<string> { $"📬 {list.Count} correos abiertos" + (department != null ? $" en {department}" : "") + ":\n" };
        foreach (var e in list)
        {
            var urgency = e.Priority >= 4 ? "🔴" : e.Priority == 3 ? "🟡" : "🟢";
            lines.Add($"{urgency} [{e.Id}] {e.Subject} — de {e.Sender} ({e.Date:dd/MM HH:mm}) P{e.Priority}");
        }

        return Task.FromResult(string.Join("\n", lines));
    }

    [Description("Gets the full details of a specific email by its ID (e.g., 'EM-001').")]
    public Task<string> GetEmailById(
        [Description("The email ID to retrieve (e.g., 'EM-001')")] string emailId)
    {
        var email = _emails.FirstOrDefault(e => e.Id.Equals(emailId, StringComparison.OrdinalIgnoreCase));
        if (email is null)
            return Task.FromResult($"No se encontró el correo con ID '{emailId}'.");

        return Task.FromResult(
            $"📧 Correo {email.Id}\n" +
            $"De: {email.Sender} ({email.Email})\n" +
            $"Asunto: {email.Subject}\n" +
            $"Fecha: {email.Date:dd/MM/yyyy HH:mm}\n" +
            $"Departamento: {email.Department}\n" +
            $"Estado: {email.Status}\n" +
            $"Prioridad: {email.Priority}/5\n\n" +
            $"Contenido:\n{email.Body}");
    }

    [Description("Gets email statistics: total count, open count, and breakdown by priority and department.")]
    public Task<string> GetEmailStats()
    {
        var total = _emails.Count;
        var open = _emails.Count(e => e.Status != "resuelto");
        var urgent = _emails.Count(e => e.Priority >= 4 && e.Status != "resuelto");

        var byDept = _emails.Where(e => e.Status != "resuelto")
            .GroupBy(e => e.Department)
            .Select(g => $"  {g.Key}: {g.Count()} correos")
            .ToList();

        return Task.FromResult(
            $"📊 Estadísticas de Correo:\n" +
            $"Total: {total} | Abiertos: {open} | Urgentes (P4-P5): {urgent}\n\n" +
            $"Por departamento:\n{string.Join("\n", byDept)}");
    }

    private sealed record EmailRecord(
        string Id, string Sender, string Email, string Subject,
        string Body, DateTime Date, string Department, string Status, int Priority);
}
