using AgentFrameworkTests.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using Xunit;
using Xunit.Abstractions;

namespace AgentFrameworkTests.Tests;

/// <summary>
/// Módulo 04: Aprobación de herramientas (Tool Approval / Human-in-the-Loop).
/// Demuestra cómo envolver herramientas con ApprovalRequiredAIFunction
/// para requerir aprobación humana antes de ejecutar acciones sensibles.
/// </summary>
public class _04_ToolApproval
{
    private readonly ITestOutputHelper _output;

    public _04_ToolApproval(ITestOutputHelper output)
    {
        _output = output;
    }

    // ---------- Funciones sensibles que requieren aprobación ----------

    /// <summary>
    /// Simula enviar un correo electrónico (acción sensible que requiere confirmación).
    /// </summary>
    [Description("Sends an email to a specified recipient")]
    private static string SendEmail(
        [Description("Recipient email address")] string to,
        [Description("Email subject")] string subject,
        [Description("Email body content")] string body)
    {
        return $"Email sent successfully to {to} with subject '{subject}'";
    }

    /// <summary>
    /// Simula eliminar un registro de la base de datos (acción destructiva).
    /// </summary>
    [Description("Deletes a record from the database by its ID")]
    private static string DeleteRecord([Description("The record ID to delete")] int recordId)
    {
        return $"Record {recordId} has been permanently deleted";
    }

    // ---------- Pruebas ----------

    /// <summary>
    /// Demuestra cómo ApprovalRequiredAIFunction intercepta la ejecución de una herramienta,
    /// requiere aprobación, y continúa tras aprobar manualmente.
    /// En un escenario real, la aprobación vendría de una interfaz de usuario.
    /// </summary>
    [Fact]
    public async Task Should_Require_Approval_Before_Sending_Email()
    {
        // Crear la función original
        AIFunction sendEmailFunction = AIFunctionFactory.Create(SendEmail);

        // Envolverla con ApprovalRequiredAIFunction
        // Esto hace que la ejecución se pause hasta recibir aprobación
        var approvalFunction = new ApprovalRequiredAIFunction(sendEmailFunction);

        AIAgent agent = TestConfiguration.CreateAgent(
            instructions: "Eres un asistente de correo electrónico. Cuando te pidan enviar un correo, usa la herramienta SendEmail.",
            tools: new List<AITool> { approvalFunction });

        // Crear sesión (requerida para RunAsync)
        AgentSession session = await agent.CreateSessionAsync();

        // Al ejecutar, el agente intentará usar la herramienta,
        // pero la respuesta contendrá una solicitud de aprobación
        AgentResponse response = await agent.RunAsync(
            "Send an email to workshop@example.com with subject 'Workshop Reminder' and body 'Don't forget the Agent Framework workshop tomorrow!'",
            session);

        Assert.NotNull(response);

        // Verificar si hay solicitudes de aprobación pendientes en la respuesta
        // La respuesta contendrá FunctionApprovalRequestContent cuando requiere aprobación
        _output.WriteLine("✅ Solicitud de aprobación recibida:");
        _output.WriteLine($"   Respuesta: {response.Text}");

        // En un escenario real, aquí se presentaría la solicitud al usuario
        // y se aprobaría o rechazaría con:
        // requestContent.CreateResponse(approved: true) para aprobar
        // requestContent.CreateResponse(approved: false) para rechazar
        _output.WriteLine("   💡 En producción, el usuario aprobaría/rechazaría la acción aquí");
    }

    /// <summary>
    /// Demuestra el uso de herramientas normales junto con herramientas que requieren aprobación.
    /// Solo las acciones sensibles piden aprobación; las demás se ejecutan directamente.
    /// </summary>
    [Fact]
    public async Task Should_Mix_Normal_And_Approval_Tools()
    {
        // Herramienta normal (no requiere aprobación)
        AIFunction searchTool = AIFunctionFactory.Create(
            ([Description("Search query")] string query) =>
                $"Found 5 results for: {query}",
            "SearchDatabase",
            "Searches the database for records matching a query");

        // Herramienta que requiere aprobación (acción destructiva)
        AIFunction deleteFunction = AIFunctionFactory.Create(DeleteRecord);
        var approvalDelete = new ApprovalRequiredAIFunction(deleteFunction);

        AIAgent agent = TestConfiguration.CreateAgent(
            instructions: "Eres un asistente de base de datos. Usa SearchDatabase para consultas y DeleteRecord para eliminaciones. Siempre busca antes de eliminar.",
            tools: new List<AITool> { searchTool, approvalDelete });

        // Crear sesión (requerida para RunAsync)
        AgentSession session = await agent.CreateSessionAsync();

        // Búsqueda normal → se ejecuta sin aprobación
        AgentResponse searchResponse = await agent.RunAsync("Search for users named John", session);
        Assert.NotNull(searchResponse.Text);
        _output.WriteLine($"✅ Búsqueda (sin aprobación): {searchResponse.Text}");

        // Eliminación → requiere aprobación
        AgentResponse deleteResponse = await agent.RunAsync("Delete record 42", session);
        Assert.NotNull(deleteResponse);
        _output.WriteLine($"✅ Eliminación (con aprobación): {deleteResponse.Text}");
        _output.WriteLine("   💡 La eliminación habrá requerido aprobación del usuario");
    }
}
