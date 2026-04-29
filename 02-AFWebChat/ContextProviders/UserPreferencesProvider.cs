using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.ContextProviders;

public class UserPreferencesProvider : AIContextProvider
{
    private readonly ProviderSessionState<UserPreferences> _sessionState;

    public UserPreferencesProvider()
    {
        _sessionState = new ProviderSessionState<UserPreferences>(
            _ => new UserPreferences(),
            nameof(UserPreferencesProvider));
    }

    public override IReadOnlyList<string> StateKeys => [_sessionState.StateKey];

    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        var prefs = _sessionState.GetOrInitializeState(context.Session);

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(prefs.Language))
            sb.AppendLine($"User's preferred language: {prefs.Language}");
        if (!string.IsNullOrEmpty(prefs.Tone))
            sb.AppendLine($"User's preferred tone: {prefs.Tone}");
        if (!string.IsNullOrEmpty(prefs.TechLevel))
            sb.AppendLine($"User's technical level: {prefs.TechLevel}");

        return new ValueTask<AIContext>(new AIContext
        {
            Instructions = sb.ToString()
        });
    }
}

public class UserPreferences
{
    public string? Language { get; set; }
    public string? Tone { get; set; }
    public string? TechLevel { get; set; }
}
