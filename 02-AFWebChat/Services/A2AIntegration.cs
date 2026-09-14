using A2A;
using AFWebChat.Agents;
using AFWebChat.Agents.A2A;
using AFWebChat.Models;
using Microsoft.Agents.AI;

namespace AFWebChat.Services;

/// <summary>
/// Integración del protocolo A2A (Agent2Agent) en AF-WebChat.
/// <para>
/// Lado <b>servidor</b>: publica agentes locales en <c>{BasePath}/{Agente}</c> para que cualquier
/// cliente A2A (otro framework, otro lenguaje, otra nube) pueda invocarlos.
/// </para>
/// <para>
/// Lado <b>cliente</b>: registra agentes A2A remotos en el <see cref="AgentRegistry"/> para que
/// aparezcan en el catálogo del chat como un tipo más de agente.
/// </para>
/// </summary>
public static class A2AIntegration
{
    public static A2ASettings ReadSettings(IConfiguration configuration)
        => configuration.GetSection(A2ASettings.SectionName).Get<A2ASettings>() ?? new A2ASettings();

    /// <summary>Registra los agentes A2A remotos declarados en configuración como agentes del chat.</summary>
    public static List<A2ARemoteAgentInfo> RegisterRemoteAgents(
        AgentRegistry registry, A2ASettings settings, ILogger logger)
    {
        var result = new List<A2ARemoteAgentInfo>();
        if (!settings.Enabled)
        {
            return result;
        }

        foreach (var remote in settings.RemoteAgents)
        {
            try
            {
                var definition = A2ARemoteAgent.CreateDefinition(remote);
                registry.Register(definition);
                result.Add(new A2ARemoteAgentInfo(definition.Name, definition.Description, remote.Url, Registered: true));
                logger.LogInformation("A2A: agente remoto '{Name}' registrado ({Url})", definition.Name, remote.Url);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "A2A: no se pudo registrar el agente remoto '{Name}'", remote.Name);
                result.Add(new A2ARemoteAgentInfo(remote.Name, remote.Description ?? "", remote.Url, Registered: false));
            }
        }

        return result;
    }

    /// <summary>Publica los agentes locales configurados como endpoints A2A.</summary>
    public static List<A2AExposedAgentInfo> MapExposedAgents(WebApplication app, A2ASettings settings)
    {
        var exposed = new List<A2AExposedAgentInfo>();
        if (!settings.Enabled)
        {
            return exposed;
        }

        var registry = app.Services.GetRequiredService<AgentRegistry>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AFWebChat.A2A");
        var basePath = "/" + settings.BasePath.Trim('/');

        foreach (var name in settings.ExposedAgents.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var definition = registry.GetDefinition(name);
            if (definition is null)
            {
                logger.LogWarning("A2A: el agente '{Name}' está en A2A:ExposedAgents pero no está registrado; se omite.", name);
                continue;
            }

            // Republicar un agente A2A remoto crearía un bucle de proxy sin valor para la demo.
            if (string.Equals(definition.Category, A2ARemoteAgent.CategoryName, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("A2A: el agente '{Name}' ya es remoto A2A; no se vuelve a publicar.", name);
                continue;
            }

            AIAgent agent;
            try
            {
                agent = registry.GetAgent(name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "A2A: no se pudo instanciar el agente '{Name}' para publicarlo vía A2A.", name);
                continue;
            }

            var path = $"{basePath}/{name}";
            var absoluteUrl = CombineUrl(settings.PublicBaseUrl, path);

            app.MapA2A(agent, path, BuildAgentCard(definition, absoluteUrl));

            exposed.Add(new A2AExposedAgentInfo(
                definition.Name,
                definition.Description,
                path,
                absoluteUrl,
                $"{absoluteUrl}/v1/card"));

            logger.LogInformation("A2A: agente '{Name}' publicado en {Path}", name, path);
        }

        return exposed;
    }

    private static AgentCard BuildAgentCard(AgentDefinition definition, string absoluteUrl)
    {
        var tags = new List<string> { definition.Category };
        tags.AddRange(definition.Tools);

        return new AgentCard
        {
            Name = definition.Name,
            Description = definition.Description,
            Url = absoluteUrl,
            Version = "1.0.0",
            Capabilities = new AgentCapabilities { Streaming = definition.SupportsStreaming },
            DefaultInputModes = ["text"],
            DefaultOutputModes = ["text"],
            Skills =
            [
                new A2A.AgentSkill
                {
                    Id = definition.Name.ToLowerInvariant(),
                    Name = definition.Name,
                    Description = definition.Description,
                    Tags = tags,
                    Examples = [.. definition.ExamplePrompts]
                }
            ]
        };
    }

    private static string CombineUrl(string baseUrl, string path)
        => $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
}

/// <summary>
/// Directorio A2A resuelto en el arranque: qué agentes locales quedaron publicados y qué agentes
/// remotos quedaron registrados. Lo consume la API de descubrimiento y la UI de demos.
/// </summary>
public class A2ADirectory
{
    public A2ASettings Settings { get; set; } = new();
    public IReadOnlyList<A2AExposedAgentInfo> ExposedAgents { get; set; } = [];
    public IReadOnlyList<A2ARemoteAgentInfo> RemoteAgents { get; set; } = [];

    public A2AInfo ToInfo() => new(Settings.Enabled, Settings.BasePath, ExposedAgents, RemoteAgents);
}
