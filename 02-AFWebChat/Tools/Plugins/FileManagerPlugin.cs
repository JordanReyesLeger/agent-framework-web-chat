using System.Collections.Concurrent;
using System.ComponentModel;

namespace AFWebChat.Tools.Plugins;

public class FileManagerPlugin
{
    private static readonly ConcurrentDictionary<string, string> _files = new();

    [Description("Upload a file with the given name and content")]
    public static string UploadFile(
        [Description("The name of the file")] string name,
        [Description("The content of the file")] string content)
    {
        _files[name] = content;
        return $"File '{name}' uploaded successfully ({content.Length} characters).";
    }

    [Description("List all uploaded files")]
    public static string ListFiles()
    {
        if (_files.IsEmpty)
            return "No files uploaded yet.";

        var lines = _files.Select(kv => $"  - {kv.Key} ({kv.Value.Length} chars)");
        return "Uploaded files:\n" + string.Join("\n", lines);
    }

    [Description("Download a file by name and return its content")]
    public static string DownloadFile(
        [Description("The name of the file to download")] string name)
    {
        return _files.TryGetValue(name, out var content)
            ? $"Content of '{name}':\n{content}"
            : $"File '{name}' not found.";
    }

    [Description("Delete a file by name")]
    public static string DeleteFile(
        [Description("The name of the file to delete")] string name)
    {
        return _files.TryRemove(name, out _)
            ? $"File '{name}' deleted successfully."
            : $"File '{name}' not found.";
    }
}
