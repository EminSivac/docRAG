using DocRag.Shared;

namespace DocRag.Admin;

public partial class SettingsForm : Form
{
    public AppConfig ResultConfig { get; private set; }

    public SettingsForm(AppConfig current)
    {
        InitializeComponent();

        ResultConfig = Clone(current);
        LoadFromConfig(current);
    }

    private void LoadFromConfig(AppConfig current)
    {
        rootsList.Items.AddRange(current.DocumentRoots.ToArray());
        lmStudioUrlTextBox.Text = current.LmStudioBaseUrl;
        embeddingModelTextBox.Text = current.EmbeddingModel;
        chatModelTextBox.Text = current.ChatModel;
        serverPortNumeric.Value = current.ServerPort;
        qdrantHttpPortNumeric.Value = current.QdrantHttpPort;
        qdrantGrpcPortNumeric.Value = current.QdrantGrpcPort;
        chunkSizeNumeric.Value = current.ChunkSizeChars;
        chunkOverlapNumeric.Value = current.ChunkOverlapChars;
        topKNumeric.Value = current.RetrievalTopK;
        qdrantExePathTextBox.Text = current.QdrantExePath;
        serverExePathTextBox.Text = current.ServerExePath;
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
        ResultConfig.EmbeddingModel = embeddingModelTextBox.Text.Trim();
        ResultConfig.ChatModel = chatModelTextBox.Text.Trim();
        ResultConfig.ServerPort = (int)serverPortNumeric.Value;
        ResultConfig.QdrantHttpPort = (int)qdrantHttpPortNumeric.Value;
        ResultConfig.QdrantGrpcPort = (int)qdrantGrpcPortNumeric.Value;
        ResultConfig.ChunkSizeChars = (int)chunkSizeNumeric.Value;
        ResultConfig.ChunkOverlapChars = (int)chunkOverlapNumeric.Value;
        ResultConfig.RetrievalTopK = (int)topKNumeric.Value;
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
        QdrantExePath = source.QdrantExePath,
        ServerExePath = source.ServerExePath
    };
}
