using System.Reflection;

var nugetBase = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".nuget", "packages");

var dllPaths = new[] {
    Path.Combine(nugetBase, "microsoft.agents.ai", "1.0.0-rc4", "lib", "net9.0", "Microsoft.Agents.AI.dll"),
    Path.Combine(nugetBase, "microsoft.agents.ai.abstractions", "1.0.0-rc4", "lib", "net9.0", "Microsoft.Agents.AI.Abstractions.dll"),
};

var assemblies = new List<Assembly>();
foreach (var p in dllPaths)
{
    if (File.Exists(p))
    {
        assemblies.Add(Assembly.LoadFrom(p));
        Console.WriteLine($"Loaded: {p}");
    }
    else Console.WriteLine($"NOT FOUND: {p}");
}

// Also check for Workflows
var wfPath = Path.Combine(nugetBase, "microsoft.agents.ai.workflows", "1.0.0-rc4", "lib", "net9.0", "Microsoft.Agents.AI.Workflows.dll");
if (File.Exists(wfPath))
{
    assemblies.Add(Assembly.LoadFrom(wfPath));
    Console.WriteLine($"Loaded: {wfPath}");
}

var targetTypes = new[] {
    "AgentResponse", "AgentResponse`1", "AgentResponseUpdate", "AgentResponseItem",
    "ChatClientAgentOptions", "ChatClientAgentRunOptions",
    "AgentSession", "AgentSessionStateBag",
    "AIContextProvider", "AIAgent", "ChatClientAgent",
    "AIContext", "InvokingContext",
    // Workflow types
    "Executor", "Executor`1", "Executor`2", "WorkflowBuilder",
};

foreach (var asm in assemblies)
{
    Type[] types;
    try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }
    
    foreach (var type in types.OrderBy(t => t!.FullName))
    {
        if (type == null) continue;
        var shortName = type.Name;
        if (!targetTypes.Any(t => shortName == t || shortName.StartsWith(t.Replace("`1", "`").Replace("`2", "`")))) continue;
        
        Console.WriteLine($"\n=== {type.FullName} ===");
        Console.WriteLine($"  IsAbstract={type.IsAbstract} IsInterface={type.IsInterface} IsSealed={type.IsSealed}");
        if (type.BaseType != null) Console.WriteLine($"  Base: {type.BaseType.FullName}");
        foreach (var iface in type.GetInterfaces()) Console.WriteLine($"  Implements: {iface.FullName}");
        
        // Constructors (public + protected)
        foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(c => c.IsPublic || c.IsFamily))
        {
            var access = ctor.IsPublic ? "public" : "protected";
            var ps = string.Join(", ", ctor.GetParameters().Select(p => $"{FormatType(p.ParameterType)} {p.Name}"));
            Console.WriteLine($"  {access} ctor({ps})");
        }
        
        // Properties (public + protected, declared only)
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            var getter = prop.GetGetMethod(true);
            var setter = prop.GetSetMethod(true);
            if (getter != null && !getter.IsPublic && !getter.IsFamily) getter = null;
            if (setter != null && !setter.IsPublic && !setter.IsFamily) setter = null;
            if (getter == null && setter == null) continue;
            
            var access = (getter?.IsPublic == true || setter?.IsPublic == true) ? "public" : "protected";
            var accessors = (getter != null ? "get " : "") + (setter != null ? "set" : "");
            Console.WriteLine($"  {access} prop {FormatType(prop.PropertyType)} {prop.Name} {{ {accessors.Trim()} }}");
        }
        
        // Methods (public + protected, excluding property accessors)
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && (m.IsPublic || m.IsFamily)))
        {
            var access = method.IsPublic ? "public" : "protected";
            var stat = method.IsStatic ? "static " : "";
            var virt = method.IsVirtual ? "virtual " : "";
            var ps = string.Join(", ", method.GetParameters().Select(p => $"{FormatType(p.ParameterType)} {p.Name}"));
            var genArgs = method.IsGenericMethod ? $"<{string.Join(",", method.GetGenericArguments().Select(g => g.Name))}>" : "";
            Console.WriteLine($"  {access} {stat}{virt}method {FormatType(method.ReturnType)} {method.Name}{genArgs}({ps})");
        }
    }
}

static string FormatType(Type t)
{
    if (t.IsGenericType)
    {
        var name = t.Name.Split('`')[0];
        var args = string.Join(", ", t.GetGenericArguments().Select(FormatType));
        return $"{name}<{args}>";
    }
    return t.Name;
}
