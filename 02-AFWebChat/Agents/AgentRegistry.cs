using AFWebChat.Models;
using Microsoft.Agents.AI;

namespace AFWebChat.Agents;

public class AgentRegistry
{
    private readonly Dictionary<string, AgentDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AIAgent> _agents = new(StringComparer.OrdinalIgnoreCase);
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentRegistry> _logger;

    public AgentRegistry(IServiceProvider serviceProvider, ILogger<AgentRegistry> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void Register(AgentDefinition definition)
    {
        _definitions[definition.Name] = definition;
        _logger.LogInformation("Registered agent definition: {AgentName} ({Category})", definition.Name, definition.Category);
    }

    public AIAgent GetAgent(string name)
    {
        if (_agents.TryGetValue(name, out var cached))
            return cached;

        if (!_definitions.TryGetValue(name, out var definition))
            throw new KeyNotFoundException($"Agent '{name}' is not registered.");

        var agent = definition.Factory(_serviceProvider);
        _agents[name] = agent;
        _logger.LogInformation("Created agent instance: {AgentName}", name);
        return agent;
    }

    public AgentDefinition? GetDefinition(string name)
        => _definitions.TryGetValue(name, out var def) ? def : null;

    public List<AgentInfo> GetAllAgentInfos()
        => _definitions.Values.Select(d => d.ToAgentInfo()).ToList();

    public List<AgentInfo> GetAgentsByCategory(string category)
        => _definitions.Values
            .Where(d => d.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .Select(d => d.ToAgentInfo())
            .ToList();

    public bool HasAgent(string name) => _definitions.ContainsKey(name);

    public bool Unregister(string name)
    {
        _agents.Remove(name);
        var removed = _definitions.Remove(name);
        if (removed) _logger.LogInformation("Unregistered agent: {AgentName}", name);
        return removed;
    }
}
