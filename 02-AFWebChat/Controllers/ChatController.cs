using System.Text.Json;
using AFWebChat.Agents;
using AFWebChat.Models;
using AFWebChat.Services;
using Microsoft.AspNetCore.Mvc;

namespace AFWebChat.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly AgentOrchestrationService _orchestration;
    private readonly AgentRegistry _registry;
    private readonly IDocumentTextExtractor _documentExtractor;
    private readonly ReasoningSettings _reasoning;
    private readonly ILogger<ChatController> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ChatController(
        AgentOrchestrationService orchestration,
        AgentRegistry registry,
        IDocumentTextExtractor documentExtractor,
        ReasoningSettings reasoning,
        ILogger<ChatController> logger)
    {
        _orchestration = orchestration;
        _registry = registry;
        _documentExtractor = documentExtractor;
        _reasoning = reasoning;
        _logger = logger;
    }

    [HttpPost("stream")]
    public async Task StreamChat()
    {
        ChatRequest request;
        try
        {
            request = await BuildChatRequestAsync();
        }
        catch (ArgumentException ex)
        {
            Response.StatusCode = 400;
            await Response.WriteAsync(ex.Message);
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            await foreach (var evt in _orchestration.RunStreamingAsync(request))
            {
                var sseData = JsonSerializer.Serialize(evt, JsonOptions);
                await Response.WriteAsync($"event: {evt.EventType}\ndata: {sseData}\n\n");
                await Response.Body.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during streaming");
            var errorEvt = StreamEventService.Error("An error occurred during processing.");
            var errorData = JsonSerializer.Serialize(errorEvt, JsonOptions);
            await Response.WriteAsync($"event: error\ndata: {errorData}\n\n");

            var doneEvt = StreamEventService.Done();
            var doneData = JsonSerializer.Serialize(doneEvt, JsonOptions);
            await Response.WriteAsync($"event: done\ndata: {doneData}\n\n");
            await Response.Body.FlushAsync();
        }
    }

    [HttpPost("send")]
    public async Task<ActionResult<ChatResponse>> SendChat()
    {
        ChatRequest request;
        try
        {
            request = await BuildChatRequestAsync();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        try
        {
            var response = await _orchestration.RunAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during chat");
            return StatusCode(500, "An error occurred during processing.");
        }
    }

    [HttpPost("approve")]
    public ActionResult ApproveToolCall([FromBody] ApprovalResponse response)
    {
        _logger.LogInformation("Tool approval: {RequestId} = {Approved}", response.RequestId, response.Approved);
        // In a full implementation, this would signal the pending approval
        return Ok();
    }

    /// <summary>Devuelve el nivel de razonamiento global actual y las opciones válidas.</summary>
    [HttpGet("reasoning")]
    public ActionResult GetReasoning()
        => Ok(new
        {
            effort = _reasoning.Effort,
            summary = _reasoning.Summary,
            efforts = ReasoningSettings.AllowedEfforts,
            summaries = ReasoningSettings.AllowedSummaries
        });

    /// <summary>
    /// Cambia en caliente el nivel de razonamiento GLOBAL para todos los agentes.
    /// No requiere reiniciar: el cambio aplica en la siguiente petición.
    /// </summary>
    [HttpPost("reasoning")]
    public ActionResult SetReasoning([FromBody] ReasoningUpdate update)
    {
        if (!string.IsNullOrWhiteSpace(update.Effort))
        {
            var wanted = update.Effort.Trim().ToLowerInvariant();
            if (!ReasoningSettings.AllowedEfforts.Contains(wanted))
                return BadRequest($"Nivel de esfuerzo inválido: '{update.Effort}'. Válidos: {string.Join(", ", ReasoningSettings.AllowedEfforts)}.");
            _reasoning.Effort = wanted;
        }

        if (!string.IsNullOrWhiteSpace(update.Summary))
        {
            var wanted = update.Summary.Trim().ToLowerInvariant();
            if (!ReasoningSettings.AllowedSummaries.Contains(wanted))
                return BadRequest($"Resumen inválido: '{update.Summary}'. Válidos: {string.Join(", ", ReasoningSettings.AllowedSummaries)}.");
            _reasoning.Summary = wanted;
        }

        _logger.LogInformation("Nivel de razonamiento global actualizado: effort={Effort}, summary={Summary}", _reasoning.Effort, _reasoning.Summary);
        return Ok(new { effort = _reasoning.Effort, summary = _reasoning.Summary });
    }

    /// <summary>
    /// Builds a ChatRequest from either JSON body or multipart/form-data.
    /// When a document file is attached, extracts its text and prepends it to the user message.
    /// </summary>
    private async Task<ChatRequest> BuildChatRequestAsync()
    {
        string sessionId, message, agentName;
        string? workflowName = null, orchestrationName = null, customPattern = null;
        string[]? attachmentUrls = null, customAgents = null;
        IFormFile? document = null;

        var contentType = Request.ContentType ?? "";

        if (contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            var form = await Request.ReadFormAsync();
            sessionId = form["sessionId"].ToString();
            message = form["message"].ToString();
            agentName = form["agentName"].ToString();
            workflowName = form["workflowName"].ToString() is { Length: > 0 } wf ? wf : null;
            orchestrationName = form["orchestrationName"].ToString() is { Length: > 0 } orch ? orch : null;
            customPattern = form["customPattern"].ToString() is { Length: > 0 } cp ? cp : null;
            var customAgentValues = form["customAgents"];
            customAgents = customAgentValues.Count > 0
                ? customAgentValues.Where(v => v is not null).Select(v => v!).ToArray()
                : null;
            document = form.Files.GetFile("document");
        }
        else
        {
            var jsonRequest = await JsonSerializer.DeserializeAsync<ChatRequest>(Request.Body, JsonOptions);
            if (jsonRequest is null)
                throw new ArgumentException("Invalid request body.");

            sessionId = jsonRequest.SessionId;
            message = jsonRequest.Message;
            agentName = jsonRequest.AgentName;
            workflowName = jsonRequest.WorkflowName;
            orchestrationName = jsonRequest.OrchestrationName;
            attachmentUrls = jsonRequest.AttachmentUrls;
            customAgents = jsonRequest.CustomAgents;
            customPattern = jsonRequest.CustomPattern;
        }

        if (string.IsNullOrEmpty(message))
            throw new ArgumentException("Message is required.");

        if (message.Length > 10240)
            throw new ArgumentException("Message too long. Maximum 10KB.");

        // Extract text from attached document and prepend to message
        if (document is { Length: > 0 })
        {
            _logger.LogInformation("Document attached: {FileName} ({Size} bytes)", document.FileName, document.Length);

            using var stream = document.OpenReadStream();
            var extractedText = await _documentExtractor.ExtractTextAsync(stream, document.FileName, HttpContext.RequestAborted);

            message = $"[DOCUMENTO ADJUNTO: {document.FileName}]\n---\n{extractedText}\n---\n\n{message}";
        }

        return new ChatRequest(
            sessionId, message, agentName,
            workflowName, orchestrationName, attachmentUrls,
            customAgents, customPattern);
    }
}
