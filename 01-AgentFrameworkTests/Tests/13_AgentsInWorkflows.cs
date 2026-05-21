using AgentFrameworkTests.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace AgentFrameworkTests.Tests;

/// <summary>
/// Módulo 13: Agentes en Workflows (AgentWorkflowBuilder).
/// Demuestra cómo usar AgentWorkflowBuilder para orquestar agentes AI
/// dentro de workflows. A diferencia del Módulo 12 (coordinación manual con RunAsync),
/// aquí los agentes se conectan como ejecutores del workflow framework.
///
/// Patrones clave:
/// - AgentWorkflowBuilder.BuildSequential: pipeline de agentes en secuencia
/// - AgentWorkflowBuilder.BuildConcurrent: agentes en paralelo (fan-out)
/// - AgentWorkflowBuilder.CreateGroupChatBuilderWith: group chat round-robin
/// - TurnToken(emitEvents: true): trigger para activar los agentes y emitir eventos
/// - AgentResponseUpdateEvent: eventos de streaming parcial de texto
/// - WorkflowOutputEvent: salida final del workflow con los mensajes completos
///
/// A diferencia de los Módulos 09-11 (ejecutores custom con Executor&lt;TIn, TOut&gt;),
/// aquí los agentes AI son los ejecutores — el framework los envuelve automáticamente.
/// La entrada al workflow es List&lt;ChatMessage&gt; (no string como en ejecutores raw).
/// </summary>
public class _13_AgentsInWorkflows
{
    private readonly ITestOutputHelper _output;

    public _13_AgentsInWorkflows(ITestOutputHelper output)
    {
        _output = output;
    }

    // ---------- Helper ----------

    /// <summary>
    /// Ejecuta un workflow basado en agentes y retorna el texto acumulado
    /// de las actualizaciones de streaming y los mensajes finales.
    ///
    /// Patrón de ejecución para agent-workflows:
    /// 1. InProcessExecution.RunStreamingAsync con List&lt;ChatMessage&gt; como entrada
    /// 2. TrySendMessageAsync(TurnToken) para activar el procesamiento de los agentes
    /// 3. Observar AgentResponseUpdateEvent (streaming) y WorkflowOutputEvent (resultado final)
    /// </summary>
    private async Task<(string StreamedText, List<ChatMessage>? FinalMessages)> RunAgentWorkflowAsync(
        Workflow workflow, string userMessage)
    {
        var sb = new StringBuilder();
        var input = new List<ChatMessage> { new(ChatRole.User, userMessage) };

        // RunStreamingAsync acepta List<ChatMessage> como entrada para agent-workflows
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow, input: input);

