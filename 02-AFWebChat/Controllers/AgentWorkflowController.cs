using System.Text.RegularExpressions;
using AFWebChat.Agents;
using AFWebChat.Models;
using AFWebChat.Orchestrations;
using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Workflows;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

namespace AFWebChat.Controllers;

[ApiController]
[Route("api/[controller]")]
public partial class AgentController : ControllerBase
{
    private readonly AgentRegistry _registry;
    private readonly ToolRegistry _toolRegistry;
    private readonly ILogger<AgentController> _logger;

    public AgentController(AgentRegistry registry, ToolRegistry toolRegistry, ILogger<AgentController> logger)
    {
        _registry = registry;
        _toolRegistry = toolRegistry;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<List<AgentInfo>> GetAll()
    {
        return Ok(_registry.GetAllAgentInfos());
    }

    [HttpGet("{name}")]
    public ActionResult<AgentInfo> Get(string name)
    {
        var def = _registry.GetDefinition(name);
        return def is not null ? Ok(def.ToAgentInfo()) : NotFound();
    }

    [HttpGet("tools")]
    public ActionResult<string[]> GetAvailableTools()
    {
        return Ok(_toolRegistry.GetToolSetNames());
    }

    [HttpPost]
    public ActionResult<AgentInfo> CreateAgent([FromBody] CreateAgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 50)
            return BadRequest("Agent name is required and must be ≤ 50 characters.");

        if (!SafeNameRegex().IsMatch(request.Name))
            return BadRequest("Agent name must contain only letters, numbers, spaces, and hyphens.");

        if (_registry.HasAgent(request.Name))
            return Conflict($"Agent '{request.Name}' already exists.");

        if (string.IsNullOrWhiteSpace(request.Instructions) || request.Instructions.Length > 8192)
            return BadRequest("Instructions are required and must be ≤ 8192 characters.");

        var toolNames = request.Tools ?? [];
        var definition = new AgentDefinition
        {
            Name = request.Name,
            Description = request.Description ?? "Custom agent",
            Category = request.Category ?? "Custom",
            Icon = request.Icon ?? "🤖",
            Color = request.Color ?? "#0078d4",
            Tools = toolNames,
            ExamplePrompts = request.ExamplePrompts ?? [],
            SupportsStreaming = true,
            Factory = sp =>
            {
                var factory = sp.GetRequiredService<ChatClientFactory>();
                var chatClient = factory.CreateChatClient();
                var tools = toolNames.Length > 0
                    ? sp.GetRequiredService<ToolRegistry>().GetTools(toolNames)
                    : [];

                return chatClient.AsAIAgent(
                    name: request.Name,
                    instructions: request.Instructions,
                    tools: tools);
            }
        };

        _registry.Register(definition);
        _logger.LogInformation("Created custom agent: {AgentName}", request.Name);

        return Ok(definition.ToAgentInfo());
    }

    [HttpDelete("{name}")]
    public ActionResult DeleteAgent(string name)
    {
        var def = _registry.GetDefinition(name);
        if (def is null)
            return NotFound();

        if (def.Category != "Custom")
            return BadRequest("Only custom agents can be deleted.");

        _registry.Unregister(name);
        _logger.LogInformation("Deleted custom agent: {AgentName}", name);
        return NoContent();
    }

    [GeneratedRegex(@"^[\w\s\-]+$")]
    private static partial Regex SafeNameRegex();
}

[ApiController]
[Route("api/[controller]")]
public class WorkflowController : ControllerBase
{
    private readonly WorkflowFactory _workflowFactory;

    public WorkflowController(WorkflowFactory workflowFactory)
    {
        _workflowFactory = workflowFactory;
    }

    [HttpGet]
    public ActionResult<List<WorkflowInfo>> GetAll()
    {
        return Ok(_workflowFactory.GetAllWorkflows());
    }

    [HttpGet("{name}")]
    public ActionResult<WorkflowInfo> Get(string name)
    {
        var wf = _workflowFactory.GetWorkflow(name);
        return wf is not null ? Ok(wf) : NotFound();
    }
}

[ApiController]
[Route("api/[controller]")]
public class OrchestrationController : ControllerBase
{
    private readonly OrchestrationFactory _orchestrationFactory;

    public OrchestrationController(OrchestrationFactory orchestrationFactory)
    {
        _orchestrationFactory = orchestrationFactory;
    }

    [HttpGet]
    public ActionResult<List<OrchestrationInfo>> GetAll()
    {
        return Ok(_orchestrationFactory.GetAllOrchestrations());
    }

    [HttpGet("{name}")]
    public ActionResult<OrchestrationInfo> Get(string name)
    {
        var orch = _orchestrationFactory.GetOrchestration(name);
        return orch is not null ? Ok(orch) : NotFound();
    }
}
