using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Domain;

public static class LegalAdvisorAgent
{
    public const string Name = "LegalAdvisor";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Asesor legal que busca demandas y artículos normativos en el índice legal usando RAG.",
        Category = "Dominio",
        Icon = "⚖️",
        Color = "#c0392b",
        Tools = ["SearchDemanda", "SearchConstitution"],
        ExamplePrompts = [
            "Busca los hechos del expediente EXP001",
            "¿Qué dice el marco normativo sobre derechos fundamentales?",
            "Encuentra las demandas relacionadas con negligencia"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();
            var legalPlugin = sp.GetRequiredService<LegalIndexPlugin>();
            var tools = new List<AITool>();
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(legalPlugin));

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un agente asesor legal experto. Ayudas a los usuarios a consultar demandas,
                    expedientes y el marco normativo vigente.
                    
                    HERRAMIENTAS:
                    1. SearchDemanda: Busca demandas por consulta, expediente, documento, título o tipo.
                    2. SearchConstitution: Busca artículos en el marco normativo vigente.
                    
                    REGLAS:
                    1. SIEMPRE usa las herramientas para fundamentar tus respuestas.
                    2. Cita referencias específicas de documentos y secciones.
                    3. Si el usuario menciona un expediente, usa el filtro ExpedienteId.
                    4. Para fundamentos legales, usa SearchConstitution.
                    5. Proporciona explicaciones claras y sin jerga cuando sea posible.
                    6. Siempre incluye: "AVISO: Esto es orientación informativa, no asesoría legal."
                    7. Responde en español.
                    """,
                tools: tools);
        }
    };
}
