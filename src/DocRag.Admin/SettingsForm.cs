using System.ComponentModel;
using System.Text.Json;
using DocRag.Shared;

namespace DocRag.Admin;

public partial class SettingsForm : Form
{
    public AppConfig ResultConfig { get; private set; }

    /// <summary>Only for the Visual Studio designer; runtime code passes the current config.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public SettingsForm() : this(new AppConfig())
    {
    }

    public SettingsForm(AppConfig current)
    {
        InitializeComponent();

        ResultConfig = Clone(current);
        LoadFromConfig(current);

        Shown += async (_, _) => await LoadModelsAsync();
        lmStudioUrlTextBox.Leave += async (_, _) => await LoadModelsAsync();
    }

    /// <summary>
    /// Fills the model dropdowns from LM Studio. The combo boxes stay editable, so a model
    /// that isn't listed (or LM Studio being offline) never blocks saving a value.
    /// </summary>
    private async Task LoadModelsAsync()
    {
        if (DesignMode)
        {
            return;
        }

        var baseUrl = lmStudioUrlTextBox.Text.Trim().TrimEnd('/');
        if (baseUrl.Length == 0)
        {
            return;
        }

        List<LmModel> models;
        try
        {
            models = await FetchModelsAsync(baseUrl);
        }
        catch
        {
            return;
        }

        if (IsDisposed)
        {
            return;
        }

        FillCombo(embeddingModelComboBox, models.Where(m => m.Type is null or "embeddings"));
        FillCombo(chatModelComboBox, models.Where(m => m.Type is null or "llm" or "vlm"));
    }

    private static async Task<List<LmModel>> FetchModelsAsync(string baseUrl)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        // LM Studio's native endpoint reports each model's type; the OpenAI-compatible one doesn't.
        foreach (var path in new[] { "/api/v0/models", "/v1/models" })
        {
            try
            {
                using var doc = JsonDocument.Parse(await http.GetStringAsync(baseUrl + path));
                var list = new List<LmModel>();
                foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
                {
                    var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;
                    list.Add(new LmModel(item.GetProperty("id").GetString() ?? "", type));
                }

                if (list.Count > 0)
                {
                    return list;
                }
            }
            catch
            {
                // try the next endpoint
            }
        }

