using System.ComponentModel;

namespace AFWebChat.Tools.Plugins;

public sealed class OrderPlugin
{
    [Description("Realizar un pedido para el artículo especificado.")]
    public string PlaceOrder([Description("Nombre del artículo")] string itemName)
        => $"Pedido realizado exitosamente para: {itemName}";
}

public sealed class RefundPlugin
{
    [Description("Ejecutar un reembolso para el artículo especificado.")]
    public string ExecuteRefund([Description("Nombre del artículo")] string itemName)
        => $"Reembolso procesado exitosamente para: {itemName}";
}

public sealed class OrderStatusPlugin
{
    [Description("Verificar el estado de un pedido.")]
    public string CheckOrderStatus([Description("ID del pedido")] string orderId)
        => $"El pedido {orderId} ha sido enviado y llegará en 2-3 días.";
}

public sealed class OrderReturnPlugin
{
    [Description("Procesar la devolución de un pedido.")]
    public string ProcessReturn(
        [Description("ID del pedido")] string orderId,
        [Description("Razón de la devolución")] string reason)
        => $"La devolución del pedido {orderId} se ha procesado correctamente. Razón: {reason}";
}

public sealed class OrderRefundPlugin
{
    [Description("Procesar un reembolso para un pedido.")]
    public string ProcessRefund(
        [Description("ID del pedido")] string orderId,
        [Description("Razón del reembolso")] string reason)
        => $"El reembolso del pedido {orderId} se ha procesado correctamente. Razón: {reason}";
}
