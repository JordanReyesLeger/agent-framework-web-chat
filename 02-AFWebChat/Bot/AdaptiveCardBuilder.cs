using AdaptiveCards;
using AFWebChat.Models;
using Microsoft.Agents.Core.Models;
using System.Text.Json;

namespace AFWebChat.Bot;

/// <summary>
/// Builds Adaptive Cards for rich Teams interactions.
/// </summary>
public static class AdaptiveCardBuilder
{
    /// <summary>
    /// Creates a welcome card shown when a user first interacts with the bot.
    /// </summary>
    public static Attachment CreateWelcomeCard()
    {
        var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 5))
        {
            Body =
            [
                new AdaptiveColumnSet
                {
                    Columns =
                    [
                        new AdaptiveColumn
                        {
                            Width = "auto",
                            Items =
                            [
                                new AdaptiveImage
                                {
                                    Url = new Uri("https://img.icons8.com/fluency/96/robot-2.png"),
                                    Size = AdaptiveImageSize.Medium,
                                    Style = AdaptiveImageStyle.Default
                                }
                            ]
                        },
                        new AdaptiveColumn
                        {
                            Width = "stretch",
                            Items =
                            [
                                new AdaptiveTextBlock
                                {
                                    Text = "AF-WebChat Multi-Agent Assistant",
                                    Weight = AdaptiveTextWeight.Bolder,
                                    Size = AdaptiveTextSize.Large,
                                    Wrap = true
                                },
                                new AdaptiveTextBlock
                                {
                                    Text = "Powered by Microsoft Agent Framework",
                                    IsSubtle = true,
                                    Spacing = AdaptiveSpacing.None,
                                    Wrap = true
                                }
                            ]
                        }
                    ]
                },
                new AdaptiveTextBlock
                {
                    Text = "I'm a multi-agent AI assistant. Select an action below or type your message to start chatting.",
                    Wrap = true,
                    Spacing = AdaptiveSpacing.Medium
                }
            ],
            Actions =
            [
                new AdaptiveSubmitAction
                {
                    Title = "📋 List Agents",
                    Data = new { command = "/agents" }
                },
                new AdaptiveSubmitAction
                {
                    Title = "💬 Start Chatting",
                    Data = new { command = "/help" }
                }
            ]
        };

        return CreateAttachment(card);
    }

    /// <summary>
    /// Creates a card listing available agents with selection buttons.
    /// </summary>
    public static Attachment CreateAgentListCard(IEnumerable<AgentInfo> agents)
    {
        var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 5))
        {
            Body =
            [
                new AdaptiveTextBlock
                {
                    Text = "📋 Available Agents",
                    Weight = AdaptiveTextWeight.Bolder,
                    Size = AdaptiveTextSize.Large,
                    Wrap = true
                },
                new AdaptiveTextBlock
                {
                    Text = "Select an agent to start a conversation:",
                    IsSubtle = true,
                    Wrap = true
                }
            ]
        };

        // Group agents by category
        var grouped = agents
            .GroupBy(a => a.Category)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            card.Body.Add(new AdaptiveTextBlock
            {
                Text = $"**{group.Key}**",
                Spacing = AdaptiveSpacing.Medium,
                Wrap = true
            });

            foreach (var agent in group)
            {
                card.Body.Add(new AdaptiveColumnSet
                {
                    Columns =
                    [
                        new AdaptiveColumn
                        {
                            Width = "auto",
                            Items =
                            [
                                new AdaptiveTextBlock { Text = agent.Icon, Size = AdaptiveTextSize.Medium }
                            ],
                            VerticalContentAlignment = AdaptiveVerticalContentAlignment.Center
                        },
                        new AdaptiveColumn
                        {
                            Width = "stretch",
                            Items =
                            [
                                new AdaptiveTextBlock
                                {
                                    Text = $"**{agent.Name}**",
                                    Wrap = true
                                },
                                new AdaptiveTextBlock
                                {
                                    Text = agent.Description,
                                    IsSubtle = true,
                                    Spacing = AdaptiveSpacing.None,
                                    Size = AdaptiveTextSize.Small,
                                    Wrap = true
                                }
                            ]
                        },
                        new AdaptiveColumn
                        {
                            Width = "auto",
                            Items =
                            [
                                new AdaptiveActionSet
                                {
                                    Actions =
                                    [
                                        new AdaptiveSubmitAction
                                        {
                                            Title = "Select",
                                            Data = new { command = $"/agent {agent.Name}" }
                                        }
                                    ]
                                }
                            ],
                            VerticalContentAlignment = AdaptiveVerticalContentAlignment.Center
                        }
                    ]
                });
            }
        }

        return CreateAttachment(card);
    }

    /// <summary>
    /// Creates a card showing the agent switch confirmation.
    /// </summary>
    public static Attachment CreateAgentSwitchCard(string agentName, string icon, string description)
    {
        var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 5))
        {
            Body =
            [
                new AdaptiveColumnSet
                {
                    Columns =
                    [
                        new AdaptiveColumn
                        {
                            Width = "auto",
                            Items =
                            [
                                new AdaptiveTextBlock { Text = icon, Size = AdaptiveTextSize.ExtraLarge }
                            ]
                        },
                        new AdaptiveColumn
                        {
                            Width = "stretch",
                            Items =
                            [
                                new AdaptiveTextBlock
                                {
                                    Text = $"Switched to **{agentName}**",
                                    Weight = AdaptiveTextWeight.Bolder,
                                    Size = AdaptiveTextSize.Medium,
                                    Wrap = true
                                },
                                new AdaptiveTextBlock
                                {
                                    Text = description,
                                    IsSubtle = true,
                                    Spacing = AdaptiveSpacing.None,
                                    Wrap = true
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        return CreateAttachment(card);
    }

    /// <summary>
    /// Creates a response card with the agent's answer, showing which agent responded.
    /// </summary>
    public static Attachment CreateResponseCard(string agentName, string icon, string responseText)
    {
        var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 5))
        {
            Body =
            [
                new AdaptiveColumnSet
                {
                    Columns =
                    [
                        new AdaptiveColumn
                        {
                            Width = "auto",
                            Items =
                            [
                                new AdaptiveTextBlock { Text = icon, Size = AdaptiveTextSize.Small }
                            ],
                            VerticalContentAlignment = AdaptiveVerticalContentAlignment.Top
                        },
                        new AdaptiveColumn
                        {
                            Width = "stretch",
                            Items =
                            [
                                new AdaptiveTextBlock
                                {
                                    Text = $"**{agentName}**",
                                    Size = AdaptiveTextSize.Small,
                                    IsSubtle = true,
                                    Spacing = AdaptiveSpacing.None
                                },
                                new AdaptiveTextBlock
                                {
                                    Text = responseText,
                                    Wrap = true,
                                    Spacing = AdaptiveSpacing.Small
                                }
                            ]
                        }
                    ]
                }
            ],
            Actions =
            [
                new AdaptiveSubmitAction
                {
                    Title = "🔄 Switch Agent",
                    Data = new { command = "/agents" }
                }
            ]
        };

        return CreateAttachment(card);
    }

    /// <summary>
    /// Creates a proactive notification card.
    /// </summary>
    public static Attachment CreateNotificationCard(string title, string message, string? severity = null)
    {
        var accentColor = severity switch
        {
            "warning" => AdaptiveTextColor.Warning,
            "error" => AdaptiveTextColor.Attention,
            "success" => AdaptiveTextColor.Good,
            _ => AdaptiveTextColor.Accent
        };

        var icon = severity switch
        {
            "warning" => "⚠️",
            "error" => "🚨",
            "success" => "✅",
            _ => "🔔"
        };

        var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 5))
        {
            Body =
            [
                new AdaptiveContainer
                {
                    Style = AdaptiveContainerStyle.Emphasis,
                    Items =
                    [
                        new AdaptiveColumnSet
                        {
                            Columns =
                            [
                                new AdaptiveColumn
                                {
                                    Width = "auto",
                                    Items =
                                    [
                                        new AdaptiveTextBlock
                                        {
                                            Text = icon,
                                            Size = AdaptiveTextSize.Large
                                        }
                                    ]
                                },
                                new AdaptiveColumn
                                {
                                    Width = "stretch",
                                    Items =
                                    [
                                        new AdaptiveTextBlock
                                        {
                                            Text = title,
                                            Weight = AdaptiveTextWeight.Bolder,
                                            Size = AdaptiveTextSize.Medium,
                                            Color = accentColor,
                                            Wrap = true
                                        },
                                        new AdaptiveTextBlock
                                        {
                                            Text = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC"),
                                            IsSubtle = true,
                                            Size = AdaptiveTextSize.Small,
                                            Spacing = AdaptiveSpacing.None
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                },
                new AdaptiveTextBlock
                {
                    Text = message,
                    Wrap = true,
                    Spacing = AdaptiveSpacing.Medium
                }
            ],
            Actions =
            [
                new AdaptiveSubmitAction
                {
                    Title = "👍 Acknowledge",
                    Data = new { command = "ack", notificationId = Guid.NewGuid().ToString() }
                }
            ]
        };

        return CreateAttachment(card);
    }

    /// <summary>
    /// Creates a help card listing commands.
    /// </summary>
    public static Attachment CreateHelpCard()
    {
        var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 5))
        {
            Body =
            [
                new AdaptiveTextBlock
                {
                    Text = "📖 Available Commands",
                    Weight = AdaptiveTextWeight.Bolder,
                    Size = AdaptiveTextSize.Large,
                    Wrap = true
                },
                new AdaptiveFactSet
                {
                    Facts =
                    [
                        new AdaptiveFact("/agents", "List all available AI agents"),
                        new AdaptiveFact("/agent <name>", "Switch to a specific agent"),
                        new AdaptiveFact("/clear", "Clear chat history for the current agent"),
                        new AdaptiveFact("/new", "Start a fresh conversation (reset agent + history)"),
                        new AdaptiveFact("/help", "Show this help card"),
                        new AdaptiveFact("(any text)", "Chat with the current agent"),
                    ]
                },
                new AdaptiveTextBlock
                {
                    Text = "💡 **Tip:** Each agent has its own chat history. Switching agents doesn't lose your previous conversations.",
                    IsSubtle = true,
                    Wrap = true,
                    Spacing = AdaptiveSpacing.Medium
                }
            ]
        };

        return CreateAttachment(card);
    }

    private static Attachment CreateAttachment(AdaptiveCard card)
    {
        // Serialize to JSON string first, then parse to JsonElement.
        // This ensures the "version" field is serialized as "1.5" (string)
        // instead of {"major":1,"minor":5} (object) which Teams rejects.
        var jsonString = card.ToJson();
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);

        return new Attachment
        {
            ContentType = AdaptiveCard.ContentType,
            Content = jsonElement
        };
    }
}
