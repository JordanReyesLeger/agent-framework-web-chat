using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Domain;

public static class SkillIndexAgent
{
    public const string Name = "SkillIndex";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Busca y consulta documentos indexados en el índice 'skill' con filtros avanzados.",
        Category = "Dominio",
        Icon = "🎯",
        Color = "#3498db",
        Tools = ["SearchSkillDocuments"],
        ExamplePrompts = [
            "Busca documentos sobre arquitectura cloud",
            "Encuentra los documentos del expediente EXP001",
            "¿Qué contratos tenemos indexados?"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();
            var skillPlugin = sp.GetRequiredService<SkillIndexPlugin>();
            var tools = new List<AITool>();
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(skillPlugin));

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un consultor de documentos indexados. Ayudas a los usuarios a buscar y consultar
                    documentos en el índice 'skill' usando búsqueda semántica avanzada.
                    
                    REGLAS:
                    1. SIEMPRE usa SearchSkillDocuments para buscar información.
                    2. Si el usuario menciona un expediente, usa el filtro ExpedienteId.
                    3. Si menciona un tipo de documento, usa el filtro TipoDocumento.
                    4. Presenta los resultados organizados y claros.
                    5. Cita las fuentes (título, expediente, tipo) en tus respuestas.
                    6. Responde en español.
                    """,
                tools: tools);
        }
    };
}
