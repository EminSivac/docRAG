namespace DocRag.Shared;

public sealed class ChatRequest
{
    public string Question { get; set; } = "";

    /// <summary>Deep research: several search queries, more sources, multi-step answer. Slower but more thorough.</summary>
    public bool Deep { get; set; }
}

public sealed class ChatResponse
{
    public string Answer { get; set; } = "";
    public List<SourceCitation> Sources { get; set; } = new();
    public bool Deep { get; set; }

    /// <summary>Search queries used (deep research only).</summary>
    public List<string> Queries { get; set; } = new();
    public double ElapsedSeconds { get; set; }
}

public sealed class SourceCitation
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string? Location { get; set; }
    public string Snippet { get; set; } = "";
    public float Score { get; set; }
}

public sealed class IngestStatus
{
    public bool IsRunning { get; set; }
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int SkippedFiles { get; set; }
    public int FailedFiles { get; set; }
    public int TotalChunksIndexed { get; set; }
    public string? CurrentFile { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public List<string> RecentErrors { get; set; } = new();
}
