using System.ComponentModel;

namespace AFWebChat.Tools.Plugins;

public class WebSearchPlugin
{
    [Description("Search the web for information on a given query")]
    public static string SearchWeb(
        [Description("The search query")] string query)
    {
        // Simulated web search results for demo
        return $"Web search results for '{query}':\n" +
               $"1. [Wikipedia] Information about {query} - Overview and key facts...\n" +
               $"2. [Microsoft Learn] Documentation related to {query}...\n" +
               $"3. [Stack Overflow] Common questions about {query}...\n" +
               "Note: This is a simulated search. Connect Bing Search API for real results.";
    }

    [Description("Fetch the content of a web URL")]
    public static string FetchUrl(
        [Description("The URL to fetch content from")] string url)
    {
        // Simulated URL fetch for demo
        return $"Content fetched from {url}:\n" +
               "This is simulated content. In production, this would fetch the actual page content.";
    }
}
