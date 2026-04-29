using System.ComponentModel;
using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Approval;

public static class DataModifierAgent
{
    public const string Name = "DataModifier";

    [Description("Insert a new record into a database table")]
    public static string InsertRecord(
        [Description("The table name")] string table,
        [Description("The data to insert as key=value pairs")] string data)
    {
        return $"Record inserted into [{table}]: {data}";
    }

    [Description("Update an existing record in a database table")]
    public static string UpdateRecord(
        [Description("The table name")] string table,
        [Description("The WHERE condition")] string condition,
        [Description("The data to update as key=value pairs")] string data)
    {
        return $"Record updated in [{table}] WHERE {condition}: {data}";
    }

    [Description("Delete a record from a database table")]
    public static string DeleteRecord(
        [Description("The table name")] string table,
        [Description("The WHERE condition")] string condition)
    {
        return $"Record deleted from [{table}] WHERE {condition}";
    }

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Modifica registros de base de datos (INSERT, UPDATE, DELETE) con aprobación humana requerida.",
        Category = "Aprobación",
        Icon = "⚠️",
        Color = "#e74c3c",
        Tools = ["InsertRecord ⚠️", "UpdateRecord ⚠️", "DeleteRecord ⚠️"],
        ExamplePrompts = ["Inserta un nuevo registro de cliente", "Actualiza el estado del pedido #12345", "Elimina todas las cuentas inactivas del año pasado"],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            var insertTool = AIFunctionFactory.Create(InsertRecord, name: nameof(InsertRecord));
            var updateTool = AIFunctionFactory.Create(UpdateRecord, name: nameof(UpdateRecord));
            var deleteTool = AIFunctionFactory.Create(DeleteRecord, name: nameof(DeleteRecord));

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un agente de modificación de base de datos. Puedes insertar, actualizar y eliminar registros.
                    IMPORTANTE: Todas las operaciones requieren aprobación explícita del usuario antes de ejecutarse.
                    1. Siempre explica lo que vas a hacer antes de usar cualquier herramienta.
                    2. Espera a que el usuario confirme antes de proceder.
                    3. Muestra los datos exactos que se modificarán.
                    4. Después de la ejecución, confirma el resultado.
                    """,
                tools:
                [
                    new ApprovalRequiredAIFunction(insertTool),
                    new ApprovalRequiredAIFunction(updateTool),
                    new ApprovalRequiredAIFunction(deleteTool)
                ]);
        }
    };
}
