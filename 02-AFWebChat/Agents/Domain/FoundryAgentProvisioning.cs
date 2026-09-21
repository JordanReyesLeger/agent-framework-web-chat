using System.Security.Cryptography;
using System.Text;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Microsoft.Extensions.AI;
using OpenAI.Responses;

#pragma warning disable OPENAI001 // Las APIs de Responses son experimentales en este SDK.

namespace AFWebChat.Agents.Domain;

/// <summary>
/// Utilidades compartidas por los agentes "Foundry Agent versioned".
///
/// Resuelve dos problemas propios de ese patrón:
///
/// 1. <b>Las herramientas locales deben declararse en Foundry.</b> Pasar <c>AIFunction</c>s a
///    <c>AsAIAgent(record, tools)</c> solo aporta la IMPLEMENTACIÓN que se ejecuta en este
///    proceso; el modelo únicamente ve las herramientas declaradas en la versión del agente
///    guardada en Foundry. <see cref="ToResponseTools"/> traduce las funciones locales a
///    declaraciones que se publican junto al resto de la definición.
///
/// 2. <b>La definición vive en el servidor y se queda obsoleta.</b> Como el agente se crea una
///    sola vez, cambiar instrucciones o herramientas en el código no tenía efecto sobre un agente
///    ya existente. <see cref="EnsureAgentVersion"/> firma cada definición y publica una versión
///    nueva en cuanto la firma cambia.
/// </summary>
public static class FoundryAgentProvisioning
{
    /// <summary>Clave de metadatos donde se guarda la firma de la definición publicada.</summary>
    private const string RevisionMetadataKey = "afwebchat_revision";

    /// <summary>
    /// Convierte herramientas locales (<see cref="AIFunction"/>) en declaraciones que Foundry
    /// publica al modelo. La ejecución sigue siendo local: Foundry solicita la llamada y el SDK
    /// la resuelve en este proceso con la instancia que se pasa a <c>AsAIAgent</c>.
    /// </summary>
    public static IEnumerable<ResponseTool> ToResponseTools(IEnumerable<AITool> tools)
    {
        foreach (var tool in tools)
        {
            if (tool is not AIFunction function) continue;

            yield return ResponseTool.CreateFunctionTool(
                functionName: function.Name,
                functionParameters: BinaryData.FromString(function.JsonSchema.GetRawText()),
                strictModeEnabled: false,
                functionDescription: function.Description);
        }
    }

    /// <summary>
    /// Devuelve el agente versionado de Foundry, creándolo o publicando una versión nueva si la
    /// definición del código cambió respecto de la publicada.
    /// </summary>
    /// <param name="client">Cliente del proyecto de Foundry.</param>
    /// <param name="agentName">Nombre del agente en Foundry.</param>
    /// <param name="buildDefinition">Construye la definición deseada (se invoca solo si hay que publicar).</param>
    /// <param name="revisionSource">
    /// Texto que identifica la definición (instrucciones + modelo + herramientas). Si cambia,
    /// se publica una versión nueva.
    /// </param>
    public static ProjectsAgentRecord EnsureAgentVersion(
        AIProjectClient client,
        string agentName,
        Func<DeclarativeAgentDefinition> buildDefinition,
        string revisionSource,
        ILogger logger)
    {
        var revision = ComputeRevision(revisionSource);
        var admin = client.AgentAdministrationClient;

        ProjectsAgentRecord? record = null;
        try
        {
            record = admin.GetAgent(agentName);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404) { }
        catch (System.ClientModel.ClientResultException ex) when (ex.Status == 404) { }

        if (record is not null && PublishedRevisionMatches(record, revision, agentName, logger))
        {
            logger.LogInformation(
                "Foundry agent '{AgentName}' está al día (revisión {Revision}).", agentName, revision);
            return record;
        }

        var isUpdate = record is not null;
        logger.LogInformation(
            isUpdate
                ? "La definición de '{AgentName}' cambió — publicando versión nueva (revisión {Revision})..."
                : "Foundry agent '{AgentName}' no existe — creándolo (revisión {Revision})...",
            agentName, revision);

        var options = new ProjectsAgentVersionCreationOptions(buildDefinition())
        {
            Metadata = { [RevisionMetadataKey] = revision }
        };

        var version = admin.CreateAgentVersion(agentName: agentName, options: options);
        logger.LogInformation(
            "Foundry agent '{AgentName}' publicado (id: {Id}, versión: {Version}).",
            agentName, version.Value.Id, version.Value.Version);

        // Foundry necesita unos segundos para que la versión nueva quede disponible para ejecución.
        Thread.Sleep(5000);
        return admin.GetAgent(agentName);
    }

    private static bool PublishedRevisionMatches(
        ProjectsAgentRecord record, string revision, string agentName, ILogger logger)
    {
        try
        {
            var latest = record.GetLatestVersion();
            return latest.Metadata is not null
                && latest.Metadata.TryGetValue(RevisionMetadataKey, out var published)
                && published == revision;
        }
        catch (Exception ex)
        {
            // Si no se puede leer la versión publicada, se prefiere republicar antes que
            // arrastrar una definición obsoleta.
            logger.LogWarning(ex,
                "No se pudo leer la revisión publicada de '{AgentName}'; se publicará una versión nueva.",
                agentName);
            return false;
        }
    }

    private static string ComputeRevision(string source)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)))[..16];
}
