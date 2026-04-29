using System.Reflection;
using Microsoft.Extensions.AI;

namespace AFWebChat.Tools;

public static class AIFunctionFactoryExtensions
{
    /// <summary>
    /// Creates AITool instances for all public static methods in a plugin class.
    /// </summary>
    public static IList<AITool> CreateFromStatic<T>()
    {
        return typeof(T)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => (AITool)AIFunctionFactory.Create(m, target: null))
            .ToList();
    }

    /// <summary>
    /// Creates AITool instances for all public instance methods in a plugin instance.
    /// </summary>
    public static IList<AITool> CreateFromInstance<T>(T instance) where T : class
    {
        return typeof(T)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => (AITool)AIFunctionFactory.Create(m, instance))
            .ToList();
    }
}
