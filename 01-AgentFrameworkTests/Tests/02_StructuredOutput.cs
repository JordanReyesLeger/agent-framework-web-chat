using AgentFrameworkTests.Helpers;
using Microsoft.Agents.AI;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace AgentFrameworkTests.Tests;

/// <summary>
/// Módulo 02: Salida estructurada.
/// Demuestra cómo obtener respuestas tipadas usando RunAsync&lt;T&gt;().
/// El modelo devuelve JSON que se deserializa automáticamente al tipo especificado.
/// </summary>
public class _02_StructuredOutput
{
    private readonly ITestOutputHelper _output;

    public _02_StructuredOutput(ITestOutputHelper output)
    {
        _output = output;
    }

    // ---------- Modelos internos para salida estructurada ----------

    /// <summary>
    /// Modelo para representar un análisis de sentimiento (ejemplo simple).
    /// </summary>
    internal class SentimentAnalysis
    {
        [JsonPropertyName("sentiment")]
        [Description("The detected sentiment: positive, negative, or neutral")]
        public string Sentiment { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        [Description("Confidence score between 0.0 and 1.0")]
        public double Confidence { get; set; }

        [JsonPropertyName("explanation")]
        [Description("Brief explanation of why this sentiment was detected")]
        public string Explanation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Modelo para representar información extraída de un texto (ejemplo con nesting).
    /// </summary>
    internal class ExtractedPersonInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("age")]
        public int Age { get; set; }

        [JsonPropertyName("occupation")]
        public string Occupation { get; set; } = string.Empty;

        [JsonPropertyName("skills")]
        public List<string> Skills { get; set; } = [];

        [JsonPropertyName("address")]
        public AddressInfo? Address { get; set; }
    }

    internal class AddressInfo
    {
        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;

        [JsonPropertyName("country")]
        public string Country { get; set; } = string.Empty;
    }

    // ---------- Pruebas ----------

    /// <summary>
    /// Obtiene una respuesta estructurada de tipo simple usando RunAsync&lt;T&gt;().
    /// El agente analiza el sentimiento de un texto y devuelve un objeto tipado.
    /// </summary>
    [Fact]
    public async Task Should_Return_Structured_Sentiment_Analysis()
    {
        AIAgent agent = TestConfiguration.CreateAgent(
            instructions: "Eres un motor de análisis de sentimiento. Analiza el sentimiento del texto proporcionado y responde con el formato estructurado solicitado.");

        // Crear sesión (requerida para RunAsync)
        AgentSession session = await agent.CreateSessionAsync();

        // RunAsync<T> fuerza al modelo a devolver JSON con el esquema de SentimentAnalysis
        // El framework configura automáticamente el formato de respuesta basado en el tipo T
        AgentResponse<SentimentAnalysis> response = await agent.RunAsync<SentimentAnalysis>(
            "I absolutely love this new Agent Framework! It makes building AI agents so much easier and fun!",
            session);

        // Verificar que la respuesta se deserializó correctamente
        Assert.NotNull(response);
        Assert.NotNull(response.Result);
        Assert.False(string.IsNullOrEmpty(response.Result.Sentiment));
        Assert.InRange(response.Result.Confidence, 0.0, 1.0);

        _output.WriteLine("✅ Análisis de sentimiento estructurado:");
        _output.WriteLine($"   Sentimiento: {response.Result.Sentiment}");
        _output.WriteLine($"   Confianza: {response.Result.Confidence:P0}");
        _output.WriteLine($"   Explicación: {response.Result.Explanation}");
    }

    /// <summary>
    /// Obtiene una respuesta estructurada con tipos anidados (nested objects y arrays).
    /// Demuestra que el framework soporta modelos complejos con propiedades jerárquicas.
    /// </summary>
    [Fact]
    public async Task Should_Return_Structured_Nested_Object()
    {
        AIAgent agent = TestConfiguration.CreateAgent(
            instructions: "Extraes información de personas del texto. Responde con el formato estructurado solicitado.");

        // Crear sesión (requerida para RunAsync)
        AgentSession session = await agent.CreateSessionAsync();

        // Texto del cual extraer la información
        string text = "Maria Garcia is a 35-year-old software architect from Madrid, Spain. " +
                       "She specializes in C#, Azure, and AI/ML technologies.";

        // RunAsync<T> configura automáticamente el esquema JSON del tipo especificado
        AgentResponse<ExtractedPersonInfo> response = await agent.RunAsync<ExtractedPersonInfo>(
            text, session);

        Assert.NotNull(response.Result);
        Assert.False(string.IsNullOrEmpty(response.Result.Name));
        Assert.True(response.Result.Age > 0);
        Assert.NotEmpty(response.Result.Skills);

        _output.WriteLine("✅ Información extraída (objeto anidado):");
        _output.WriteLine($"   Nombre: {response.Result.Name}");
        _output.WriteLine($"   Edad: {response.Result.Age}");
        _output.WriteLine($"   Ocupación: {response.Result.Occupation}");
        _output.WriteLine($"   Habilidades: {string.Join(", ", response.Result.Skills)}");
        _output.WriteLine($"   Ciudad: {response.Result.Address?.City}");
        _output.WriteLine($"   País: {response.Result.Address?.Country}");
    }
}
