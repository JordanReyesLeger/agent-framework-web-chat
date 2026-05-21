using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using System.ClientModel;

namespace AgentFrameworkTests.Helpers;

/// <summary>
/// Clase auxiliar para configuración compartida en las pruebas.
/// Proporciona métodos estáticos para crear clientes de chat de Azure OpenAI.
/// </summary>
public static class TestConfiguration
{
    private static readonly Lazy<IConfiguration> _configuration = new(() =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build());

    /// <summary>
    /// Obtiene la configuración cargada desde appsettings.json.
    /// </summary>
    public static IConfiguration Configuration => _configuration.Value;

    /// <summary>
    /// Crea un ChatClient configurado con Azure OpenAI.
    /// </summary>
    public static ChatClient CreateChatClient()
    {
        var endpoint = Configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("Falta la configuración AzureOpenAI:Endpoint");
        var apiKey = Configuration["AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("Falta la configuración AzureOpenAI:ApiKey");
        var deploymentName = Configuration["AzureOpenAI:DeploymentName"]
            ?? throw new InvalidOperationException("Falta la configuración AzureOpenAI:DeploymentName");

        return new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey))
            .GetChatClient(deploymentName);
    }

    /// <summary>
    /// Crea un AIAgent usando .AsAIAgent() configurado con Azure OpenAI.
    /// </summary>
    public static AIAgent CreateAgent(
        string? instructions = null,
        string? name = null,
        string? description = null,
        IList<AITool>? tools = null)
    {
        return CreateChatClient().AsAIAgent(
            instructions: instructions,
            name: name,
            description: description,
            tools: tools);
    }

    /// <summary>
    /// Crea un AIAgent usando .AsAIAgent() con opciones personalizadas.
    /// </summary>
    public static AIAgent CreateAgent(ChatClientAgentOptions options)
    {
        return CreateChatClient().AsAIAgent(options);
    }

    /// <summary>
    /// Obtiene el nombre del despliegue (deployment) configurado.
    /// </summary>
    public static string DeploymentName =>
        Configuration["AzureOpenAI:DeploymentName"]
        ?? throw new InvalidOperationException("Falta la configuración AzureOpenAI:DeploymentName");
}
