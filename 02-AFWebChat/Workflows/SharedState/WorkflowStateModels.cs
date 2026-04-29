namespace AFWebChat.Workflows.SharedState;

public class WorkflowStateModels
{
    public string Input { get; set; } = "";
    public string? ClassificationResult { get; set; }
    public string? AnalysisResult { get; set; }
    public double Confidence { get; set; }
}

public class DraftContent
{
    public string Text { get; set; } = "";
    public int Iteration { get; set; }
}

public class ReviewResult
{
    public bool Approved { get; set; }
    public string Feedback { get; set; } = "";
    public int Iteration { get; set; }
}

public class DocumentProcessingState
{
    public string FileName { get; set; } = "";
    public string? ExtractedText { get; set; }
    public List<string> Chunks { get; set; } = [];
    public string Status { get; set; } = "Pending";
}
