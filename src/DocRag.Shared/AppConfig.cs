namespace DocRag.Shared;

/// <summary>
/// Shared configuration written by DocRag.Admin and read by DocRag.Server on startup.
/// Persisted as JSON at a well-known path (see ConfigPaths).
/// </summary>
public sealed class AppConfig
{
    public List<string> DocumentRoots { get; set; } = new();

    public string LmStudioBaseUrl { get; set; } = "http://localhost:1234";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string ChatModel { get; set; } = "local-model";

    public string QdrantHost { get; set; } = "localhost";
    public int QdrantGrpcPort { get; set; } = 6334;
    public int QdrantHttpPort { get; set; } = 6333;
    public string QdrantCollectionName { get; set; } = "docrag_chunks";

    public int ServerPort { get; set; } = 5178;

    /// <summary>Optional explicit path to qdrant.exe. Empty = auto-detect.</summary>
    public string QdrantExePath { get; set; } = "";

    /// <summary>Optional explicit path to DocRag.Server.exe. Empty = auto-detect.</summary>
    public string ServerExePath { get; set; } = "";

    public int ChunkSizeChars { get; set; } = 1000;
    public int ChunkOverlapChars { get; set; } = 150;
    public int EmbeddingBatchSize { get; set; } = 16;
    public int EmbeddingMaxParallelism { get; set; } = 2;

    public int RetrievalTopK { get; set; } = 6;

    /// <summary>
    /// Context window (tokens) the chat model is loaded with in LM Studio. 0 = detect at runtime from LM Studio.
    /// The server clamps the context budget to what fits into this window, so a too large
    /// <see cref="RetrievalMaxContextChars"/> can't overflow the model.
    /// </summary>
    public int ChatContextTokens { get; set; } = 0;

    /// <summary>Character budget for the context sent to the chat model; hits are expanded to whole sections up to this size.</summary>
    public int RetrievalMaxContextChars { get; set; } = 12000;

    public static readonly string[] SupportedExtensions =
    {
        ".pdf", ".docx", ".xlsx", ".pptx", ".txt", ".md"
    };

    /// <summary>
    /// The Qdrant collection actually used at runtime, scoped to the embedding model.
    /// Different embedding models produce vectors of different dimensionality/semantics,
    /// so each model gets its own collection instead of silently mixing incompatible vectors.
    /// </summary>
    public string GetEffectiveCollectionName() =>
        $"{QdrantCollectionName}__{SanitizeForCollectionName(EmbeddingModel)}";

    private static string SanitizeForCollectionName(string value) =>
        new(value.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
}

public static class ConfigPaths
{
    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DocRag");

    public static string ConfigFilePath => Path.Combine(ConfigDirectory, "config.json");

    public static string DataDirectory => Path.Combine(ConfigDirectory, "data");

    public static string ManifestDbPath => Path.Combine(DataDirectory, "manifest.db");

    public static string LogsDirectory => Path.Combine(ConfigDirectory, "logs");
}
