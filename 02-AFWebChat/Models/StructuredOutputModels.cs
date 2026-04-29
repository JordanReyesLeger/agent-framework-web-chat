using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AFWebChat.Models;

[Description("Extracted entities from text")]
public sealed class ExtractedEntities
{
    [JsonPropertyName("people")]
    public List<string> People { get; set; } = [];

    [JsonPropertyName("companies")]
    public List<string> Companies { get; set; } = [];

    [JsonPropertyName("dates")]
    public List<string> Dates { get; set; } = [];

    [JsonPropertyName("amounts")]
    public List<string> Amounts { get; set; } = [];
}

[Description("Sentiment analysis result")]
public sealed class SentimentResult
{
    [JsonPropertyName("sentiment")]
    public string Sentiment { get; set; } = "";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("emotions")]
    public List<string> Emotions { get; set; } = [];

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";
}

[Description("Code review result")]
public sealed class CodeReviewResult
{
    [JsonPropertyName("issues")]
    public List<CodeIssue> Issues { get; set; } = [];

    [JsonPropertyName("overallQuality")]
    public string OverallQuality { get; set; } = "";

    [JsonPropertyName("suggestions")]
    public List<string> Suggestions { get; set; } = [];
}

public sealed class CodeIssue
{
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("line")]
    public int? Line { get; set; }
}