        return new List<LmModel>();
    }

    private static void FillCombo(ComboBox combo, IEnumerable<LmModel> models)
    {
        var current = combo.Text;
        combo.BeginUpdate();
        combo.Items.Clear();
        combo.Items.AddRange(models.Select(m => m.Id).Where(id => id.Length > 0).Distinct().Cast<object>().ToArray());
        combo.Text = current;
        combo.EndUpdate();
    }

    private sealed record LmModel(string Id, string? Type);

    private void LoadFromConfig(AppConfig current)
    {
        rootsList.Items.AddRange(current.DocumentRoots.ToArray());
        lmStudioUrlTextBox.Text = current.LmStudioBaseUrl;
        embeddingModelComboBox.Text = current.EmbeddingModel;
        chatModelComboBox.Text = current.ChatModel;
        serverPortNumeric.Value = current.ServerPort;
        qdrantHttpPortNumeric.Value = current.QdrantHttpPort;
        qdrantGrpcPortNumeric.Value = current.QdrantGrpcPort;
        chunkSizeNumeric.Value = current.ChunkSizeChars;
        chunkOverlapNumeric.Value = current.ChunkOverlapChars;
        topKNumeric.Value = current.RetrievalTopK;
        batchSizeNumeric.Value = Math.Clamp(current.EmbeddingBatchSize, (int)batchSizeNumeric.Minimum, (int)batchSizeNumeric.Maximum);
        parallelismNumeric.Value = Math.Clamp(current.EmbeddingMaxParallelism, (int)parallelismNumeric.Minimum, (int)parallelismNumeric.Maximum);
        contextTokensNumeric.Value = Math.Clamp(current.ChatContextTokens, (int)contextTokensNumeric.Minimum, (int)contextTokensNumeric.Maximum);
        maxContextCharsNumeric.Value = Math.Clamp(current.RetrievalMaxContextChars, (int)maxContextCharsNumeric.Minimum, (int)maxContextCharsNumeric.Maximum);
        qdrantExePathTextBox.Text = current.QdrantExePath;
        serverExePathTextBox.Text = current.ServerExePath;
    }

    /// <summary>
    /// Reads the context window the chat model is currently loaded with from LM Studio and derives
    /// a matching context budget, number of hits and answer length from it.
    /// </summary>
    private async void ContextButton_Click(object? sender, EventArgs e)
    {
        var baseUrl = lmStudioUrlTextBox.Text.Trim();
        if (baseUrl.Length == 0)
        {
            contextStatusLabel.Text = "Bitte zuerst die LM-Studio-URL eintragen.";
            return;
        }

        contextButton.Enabled = false;
        contextStatusLabel.Text = "Frage LM Studio ab …";
        try
        {
            var info = await LmStudioProbe.FindChatModelAsync(baseUrl, chatModelComboBox.Text.Trim());
            if (info is null)
            {
                contextStatusLabel.Text = "LM Studio nicht erreichbar oder kein Chat-Modell gefunden.";
                return;
            }

            if (info.LoadedContext is not > 0)
            {
                contextStatusLabel.Text = info.MaxContext is > 0
                    ? $"„{info.Id}“ ist nicht geladen (Modell-Maximum: {info.MaxContext:N0} Token). " +
                      "Modell in LM Studio mit der gewünschten Context Length laden und erneut ermitteln."
                    : $"„{info.Id}“ ist nicht geladen – Kontext unbekannt.";
                return;
            }

            var settings = ContextOptimizer.Compute(info.LoadedContext.Value);
            contextTokensNumeric.Value = Math.Clamp(settings.ContextTokens, (int)contextTokensNumeric.Minimum, (int)contextTokensNumeric.Maximum);
            maxContextCharsNumeric.Value = Math.Clamp(settings.RetrievalMaxContextChars, (int)maxContextCharsNumeric.Minimum, (int)maxContextCharsNumeric.Maximum);
            topKNumeric.Value = Math.Clamp(settings.RetrievalTopK, (int)topKNumeric.Minimum, (int)topKNumeric.Maximum);

            var hint = info.MaxContext is > 0 && info.MaxContext > info.LoadedContext
                ? $" Das Modell unterstützt bis {info.MaxContext:N0} Token – für mehr Kontext in LM Studio die Context Length erhöhen."
                : "";
            contextStatusLabel.Text =
                $"„{info.Id}“ läuft mit {info.LoadedContext:N0} Token → {settings.RetrievalMaxContextChars:N0} Zeichen Auszüge, " +
                $"Top-K {settings.RetrievalTopK}, Antwort bis {settings.MaxOutputTokens:N0} Token. Mit Speichern übernehmen.{hint}";
        }
        finally
        {
            contextButton.Enabled = true;
        }
    }

    private CancellationTokenSource? _benchmarkCts;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _benchmarkCts?.Cancel();
        base.OnFormClosing(e);
    }

    private async void OptimizeButton_Click(object? sender, EventArgs e)
    {
        if (_benchmarkCts is not null)
        {
            _benchmarkCts.Cancel();
            return;
        }

        var baseUrl = lmStudioUrlTextBox.Text.Trim();
        var model = embeddingModelComboBox.Text.Trim();
        if (baseUrl.Length == 0 || model.Length == 0)
        {
            optimizeStatusLabel.Text = "Bitte zuerst LM-Studio-URL und Embedding-Modell wählen.";
            return;
        }

        _benchmarkCts = new CancellationTokenSource();
        optimizeButton.Text = "Abbrechen";
        optimizeStatusLabel.Text = "Starte Test … (parallele Indexierung währenddessen verfälscht das Ergebnis)";

        try
        {
            var progress = new Progress<string>(text => optimizeStatusLabel.Text = text);
            var result = await EmbeddingBenchmark.RunAsync(
                baseUrl, model, (int)chunkSizeNumeric.Value, progress, _benchmarkCts.Token);

            batchSizeNumeric.Value = Math.Clamp(result.BatchSize, (int)batchSizeNumeric.Minimum, (int)batchSizeNumeric.Maximum);
            parallelismNumeric.Value = Math.Clamp(result.Parallelism, (int)parallelismNumeric.Minimum, (int)parallelismNumeric.Maximum);

            var details = string.Join(", ", result.Runs.Select(r => $"B{r.BatchSize}×P{r.Parallelism}: {r.ChunksPerSecond:0.0}/s"));
            optimizeStatusLabel.Text =
                $"Ergebnis: Batch {result.BatchSize}, {result.Parallelism} parallel (~{result.ChunksPerSecond:0.0} Chunks/s). " +
                $"Mit OK speichern. Messungen: {details}";
        }
        catch (OperationCanceledException)
        {
            optimizeStatusLabel.Text = "Test abgebrochen.";
        }
        catch (Exception ex)
        {
            optimizeStatusLabel.Text = $"Test fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            _benchmarkCts?.Dispose();
            _benchmarkCts = null;
            optimizeButton.Text = "Optimale Einstellungen ermitteln";
        }
    }

    private void AddRootButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK && !rootsList.Items.Contains(dialog.SelectedPath))
        {
            rootsList.Items.Add(dialog.SelectedPath);
        }
    }

    private void RemoveRootButton_Click(object? sender, EventArgs e)
    {
        if (rootsList.SelectedItem is not null)
        {
            rootsList.Items.Remove(rootsList.SelectedItem);
        }
    }

    private void QdrantExePathBrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = "qdrant.exe|qdrant.exe" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            qdrantExePathTextBox.Text = dialog.FileName;
        }
    }

    private void ServerExePathBrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = "DocRag.Server.exe|DocRag.Server.exe" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            serverExePathTextBox.Text = dialog.FileName;
        }
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        ResultConfig.DocumentRoots = rootsList.Items.Cast<string>().ToList();
        ResultConfig.LmStudioBaseUrl = lmStudioUrlTextBox.Text.Trim();
        ResultConfig.EmbeddingModel = embeddingModelComboBox.Text.Trim();
        ResultConfig.ChatModel = chatModelComboBox.Text.Trim();
        ResultConfig.ServerPort = (int)serverPortNumeric.Value;
        ResultConfig.QdrantHttpPort = (int)qdrantHttpPortNumeric.Value;
        ResultConfig.QdrantGrpcPort = (int)qdrantGrpcPortNumeric.Value;
        ResultConfig.ChunkSizeChars = (int)chunkSizeNumeric.Value;
        ResultConfig.ChunkOverlapChars = (int)chunkOverlapNumeric.Value;
        ResultConfig.RetrievalTopK = (int)topKNumeric.Value;
        ResultConfig.EmbeddingBatchSize = (int)batchSizeNumeric.Value;
        ResultConfig.EmbeddingMaxParallelism = (int)parallelismNumeric.Value;
        ResultConfig.ChatContextTokens = (int)contextTokensNumeric.Value;
        ResultConfig.RetrievalMaxContextChars = (int)maxContextCharsNumeric.Value;
        ResultConfig.QdrantExePath = qdrantExePathTextBox.Text.Trim();
        ResultConfig.ServerExePath = serverExePathTextBox.Text.Trim();
    }

    private static AppConfig Clone(AppConfig source) => new()
    {
        DocumentRoots = new List<string>(source.DocumentRoots),
        LmStudioBaseUrl = source.LmStudioBaseUrl,
        EmbeddingModel = source.EmbeddingModel,
        ChatModel = source.ChatModel,
        QdrantHost = source.QdrantHost,
        QdrantGrpcPort = source.QdrantGrpcPort,
        QdrantHttpPort = source.QdrantHttpPort,
        QdrantCollectionName = source.QdrantCollectionName,
        ServerPort = source.ServerPort,
        ChunkSizeChars = source.ChunkSizeChars,
        ChunkOverlapChars = source.ChunkOverlapChars,
        EmbeddingBatchSize = source.EmbeddingBatchSize,
        EmbeddingMaxParallelism = source.EmbeddingMaxParallelism,
        RetrievalTopK = source.RetrievalTopK,
        RetrievalMaxContextChars = source.RetrievalMaxContextChars,
        ChatContextTokens = source.ChatContextTokens,
        QdrantExePath = source.QdrantExePath,
        ServerExePath = source.ServerExePath
    };
}
