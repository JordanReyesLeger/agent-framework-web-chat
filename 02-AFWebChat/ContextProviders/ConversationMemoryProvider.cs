using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.ContextProviders;

public class ConversationMemoryProvider : AIContextProvider
{
    private readonly ProviderSessionState<MemoryState> _sessionState;
    private IReadOnlyList<string>? _stateKeys;
    private readonly IChatClient _chatClient;

    public ConversationMemoryProvider(IChatClient chatClient)
    {
        _sessionState = new ProviderSessionState<MemoryState>(
            _ => new MemoryState(),
            nameof(ConversationMemoryProvider));
        _chatClient = chatClient;
    }

    public override IReadOnlyList<string> StateKeys =>
        _stateKeys ??= [_sessionState.StateKey];

    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        var state = _sessionState.GetOrInitializeState(context.Session);

        var instructions = new StringBuilder();
        if (state.Memories.Count > 0)
        {
            instructions.AppendLine("Context from previous conversations:");
            foreach (var memory in state.Memories.TakeLast(10))
            {
                instructions.AppendLine($"- {memory}");
            }
        }

        return new ValueTask<AIContext>(new AIContext
        {
            Instructions = instructions.ToString()
        });
    }

    protected override async ValueTask StoreAIContextAsync(
        InvokedContext context,
        CancellationToken cancellationToken = default)
    {
        var state = _sessionState.GetOrInitializeState(context.Session);

        // Extract key facts from user messages
        var userMessages = context.RequestMessages
            .Where(m => m.Role == ChatRole.User)
            .Select(m => m.Text)
            .Where(t => !string.IsNullOrEmpty(t));

        foreach (var msg in userMessages)
        {
            if (msg!.Length > 20) // Only store meaningful messages
            {
                try
                {
                    var result = await _chatClient.GetResponseAsync(
                        [new ChatMessage(ChatRole.User, msg)],
                        new ChatOptions
                        {
                            Instructions = "Extract one key fact or preference from this message. Respond with just the fact, or 'NONE' if there's nothing worth remembering."
                        },
                        cancellationToken: cancellationToken);

                    var fact = result.Text?.Trim();
                    if (!string.IsNullOrEmpty(fact) && !fact.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                    {
                        state.Memories.Add(fact);
                    }
                }
                catch
                {
                    // Silently skip if extraction fails
                }
            }
        }

        _sessionState.SaveState(context.Session, state);
    }
}

public class MemoryState
{
    public List<string> Memories { get; set; } = [];
}
