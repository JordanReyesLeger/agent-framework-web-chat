using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Tools;

public static class OrderStatusAgent
{
    public const string Name = "OrderStatus";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Consulta el estado de pedidos, rastreo y tiempos de entrega.",
        Category = "Herramientas",
        Icon = "📦",
        Color = "#e67e22",
        Tools = ["CheckOrderStatus"],
        ExamplePrompts = [
            "¿Cuál es el estado de mi pedido ORD-12345?",
            "¿Cuándo llega mi paquete?",
            "Rastrear pedido ORD-67890"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var chatClient = sp.GetRequiredService<ChatClientFactory>().CreateChatClient();
            var tools = new List<AITool>();
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(new OrderStatusPlugin()));

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un asistente de servicio al cliente especializado en estados de pedidos.
                    Ayudas a los usuarios a verificar el estado de sus pedidos.
                    Usa la función CheckOrderStatus cuando el usuario pregunte por un pedido.
                    Sé amable y profesional. Responde en español.
                    """,
                tools: tools);
        }
    };
}

public static class OrderReturnAgent
{
    public const string Name = "OrderReturn";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Procesa devoluciones de pedidos. Gestiona solicitudes de devolución.",
        Category = "Herramientas",
        Icon = "↩️",
        Color = "#c0392b",
        Tools = ["ProcessReturn"],
        ExamplePrompts = [
            "Quiero devolver mi pedido ORD-12345",
            "El producto llegó dañado, necesito devolverlo",
            "Procesar devolución del pedido ORD-67890"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var chatClient = sp.GetRequiredService<ChatClientFactory>().CreateChatClient();
            var tools = new List<AITool>();
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(new OrderReturnPlugin()));

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un asistente de devoluciones. Ayudas a los usuarios a procesar devoluciones
                    de pedidos. Solicita el ID del pedido y la razón de la devolución.
                    Sé empático y profesional. Responde en español.
                    """,
                tools: tools);
        }
    };
}

public static class OrderRefundAgent
{
    public const string Name = "OrderRefund";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Procesa reembolsos de pedidos. Gestiona solicitudes de reembolso.",
        Category = "Herramientas",
        Icon = "💸",
        Color = "#27ae60",
        Tools = ["ProcessRefund"],
        ExamplePrompts = [
            "Necesito un reembolso por mi pedido ORD-12345",
            "Me cobraron de más, quiero un reembolso",
            "Solicitar reembolso del pedido ORD-67890"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var chatClient = sp.GetRequiredService<ChatClientFactory>().CreateChatClient();
            var tools = new List<AITool>();
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(new OrderRefundPlugin()));

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un asistente de reembolsos. Ayudas a los usuarios a procesar reembolsos
                    de pedidos. Solicita el ID del pedido y la razón del reembolso.
                    Sé empático y profesional. Responde en español.
                    """,
                tools: tools);
        }
    };
}
