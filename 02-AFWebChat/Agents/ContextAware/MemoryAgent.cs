using AFWebChat.ContextProviders;
using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.ContextAware;

public static class MemoryAgent
{
    public const string Name = "MemoryAgent";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Agente con memoria persistente — recuerda información entre conversaciones.",
        Category = "Contexto",
        Icon = "🧠",
        Color = "#e056a0",
        ContextProviders = ["ConversationMemory"],
        Tools = ["GenerateChart"],
        ExamplePrompts = ["Recuerda que mi nombre es Alex", "¿Qué te dije antes?", "¿Cuáles son mis preferencias?"],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();
            var memoryProvider = new ConversationMemoryProvider(chatClient);

            return chatClient.AsAIAgent(new ChatClientAgentOptions
            {
                Name = Name,
                ChatOptions = new()
                {
                    Instructions = """
                        Eres un asistente con memoria persistente. Recuerdas información
                        de conversaciones anteriores y la usas para dar respuestas personalizadas.
                        Presta atención a las preferencias del usuario, nombres, datos que comparta y detalles importantes.
                        Referencia la información recordada de forma natural cuando sea relevante.
                        Si el usuario te da datos numéricos que valga la pena visualizar, usa GenerateChart.
                        """,
                    Tools = [.. AIFunctionFactoryExtensions.CreateFromStatic<ChartPlugin>()]
                },
                AIContextProviders = [memoryProvider]
            });
        }
    };
}
