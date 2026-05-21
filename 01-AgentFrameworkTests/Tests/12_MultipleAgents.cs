using AgentFrameworkTests.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;
using Xunit.Abstractions;

namespace AgentFrameworkTests.Tests;

/// <summary>
/// Módulo 12: Múltiples agentes en una sola ejecución.
/// Demuestra cómo crear varios agentes especializados y coordinarlos
/// de forma secuencial, donde la salida de un agente alimenta al siguiente.
///
/// Patrones clave:
/// - Crear múltiples AIAgent con instrucciones diferentes
/// - Ejecutar agentes secuencialmente: la respuesta de uno se pasa como entrada al siguiente
/// - Cada agente mantiene su propia sesión y contexto independiente
/// - Coordinar agentes sin usar workflows (patrón manual simple)
/// </summary>
public class _12_MultipleAgents
{
    private readonly ITestOutputHelper _output;

    public _12_MultipleAgents(ITestOutputHelper output)
    {
        _output = output;
    }

    // ---------- Pruebas ----------

    /// <summary>
    /// Ejecuta dos agentes en secuencia: un escritor genera contenido
    /// y un crítico lo evalúa. La salida del escritor se pasa como entrada al crítico.
    ///
    /// Patrón:
    /// 1. Agente escritor recibe un tema y genera un párrafo
    /// 2. Agente crítico recibe el párrafo y genera una evaluación
    /// </summary>
    [Fact]
    public async Task Should_Run_Writer_And_Critic_Sequentially()
    {
        // Agente 1: Escritor creativo
        AIAgent writer = TestConfiguration.CreateAgent(
            instructions: "Eres un escritor creativo. Escribe un párrafo corto (máximo 3 oraciones) sobre el tema proporcionado. Sé conciso y creativo.",
            name: "Escritor");

        // Agente 2: Crítico literario
        AIAgent critic = TestConfiguration.CreateAgent(
            instructions: "Eres un crítico literario estricto. Evalúa el texto proporcionado en una oración breve indicando un aspecto positivo y uno a mejorar. Sé directo.",
            name: "Critico");

        // Paso 1: El escritor genera contenido
        AgentSession writerSession = await writer.CreateSessionAsync();
        AgentResponse writerResponse = await writer.RunAsync(
            "Escribe sobre la inteligencia artificial en la vida cotidiana", writerSession);

        Assert.NotNull(writerResponse.Text);
        _output.WriteLine("📝 Escritor:");
        _output.WriteLine($"   {writerResponse.Text}");

        // Paso 2: El crítico evalúa la salida del escritor
        AgentSession criticSession = await critic.CreateSessionAsync();
        AgentResponse criticResponse = await critic.RunAsync(
            $"Evalúa este texto: \"{writerResponse.Text}\"", criticSession);

        Assert.NotNull(criticResponse.Text);
        _output.WriteLine("\n🔍 Crítico:");
        _output.WriteLine($"   {criticResponse.Text}");

        _output.WriteLine("\n✅ Dos agentes ejecutados en secuencia: Escritor → Crítico");
    }

    /// <summary>
    /// Tres agentes en cadena formando un pipeline de procesamiento de texto:
    /// Redactor → Traductor → Resumidor.
    ///
    /// Cada agente tiene un rol especializado y su salida alimenta al siguiente.
    /// Demuestra el patrón de pipeline sin necesidad de workflows formales.
    /// </summary>
    [Fact]
    public async Task Should_Run_Three_Agent_Pipeline()
    {
        // Agente 1: Redactor — genera un texto técnico breve
        AIAgent redactor = TestConfiguration.CreateAgent(
            instructions: "Eres un redactor técnico. Escribe una explicación breve (2-3 oraciones) sobre el concepto proporcionado. Usa lenguaje claro y preciso. Responde en español.",
            name: "Redactor");

        // Agente 2: Traductor — traduce al inglés
        AIAgent translator = TestConfiguration.CreateAgent(
            instructions: "Eres un traductor profesional. Traduce el texto proporcionado al inglés. Solo responde con la traducción, sin agregar notas ni explicaciones.",
            name: "Traductor");

        // Agente 3: Resumidor — crea un resumen de una línea
        AIAgent summarizer = TestConfiguration.CreateAgent(
            instructions: "You are a summarizer. Create a one-sentence summary of the provided text. Be extremely concise.",
            name: "Resumidor");

        // Pipeline: Redactor → Traductor → Resumidor
        string topic = "¿Qué es un contenedor Docker?";

        // Paso 1: Redactar
        AgentSession s1 = await redactor.CreateSessionAsync();
        AgentResponse r1 = await redactor.RunAsync(topic, s1);
        Assert.NotNull(r1.Text);
        _output.WriteLine("📝 Redactor (ES):");
        _output.WriteLine($"   {r1.Text}");

        // Paso 2: Traducir
        AgentSession s2 = await translator.CreateSessionAsync();
        AgentResponse r2 = await translator.RunAsync(r1.Text!, s2);
        Assert.NotNull(r2.Text);
        _output.WriteLine("\n🌐 Traductor (EN):");
        _output.WriteLine($"   {r2.Text}");

        // Paso 3: Resumir
        AgentSession s3 = await summarizer.CreateSessionAsync();
        AgentResponse r3 = await summarizer.RunAsync(r2.Text!, s3);
        Assert.NotNull(r3.Text);
        _output.WriteLine("\n📋 Resumidor:");
        _output.WriteLine($"   {r3.Text}");

        _output.WriteLine("\n✅ Pipeline de 3 agentes completado: Redactor → Traductor → Resumidor");
    }

    /// <summary>
    /// Múltiples agentes procesan el mismo mensaje de forma independiente (fan-out).
    /// Cada agente analiza el mismo texto desde una perspectiva diferente.
    ///
    /// Patrón fan-out: un mensaje → N agentes → N respuestas independientes.
    /// Útil para obtener múltiples perspectivas sobre un mismo problema.
    /// </summary>
    [Fact]
    public async Task Should_Run_Multiple_Agents_On_Same_Input()
    {
        // Definir agentes con perspectivas diferentes
        var agents = new (string Name, string Instructions)[]
        {
            ("Optimista", "Eres un analista extremadamente optimista. Analiza el mensaje proporcionado destacando solo lo positivo en una oración."),
            ("Pesimista", "Eres un analista muy pesimista. Analiza el mensaje proporcionado señalando solo los riesgos o problemas en una oración."),
            ("Neutral", "Eres un analista neutral y equilibrado. Analiza el mensaje proporcionado de forma objetiva en una oración.")
        };

        string inputMessage = "Una empresa de tecnología planea reemplazar el 50% de sus procesos manuales con inteligencia artificial";

        var responses = new List<(string AgentName, string Response)>();

        // Ejecutar cada agente de forma independiente con el mismo input
        foreach (var (name, instructions) in agents)
        {
            AIAgent agent = TestConfiguration.CreateAgent(
                instructions: instructions,
                name: name);

            AgentSession session = await agent.CreateSessionAsync();
            AgentResponse response = await agent.RunAsync(inputMessage, session);

            Assert.NotNull(response.Text);
            responses.Add((name, response.Text!));
        }

        // Mostrar todas las perspectivas
        _output.WriteLine($"📨 Mensaje: \"{inputMessage}\"\n");
        foreach (var (agentName, response) in responses)
        {
            _output.WriteLine($"🔹 {agentName}: {response}");
        }

        Assert.Equal(3, responses.Count);
        _output.WriteLine($"\n✅ {responses.Count} agentes procesaron el mismo mensaje con perspectivas diferentes.");
    }
}
