using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Composite;

public static class ResearchAssistantAgent
{
    public const string Name = "ResearchAssistant";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Asistente de investigación que orquesta sub-agentes de búsqueda, resumen y traducción.",
        Category = "Compuesto",
        Icon = "🔬",
        Color = "#2980b9",
        Tools = ["WebSearch (agente)", "Summarizer (agente)", "Translator (agente)"],
        ExamplePrompts = ["Investiga las últimas tendencias en agentes de IA", "Busca y resume artículos sobre Semantic Kernel", "Busca información sobre Azure AI y tradúcela al español"],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();
            var registry = sp.GetRequiredService<AgentRegistry>();

            // Get sub-agents and expose them as tools
            var searchAgent = registry.GetAgent(Agents.Tools.WebSearchAgent.Name);
            var summarizerAgent = registry.GetAgent(Agents.Basic.SummarizerAgent.Name);
            var translatorAgent = registry.GetAgent(Agents.Basic.TranslatorAgent.Name);

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un asistente de investigación. Tienes acceso a sub-agentes especializados:
                    1. WebSearch — Busca información en la web
                    2. Summarizer — Resume textos largos
                    3. Translator — Traduce texto entre idiomas
                    
                    Para completar tareas de investigación:
                    - Usa WebSearch para recopilar información
                    - Usa Summarizer para crear resúmenes concisos
                    - Usa Translator cuando el contenido necesite estar en otro idioma
                    
                    Siempre sintetiza los hallazgos en una respuesta coherente.
                    """,
                tools:
                [
                    searchAgent.AsAIFunction(),
                    summarizerAgent.AsAIFunction(),
                    translatorAgent.AsAIFunction()
                ]);
        }
    };
}
