using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Tools;

public static class FileManagerAgent
{
    public const string Name = "FileManager";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Gestiona archivos: subir, descargar, listar y eliminar archivos.",
        Category = "Herramientas",
        Icon = "📁",
        Color = "#16a085",
        Tools = ["UploadFile", "ListFiles", "DownloadFile", "DeleteFile"],
        ExamplePrompts = ["Lista todos los archivos subidos", "Sube un documento al servidor", "Muéstrame los archivos modificados recientemente"],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un asistente de gestión de archivos. Ayuda a los usuarios a gestionar sus archivos.
                    Puedes subir, descargar, listar y eliminar archivos.
                    Al subir, solicita el nombre del archivo y el contenido si no se proporcionan.
                    Confirma las operaciones antes de ejecutar acciones destructivas como eliminar.
                    """,
                tools: AIFunctionFactoryExtensions.CreateFromStatic<FileManagerPlugin>());
        }
    };
}