        // TurnToken activa el procesamiento — los agentes no inician hasta recibirlo
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        WorkflowOutputEvent? finalOutput = null;

        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is AgentResponseUpdateEvent update)
            {
                // Cada update contiene un fragmento de texto del streaming del agente
                sb.Append(update.Data);
            }
            else if (evt is WorkflowOutputEvent outputEvt)
            {
                // El WorkflowOutputEvent indica que el workflow terminó
                finalOutput = outputEvt;
                break;
            }
            else if (evt is WorkflowErrorEvent errorEvt)
            {
                throw new InvalidOperationException(
                    $"Error en workflow: {errorEvt.Exception?.Message ?? "Error desconocido"}");
            }
        }

        return (sb.ToString(), finalOutput?.As<List<ChatMessage>>());
    }

    // ---------- Pruebas ----------

    /// <summary>
    /// Workflow secuencial de agentes: Escritor → Traductor.
    /// AgentWorkflowBuilder.BuildSequential conecta los agentes como ejecutores
    /// en un pipeline donde la salida del primero alimenta al siguiente.
    ///
    /// A diferencia del Módulo 12 (coordinación manual con CreateSessionAsync/RunAsync),
    /// aquí el framework maneja la orquestación, el paso de mensajes entre agentes,
    /// y el streaming de eventos automáticamente.
    /// </summary>
    [Fact]
    public async Task Should_Run_Sequential_Agent_Workflow()
    {
        // Crear agentes con instrucciones específicas
        AIAgent writer = TestConfiguration.CreateAgent(
            instructions: "Eres un escritor creativo. Escribe exactamente 2 oraciones sobre el tema proporcionado. Responde solo con el texto, sin encabezados.",
            name: "Escritor",
            description: "Genera contenido creativo en español");

        AIAgent translator = TestConfiguration.CreateAgent(
            instructions: "Eres un traductor profesional. Traduce el texto completo al inglés. Responde solo con la traducción.",
            name: "Traductor",
            description: "Traduce texto del español al inglés");

        // BuildSequential crea un workflow pipeline:
        // El mensaje del usuario llega al Escritor → su respuesta pasa al Traductor → salida final
        Workflow workflow = AgentWorkflowBuilder.BuildSequential(writer, translator);

        var (streamedText, finalMessages) = await RunAgentWorkflowAsync(
            workflow, "La exploración espacial y el futuro de la humanidad");

        Assert.NotEmpty(streamedText);
        _output.WriteLine("🔄 Workflow Secuencial de Agentes (Escritor → Traductor):");
        _output.WriteLine($"\n📝 Texto streameado:\n   {streamedText}");

        if (finalMessages != null)
        {
            _output.WriteLine($"\n📩 Mensajes finales: {finalMessages.Count}");
            foreach (var msg in finalMessages)
            {
                var preview = msg.Text?.Length > 150 ? msg.Text[..150] + "..." : msg.Text;
                _output.WriteLine($"   [{msg.Role}]: {preview}");
            }
        }

        _output.WriteLine("\n✅ Workflow secuencial completado: Escritor → Traductor (orquestado por AgentWorkflowBuilder)");
    }

    /// <summary>
    /// Workflow concurrente de agentes: Optimista y Pesimista en paralelo.
    /// AgentWorkflowBuilder.BuildConcurrent ejecuta todos los agentes
    /// simultáneamente con el mismo input y luego agrega los resultados.
    ///
    /// Es el equivalente al patrón fan-out del Módulo 12, pero orquestado
    /// por el framework de workflows con ejecución verdaderamente paralela.
    /// </summary>
    [Fact]
    public async Task Should_Run_Concurrent_Agent_Workflow()
    {
        AIAgent optimist = TestConfiguration.CreateAgent(
            instructions: "Eres un analista extremadamente optimista. Analiza el tema proporcionado en una oración destacando solo aspectos positivos. Responde en español.",
            name: "Optimista",
            description: "Analiza de forma optimista");

        AIAgent pessimist = TestConfiguration.CreateAgent(
            instructions: "Eres un analista muy pesimista. Analiza el tema proporcionado en una oración señalando solo riesgos y problemas. Responde en español.",
            name: "Pesimista",
            description: "Analiza de forma pesimista");

        // BuildConcurrent ejecuta los agentes en paralelo con el mismo input
        Workflow workflow = AgentWorkflowBuilder.BuildConcurrent([optimist, pessimist]);

        var (streamedText, finalMessages) = await RunAgentWorkflowAsync(
            workflow, "El impacto de la inteligencia artificial en el mercado laboral");

        Assert.NotEmpty(streamedText);
        _output.WriteLine("⚡ Workflow Concurrente de Agentes (Optimista ↔ Pesimista):");
        _output.WriteLine($"\n📝 Respuestas combinadas:\n   {streamedText}");

        if (finalMessages != null)
        {
            _output.WriteLine($"\n📩 Mensajes finales: {finalMessages.Count}");
            foreach (var msg in finalMessages)
            {
                var preview = msg.Text?.Length > 150 ? msg.Text[..150] + "..." : msg.Text;
                _output.WriteLine($"   [{msg.Role}]: {preview}");
            }
        }

        _output.WriteLine("\n✅ Workflow concurrente completado: Optimista ↔ Pesimista (en paralelo)");
    }

    /// <summary>
    /// Workflow de group chat con protocolo round-robin.
    /// Tres agentes debaten un tema por turnos usando RoundRobinGroupChatManager.
    ///
    /// MaximumIterationCount controla cuántas rondas completas se ejecutan.
    /// Cada ronda consiste en que todos los participantes responden una vez.
    /// El resultado final contiene la conversación completa del grupo.
    /// </summary>
    [Fact]
    public async Task Should_Run_GroupChat_Agent_Workflow()
    {
        AIAgent developer = TestConfiguration.CreateAgent(
            instructions: "Eres un desarrollador de software. En las discusiones, aporta la perspectiva técnica sobre viabilidad e implementación. Responde en 1-2 oraciones concisas en español.",
            name: "Desarrollador",
            description: "Aporta perspectiva técnica de desarrollo de software");

        AIAgent designer = TestConfiguration.CreateAgent(
            instructions: "Eres un diseñador UX/UI. En las discusiones, aporta la perspectiva de experiencia de usuario y diseño visual. Responde en 1-2 oraciones concisas en español.",
            name: "Disenador",
            description: "Aporta perspectiva de diseño UX/UI");

        AIAgent productManager = TestConfiguration.CreateAgent(
            instructions: "Eres un gerente de producto. En las discusiones, aporta la perspectiva de negocio, priorización y valor para el cliente. Responde en 1-2 oraciones concisas en español.",
            name: "GerenteProducto",
            description: "Aporta perspectiva de negocio y producto");

        // CreateGroupChatBuilderWith configura el gestor de turnos
        // RoundRobinGroupChatManager alterna los turnos entre participantes
        Workflow workflow = AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(agents =>
                new RoundRobinGroupChatManager(agents) { MaximumIterationCount = 2 })
            .AddParticipants(developer, designer, productManager)
            .Build();

        var (streamedText, finalMessages) = await RunAgentWorkflowAsync(
            workflow, "¿Deberíamos agregar un modo oscuro a nuestra aplicación móvil?");

        Assert.NotEmpty(streamedText);
        _output.WriteLine("🗣️ Group Chat (Round-Robin, 2 rondas):");
        _output.WriteLine($"\n📝 Discusión completa:\n   {streamedText}");

        if (finalMessages != null)
        {
            _output.WriteLine($"\n📩 Mensajes del chat: {finalMessages.Count}");
            foreach (var msg in finalMessages)
            {
                var preview = msg.Text?.Length > 100 ? msg.Text[..100] + "..." : msg.Text;
                _output.WriteLine($"   [{msg.Role}]: {preview}");
            }
        }

        _output.WriteLine("\n✅ Group chat completado: Desarrollador ↔ Diseñador ↔ Gerente (round-robin, 2 rondas)");
    }

    /// <summary>
    /// Workflow con agentes usando WorkflowBuilder + AddEdge directamente.
    /// A diferencia de los tests anteriores que usan AgentWorkflowBuilder (helper de alto nivel),
    /// aquí construimos el grafo manualmente usando la API de bajo nivel:
    ///
    ///   1. agent.BindAsExecutor(AIAgentHostOptions) → convierte AIAgent en ExecutorBinding
    ///   2. new WorkflowBuilder(start) → crea el builder con el nodo inicial
    ///   3. builder.AddEdge(from, to) → conecta nodos como un grafo dirigido
    ///   4. OutputMessagesExecutor → nodo terminal que recolecta la salida final
    ///
    /// Este es el mismo patrón que usan los Módulos 09-11 con ejecutores custom,
    /// pero aquí los nodos del workflow son agentes AI reales en vez de funciones simples.
    /// </summary>
    [Fact]
    public async Task Should_Run_Agent_Workflow_With_Manual_Edges()
    {
        // 1. Crear agentes AI
        AIAgent investigador = TestConfiguration.CreateAgent(
            instructions: "Eres un investigador. Dado un tema, escribe exactamente 2 datos curiosos sobre él. Responde solo con los datos, sin encabezados ni numeración.",
            name: "Investigador",
            description: "Investiga datos curiosos sobre un tema");

        AIAgent resumidor = TestConfiguration.CreateAgent(
            instructions: "Eres un editor. Toma el texto proporcionado y genera un resumen de una sola oración que capture la esencia. Responde solo con el resumen.",
            name: "Resumidor",
            description: "Resume texto en una oración");

        // 2. Convertir agentes a ExecutorBinding usando BindAsExecutor
        // AIAgentHostOptions configura cómo se comporta el agente dentro del workflow:
        // - ReassignOtherAgentsAsUsers: los mensajes de otros agentes se presentan como mensajes de usuario
        // - ForwardIncomingMessages: reenvía los mensajes entrantes al agente
        var hostOptions = new AIAgentHostOptions
        {
            ReassignOtherAgentsAsUsers = true,
            ForwardIncomingMessages = true,
        };

        ExecutorBinding investigadorNode = investigador.BindAsExecutor(hostOptions);
        ExecutorBinding resumidorNode = resumidor.BindAsExecutor(hostOptions);

        // 3. Construir el workflow manualmente con WorkflowBuilder + AddEdge
        // Grafo: Investigador → Resumidor (el último nodo es la fuente de salida)
        Workflow workflow = new WorkflowBuilder(investigadorNode)
            .AddEdge(investigadorNode, resumidorNode)
            .WithOutputFrom(resumidorNode)
            .Build();

        // 4. Ejecutar usando el helper existente
        var (streamedText, finalMessages) = await RunAgentWorkflowAsync(
            workflow, "Los pulpos y su inteligencia");

        Assert.NotEmpty(streamedText);
        _output.WriteLine("🔧 Workflow Manual con AddEdge (Investigador → Resumidor):");
        _output.WriteLine($"\n📝 Texto streameado:\n   {streamedText}");

        if (finalMessages != null)
        {
            _output.WriteLine($"\n📩 Mensajes finales: {finalMessages.Count}");
            foreach (var msg in finalMessages)
            {
                var preview = msg.Text?.Length > 200 ? msg.Text[..200] + "..." : msg.Text;
                _output.WriteLine($"   [{msg.Role}]: {preview}");
            }
        }

        _output.WriteLine("\n✅ Workflow manual completado: Investigador → Resumidor (WorkflowBuilder + AddEdge)");
    }

    /// <summary>
    /// Enrutamiento condicional con agentes: Clasificador → Optimista / Pesimista.
    /// Aplica el patrón de edges condicionales del Módulo 10 (AddEdge&lt;T&gt; con condition)
    /// pero usando agentes AI en lugar de ejecutores custom (Executor&lt;TIn, TOut&gt;).
    ///
    /// Flujo:
    ///   [Clasificador] →(POSITIVO)→ [Optimista]
    ///                  →(NEGATIVO)→ [Pesimista]
    ///
    /// Técnica clave: AddEdge&lt;object&gt; con condition y closure.
    /// AIAgentHostExecutor envía List&lt;ChatMessage&gt; ANTES que TurnToken,
    /// permitiendo capturar la clasificación en la closure antes de evaluar
    /// la ruta del TurnToken. Así tanto mensajes como TurnToken van al mismo handler.
    ///
    /// ForwardIncomingMessages=false en el clasificador evita que los mensajes
    /// originales del usuario se envíen downstream antes de la respuesta clasificada.
    /// </summary>
    [Fact]
    public async Task Should_Route_Conditionally_To_Negative_Agent()
    {
        // 1. Crear los agentes
        // Clasificador: analiza sentimiento y responde con POSITIVO o NEGATIVO
        AIAgent clasificador = TestConfiguration.CreateAgent(
            instructions: "Eres un clasificador de sentimientos. Analiza el texto y responde SOLAMENTE " +
                          "con la palabra 'POSITIVO' si el sentimiento es positivo, o 'NEGATIVO' si es negativo. " +
                          "No incluyas ninguna otra palabra ni explicación.",
            name: "Clasificador",
            description: "Clasifica el sentimiento como POSITIVO o NEGATIVO");

        AIAgent optimista = TestConfiguration.CreateAgent(
            instructions: "Eres un agente optimista. Responde de forma alegre y motivadora en una oración en español.",
            name: "Optimista",
            description: "Responde de forma optimista");

        AIAgent pesimista = TestConfiguration.CreateAgent(
            instructions: "Eres un agente pesimista. Responde señalando riesgos y dificultades en una oración en español.",
            name: "Pesimista",
            description: "Responde de forma pesimista");

        // 2. Configurar opciones de host
        // El clasificador NO reenvía mensajes entrantes — solo envía su respuesta clasificada
        var clasificadorOptions = new AIAgentHostOptions
        {
            ReassignOtherAgentsAsUsers = true,
            ForwardIncomingMessages = false,
        };

        var handlerOptions = new AIAgentHostOptions
        {
            ReassignOtherAgentsAsUsers = true,
            ForwardIncomingMessages = true,
        };

        // 3. Convertir agentes a ExecutorBinding
        ExecutorBinding clasificadorBinding = clasificador.BindAsExecutor(clasificadorOptions);
        ExecutorBinding optimistaBinding = optimista.BindAsExecutor(handlerOptions);
        ExecutorBinding pesimistaBinding = pesimista.BindAsExecutor(handlerOptions);

        // 4. Construir el workflow con edges condicionales
        // Closure para rastrear la ruta:
        //   - Cuando llega List<ChatMessage>, se evalúa el contenido y se almacena la ruta
        //   - Cuando llega TurnToken (u otro tipo), se usa la ruta previamente almacenada
        //   - Esto funciona porque AIAgentHostExecutor envía mensajes ANTES que TurnToken
        bool rutaPositiva = false;

        Workflow workflow = new WorkflowBuilder(clasificadorBinding)
            .AddEdge<object>(clasificadorBinding, optimistaBinding,
                condition: obj =>
                {
                    if (obj is List<ChatMessage> msgs && msgs.Count > 0)
                    {
                        rutaPositiva = msgs.Any(m =>
                            m.Text?.Contains("POSITIVO", StringComparison.OrdinalIgnoreCase) == true);
                        return rutaPositiva;
                    }
                    // TurnToken y otros tipos siguen la misma ruta que los mensajes
                    return rutaPositiva;
                })
            .AddEdge<object>(clasificadorBinding, pesimistaBinding,
                condition: obj =>
                {
                    if (obj is List<ChatMessage> msgs && msgs.Count > 0)
                        return !rutaPositiva;
                    return !rutaPositiva;
                })
            .WithOutputFrom(optimistaBinding, pesimistaBinding)
            .Build();

        // 5. Ejecutar con texto negativo → Clasificador responde "NEGATIVO" → ruta al Pesimista
        var (streamedText, finalMessages) = await RunAgentWorkflowAsync(
            workflow, "Todo está saliendo terrible, el proyecto va a fracasar y no hay esperanza");

        // 6. Verificar que se produjo salida del handler
        Assert.False(string.IsNullOrWhiteSpace(streamedText),
            "El workflow con enrutamiento condicional debería producir texto streameado");

        _output.WriteLine("🔀 Enrutamiento Condicional con Agentes (Clasificador → Optimista / Pesimista):");
        _output.WriteLine($"\n   Ruta tomada: {(rutaPositiva ? "Optimista ☀️" : "Pesimista 🌧️")}");
        _output.WriteLine($"\n📝 Respuesta del handler:\n   {streamedText}");

        if (finalMessages != null)
        {
            _output.WriteLine($"\n📩 Mensajes finales: {finalMessages.Count}");
            foreach (var msg in finalMessages)
            {
                var preview = msg.Text?.Length > 200 ? msg.Text[..200] + "..." : msg.Text;
                _output.WriteLine($"   [{msg.Role}]: {preview}");
            }
        }

        _output.WriteLine("\n✅ Enrutamiento condicional con agentes completado " +
                          "(Clasificador → Pesimista vía AddEdge<object> con condition)");
    }
}
