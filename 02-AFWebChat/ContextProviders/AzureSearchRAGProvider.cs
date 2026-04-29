using System.Text;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.ContextProviders;

public class AzureSearchRAGProvider : AIContextProvider
{
    private readonly SearchClient? _searchClient;
    private readonly ILogger<AzureSearchRAGProvider> _logger;

    public AzureSearchRAGProvider(IConfiguration config, ILogger<AzureSearchRAGProvider> logger)
    {
        _logger = logger;

        var endpoint = config["AzureSearch:Endpoint"];
        var indexName = config["AzureSearch:IndexName"];

        if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(indexName))
        {
            _searchClient = new SearchClient(
                new Uri(endpoint),
                indexName,
                new Azure.Identity.DefaultAzureCredential());
        }
    }

    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        if (_searchClient is null)
        {
            return new ValueTask<AIContext>(new AIContext
            {
                Instructions = "Azure Search is not configured. RAG capabilities are unavailable."
            });
        }

        // Note: InvokingContext doesn't have RequestMessages. The actual search
        // would be triggered via tools or StoreAIContextAsync for post-invocation indexing.
        return new ValueTask<AIContext>(new AIContext
        {
            Instructions = "You have access to a document index via Azure AI Search. " +
                          "Use the provided context to answer questions based on indexed documents."
        });

    }
}
