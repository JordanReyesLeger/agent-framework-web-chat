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
    private readonly ILogger<ChatController> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ChatController(
        AgentOrchestrationService orchestration,
        AgentRegistry registry,
        ILogger<ChatController> logger)
    {
        _orchestration = orchestration;
        _registry = registry;
        _logger = logger;
    }

    [HttpPost("stream")]
    public async Task StreamChat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrEmpty(request.Message))
        {
            Response.StatusCode = 400;
            return;
        }

        // Limit message size for security
        if (request.Message.Length > 10240)
        {
            Response.StatusCode = 400;
            await Response.WriteAsync("Message too long. Maximum 10KB.");
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
    public async Task<ActionResult<ChatResponse>> SendChat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrEmpty(request.Message))
            return BadRequest("Message is required.");

        if (request.Message.Length > 10240)
            return BadRequest("Message too long. Maximum 10KB.");

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
}
