namespace AFWebChat.Models;

/// <summary>
/// Configuración del protocolo A2A (Agent2Agent) en AF-WebChat.
/// Cubre las dos direcciones del protocolo: exponer nuestros agentes como servidores A2A
/// y consumir agentes A2A remotos como si fueran agentes locales del chat.
/// </summary>
public class A2ASettings
{
    public const string SectionName = "A2A";

    /// <summary>Habilita o deshabilita toda la integración A2A.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Prefijo de ruta donde se publican los agentes locales vía A2A (ej. <c>/a2a/GeneralAssistant</c>).</summary>
    public string BasePath { get; set; } = "/a2a";

    /// <summary>URL pública base del host, usada para construir la <c>Url</c> del AgentCard.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>Nombres de agentes de AF-WebChat que se publican como servidores A2A.</summary>
    public string[] ExposedAgents { get; set; } = [];

    /// <summary>Agentes A2A remotos que se registran en el catálogo de agentes del chat.</summary>
    public A2ARemoteAgentSettings[] RemoteAgents { get; set; } = [];

    /// <summary>Timeout de las llamadas HTTP hacia agentes A2A remotos.</summary>
    public int RemoteTimeoutSeconds { get; set; } = 300;
}

/// <summary>Definición de un agente A2A remoto que se expone en el chat como un agente más.</summary>
public class A2ARemoteAgentSettings
{
    /// <summary>Nombre con el que aparece en el catálogo de agentes.</summary>
    public string Name { get; set; } = "";

    /// <summary>Endpoint A2A del agente remoto (la raíz del agente, no <c>/v1/...</c>).</summary>
    public string Url { get; set; } = "";

    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public string[]? ExamplePrompts { get; set; }
}

/// <summary>Información de descubrimiento A2A que expone la API para demos e integraciones.</summary>
public record A2AInfo(
    bool Enabled,
    string BasePath,
    IReadOnlyList<A2AExposedAgentInfo> ExposedAgents,
    IReadOnlyList<A2ARemoteAgentInfo> RemoteAgents);

public record A2AExposedAgentInfo(string Name, string Description, string Path, string Url, string AgentCardUrl);

public record A2ARemoteAgentInfo(string Name, string Description, string Url, bool Registered);
