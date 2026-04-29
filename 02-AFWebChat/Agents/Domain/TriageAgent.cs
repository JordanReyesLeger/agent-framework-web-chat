using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Domain;

public static class TriageAgent
{
    public const string Name = "Triage";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Agente de triage que clasifica solicitudes de clientes y las dirige al especialista adecuado en flujos Handoff.",
        Category = "Dominio",
        Icon = "🎯",
        Color = "#8e44ad",
        ExamplePrompts = [
            "Necesito ayuda con un pedido",
            "Quiero un reembolso",
            "¿Puedo devolver un producto?"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var chatClient = sp.GetRequiredService<ChatClientFactory>().CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un agente de triage para servicio al cliente. Tu trabajo es entender
                    la intención del usuario y dirigirlo al agente especialista correcto.
                    
                    Clasificación:
                    - Consultas sobre estados de pedidos → OrderStatus
                    - Solicitudes de devolución → OrderReturn  
                    - Solicitudes de reembolso → OrderRefund
                    - Preguntas generales → GeneralAssistant
                    
                    Sé amable, confirma la intención del usuario y redirige apropiadamente.
                    Responde en español.
                    """);
        }
    };
}
