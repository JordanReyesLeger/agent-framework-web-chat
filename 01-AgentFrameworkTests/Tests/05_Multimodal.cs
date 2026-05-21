using AgentFrameworkTests.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;
using Xunit.Abstractions;

namespace AgentFrameworkTests.Tests;

/// <summary>
/// Módulo 05: Multimodal (análisis de imágenes).
/// Demuestra cómo enviar imágenes al agente para análisis visual
/// usando ChatMessage con UriContent y TextContent.
/// </summary>
public class _05_Multimodal
{
    private readonly ITestOutputHelper _output;

    public _05_Multimodal(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Envía una imagen pública por URL para que el agente la analice.
    /// Usa UriContent para referenciar la imagen por URI sin descargarla localmente.
    /// </summary>
    [Fact]
    public async Task Should_Analyze_Image_From_Url()
    {
        AIAgent agent = TestConfiguration.CreateAgent(
            instructions: "Eres un asistente de análisis de imágenes. Describe las imágenes de forma clara y concisa.");

        // Crear sesión (requerida para RunAsync)
        AgentSession session = await agent.CreateSessionAsync();

        // Crear un mensaje multimodal que combina texto e imagen
        // UriContent permite referenciar una imagen por su URL
        var imageUrl = new Uri("https://gelsoftcom.azurewebsites.net/images/Soldados001.png");

        var message = new ChatMessage(ChatRole.User, [
            new TextContent("Describe this image in one sentence:"),
            new UriContent(imageUrl, "image/png")
        ]);

        // Enviar el mensaje multimodal al agente
        AgentResponse response = await agent.RunAsync(new[] { message }, session);

        Assert.NotNull(response);
        Assert.NotNull(response.Text);

        _output.WriteLine("✅ Análisis de imagen por URL:");
        _output.WriteLine($"   {response.Text}");
    }
}
