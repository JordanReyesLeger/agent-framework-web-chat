using Microsoft.Extensions.AI;

namespace AFWebChat.Tools;

public class ToolRegistry
{
    private readonly Dictionary<string, AIFunction[]> _toolSets = new();

    public void RegisterToolSet(string name, AIFunction[] tools)
        => _toolSets[name] = tools;

    public AIFunction[] GetTools(params string[] setNames)
        => setNames.SelectMany(n => _toolSets.TryGetValue(n, out var tools) ? tools : []).ToArray();

    public IReadOnlyDictionary<string, AIFunction[]> GetAllToolSets()
        => _toolSets;

    public string[] GetToolSetNames() => _toolSets.Keys.ToArray();
}
