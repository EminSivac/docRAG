namespace DocRag.Admin;

partial class SettingsForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components is not null)
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        layout = new TableLayoutPanel();

        rootsLabel = new Label();
        rootsPanel = new Panel();
        rootsList = new ListBox();
        rootsButtonsPanel = new FlowLayoutPanel();
        addRootButton = new Button();
        removeRootButton = new Button();

        lmStudioUrlLabel = new Label();
        lmStudioUrlTextBox = new TextBox();
        embeddingModelLabel = new Label();
        embeddingModelComboBox = new ComboBox();
        chatModelLabel = new Label();
        chatModelComboBox = new ComboBox();

        serverPortLabel = new Label();
        serverPortNumeric = new NumericUpDown();
        qdrantHttpPortLabel = new Label();
        qdrantHttpPortNumeric = new NumericUpDown();
        qdrantGrpcPortLabel = new Label();
        qdrantGrpcPortNumeric = new NumericUpDown();

        chunkSizeLabel = new Label();
        chunkSizeNumeric = new NumericUpDown();
        chunkOverlapLabel = new Label();
        chunkOverlapNumeric = new NumericUpDown();
        topKLabel = new Label();
        topKNumeric = new NumericUpDown();
        batchSizeLabel = new Label();
        batchSizeNumeric = new NumericUpDown();
        parallelismLabel = new Label();
        parallelismNumeric = new NumericUpDown();
        optimizePanel = new FlowLayoutPanel();
        optimizeButton = new Button();
        optimizeStatusLabel = new Label();

        contextTokensLabel = new Label();
        contextTokensNumeric = new NumericUpDown();
        maxContextCharsLabel = new Label();
        maxContextCharsNumeric = new NumericUpDown();
        contextPanel = new FlowLayoutPanel();
        contextButton = new Button();
        contextStatusLabel = new Label();

        qdrantExePathLabel = new Label();
        qdrantExePathPanel = new Panel();
        qdrantExePathTextBox = new TextBox();
        qdrantExePathBrowseButton = new Button();

        serverExePathLabel = new Label();
        serverExePathPanel = new Panel();
        serverExePathTextBox = new TextBox();
        serverExePathBrowseButton = new Button();

        buttonPanel = new FlowLayoutPanel();
        okButton = new Button();
        cancelButton = new Button();

        layout.SuspendLayout();
        rootsPanel.SuspendLayout();
        rootsButtonsPanel.SuspendLayout();
        qdrantExePathPanel.SuspendLayout();
        serverExePathPanel.SuspendLayout();
        buttonPanel.SuspendLayout();
        SuspendLayout();

        // layout
        layout.Dock = DockStyle.Fill;
        layout.ColumnCount = 2;
        layout.Padding = new Padding(12);
        layout.AutoScroll = true;
        layout.Name = "layout";
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowCount = 18;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // rootsLabel
        rootsLabel.AutoSize = true;
        rootsLabel.Anchor = AnchorStyles.Left;
        rootsLabel.Text = "Dokumentordner";
        layout.Controls.Add(rootsLabel, 0, 0);

        // rootsPanel
        rootsPanel.Dock = DockStyle.Fill;
        rootsPanel.Height = 110;
        rootsPanel.Controls.Add(rootsList);
        rootsPanel.Controls.Add(rootsButtonsPanel);
        layout.Controls.Add(rootsPanel, 1, 0);

        // rootsList
        rootsList.Dock = DockStyle.Top;
        rootsList.Height = 80;

        // rootsButtonsPanel
        rootsButtonsPanel.Dock = DockStyle.Bottom;
        rootsButtonsPanel.Height = 30;
        rootsButtonsPanel.Controls.Add(addRootButton);
        rootsButtonsPanel.Controls.Add(removeRootButton);

        // addRootButton
        addRootButton.AutoSize = true;
        addRootButton.Text = "Ordner hinzufügen";
        addRootButton.Click += AddRootButton_Click;

        // removeRootButton
        removeRootButton.AutoSize = true;
        removeRootButton.Text = "Entfernen";
        removeRootButton.Click += RemoveRootButton_Click;

        // lmStudioUrlLabel / lmStudioUrlTextBox
        lmStudioUrlLabel.AutoSize = true;
        lmStudioUrlLabel.Anchor = AnchorStyles.Left;
        lmStudioUrlLabel.Text = "LM Studio URL";
        layout.Controls.Add(lmStudioUrlLabel, 0, 1);
        lmStudioUrlTextBox.Dock = DockStyle.Fill;
        layout.Controls.Add(lmStudioUrlTextBox, 1, 1);

        // embeddingModelLabel / embeddingModelComboBox
        embeddingModelLabel.AutoSize = true;
        embeddingModelLabel.Anchor = AnchorStyles.Left;
        embeddingModelLabel.Text = "Embedding-Modell";
        layout.Controls.Add(embeddingModelLabel, 0, 2);
        embeddingModelComboBox.Dock = DockStyle.Fill;
        embeddingModelComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        layout.Controls.Add(embeddingModelComboBox, 1, 2);

        // chatModelLabel / chatModelComboBox
        chatModelLabel.AutoSize = true;
        chatModelLabel.Anchor = AnchorStyles.Left;
        chatModelLabel.Text = "Chat-Modell";
        layout.Controls.Add(chatModelLabel, 0, 3);
        chatModelComboBox.Dock = DockStyle.Fill;
        chatModelComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        layout.Controls.Add(chatModelComboBox, 1, 3);

        // serverPortLabel / serverPortNumeric
        serverPortLabel.AutoSize = true;
        serverPortLabel.Anchor = AnchorStyles.Left;
        serverPortLabel.Text = "Server-Port";
        layout.Controls.Add(serverPortLabel, 0, 4);
        serverPortNumeric.Minimum = 1;
        serverPortNumeric.Maximum = 65535;
        serverPortNumeric.Width = 120;
        layout.Controls.Add(serverPortNumeric, 1, 4);

        // qdrantHttpPortLabel / qdrantHttpPortNumeric
        qdrantHttpPortLabel.AutoSize = true;
        qdrantHttpPortLabel.Anchor = AnchorStyles.Left;
        qdrantHttpPortLabel.Text = "Qdrant HTTP-Port";
        layout.Controls.Add(qdrantHttpPortLabel, 0, 5);
        qdrantHttpPortNumeric.Minimum = 1;
        qdrantHttpPortNumeric.Maximum = 65535;
        qdrantHttpPortNumeric.Width = 120;
        layout.Controls.Add(qdrantHttpPortNumeric, 1, 5);

        // qdrantGrpcPortLabel / qdrantGrpcPortNumeric
        qdrantGrpcPortLabel.AutoSize = true;
        qdrantGrpcPortLabel.Anchor = AnchorStyles.Left;
        qdrantGrpcPortLabel.Text = "Qdrant gRPC-Port";
        layout.Controls.Add(qdrantGrpcPortLabel, 0, 6);
        qdrantGrpcPortNumeric.Minimum = 1;
        qdrantGrpcPortNumeric.Maximum = 65535;
        qdrantGrpcPortNumeric.Width = 120;
        layout.Controls.Add(qdrantGrpcPortNumeric, 1, 6);

        // chunkSizeLabel / chunkSizeNumeric
        chunkSizeLabel.AutoSize = true;
        chunkSizeLabel.Anchor = AnchorStyles.Left;
        chunkSizeLabel.Text = "Chunk-Größe (Zeichen)";
        layout.Controls.Add(chunkSizeLabel, 0, 7);
        chunkSizeNumeric.Minimum = 100;
        chunkSizeNumeric.Maximum = 10000;
        chunkSizeNumeric.Width = 120;
        layout.Controls.Add(chunkSizeNumeric, 1, 7);

        // chunkOverlapLabel / chunkOverlapNumeric
        chunkOverlapLabel.AutoSize = true;
        chunkOverlapLabel.Anchor = AnchorStyles.Left;
        chunkOverlapLabel.Text = "Chunk-Overlap (Zeichen)";
        layout.Controls.Add(chunkOverlapLabel, 0, 8);
        chunkOverlapNumeric.Minimum = 0;
        chunkOverlapNumeric.Maximum = 2000;
        chunkOverlapNumeric.Width = 120;
        layout.Controls.Add(chunkOverlapNumeric, 1, 8);

        // topKLabel / topKNumeric
        topKLabel.AutoSize = true;
        topKLabel.Anchor = AnchorStyles.Left;
        topKLabel.Text = "Top-K Treffer";
        layout.Controls.Add(topKLabel, 0, 9);
        topKNumeric.Minimum = 1;
        topKNumeric.Maximum = 50;
        topKNumeric.Width = 120;
        layout.Controls.Add(topKNumeric, 1, 9);

        // batchSizeLabel / batchSizeNumeric
        batchSizeLabel.AutoSize = true;
        batchSizeLabel.Anchor = AnchorStyles.Left;
        batchSizeLabel.Text = "Embedding-Batchgröße";
        layout.Controls.Add(batchSizeLabel, 0, 10);
        batchSizeNumeric.Minimum = 1;
        batchSizeNumeric.Maximum = 512;
        batchSizeNumeric.Width = 120;
        layout.Controls.Add(batchSizeNumeric, 1, 10);

        // parallelismLabel / parallelismNumeric
        parallelismLabel.AutoSize = true;
        parallelismLabel.Anchor = AnchorStyles.Left;
        parallelismLabel.Text = "Parallele Anfragen";
        layout.Controls.Add(parallelismLabel, 0, 11);
        parallelismNumeric.Minimum = 1;
        parallelismNumeric.Maximum = 32;
        parallelismNumeric.Width = 120;
        layout.Controls.Add(parallelismNumeric, 1, 11);

        // optimizeButton / optimizeStatusLabel
        optimizePanel.Dock = DockStyle.Fill;
        optimizePanel.AutoSize = true;
        optimizePanel.FlowDirection = FlowDirection.TopDown;
        optimizePanel.WrapContents = false;
        optimizePanel.Controls.Add(optimizeButton);
        optimizePanel.Controls.Add(optimizeStatusLabel);
        layout.Controls.Add(optimizePanel, 1, 12);
        optimizeButton.AutoSize = true;
        optimizeButton.Text = "Optimale Einstellungen ermitteln";
        optimizeButton.Click += OptimizeButton_Click;
        optimizeStatusLabel.AutoSize = true;
        optimizeStatusLabel.MaximumSize = new System.Drawing.Size(400, 0);

        // contextTokensLabel / contextTokensNumeric
        contextTokensLabel.AutoSize = true;
        contextTokensLabel.Anchor = AnchorStyles.Left;
        contextTokensLabel.Text = "Chat-Kontext (Token, 0 = auto)";
        layout.Controls.Add(contextTokensLabel, 0, 13);
        contextTokensNumeric.Minimum = 0;
        contextTokensNumeric.Maximum = 2000000;
        contextTokensNumeric.Increment = 1024;
        contextTokensNumeric.Width = 120;
        layout.Controls.Add(contextTokensNumeric, 1, 13);

        // maxContextCharsLabel / maxContextCharsNumeric
        maxContextCharsLabel.AutoSize = true;
        maxContextCharsLabel.Anchor = AnchorStyles.Left;
        maxContextCharsLabel.Text = "Max. Auszüge (Zeichen)";
        layout.Controls.Add(maxContextCharsLabel, 0, 14);
        maxContextCharsNumeric.Minimum = 1000;
        maxContextCharsNumeric.Maximum = 4000000;
        maxContextCharsNumeric.Increment = 1000;
        maxContextCharsNumeric.Width = 120;
        layout.Controls.Add(maxContextCharsNumeric, 1, 14);

        // contextButton / contextStatusLabel
        contextPanel.Dock = DockStyle.Fill;
        contextPanel.AutoSize = true;
        contextPanel.FlowDirection = FlowDirection.TopDown;
        contextPanel.WrapContents = false;
        contextPanel.Controls.Add(contextButton);
        contextPanel.Controls.Add(contextStatusLabel);
        layout.Controls.Add(contextPanel, 1, 15);
        contextButton.AutoSize = true;
        contextButton.Text = "Optimalen Kontext ermitteln";
        contextButton.Click += ContextButton_Click;
        contextStatusLabel.AutoSize = true;
        contextStatusLabel.MaximumSize = new System.Drawing.Size(400, 0);

        // qdrantExePathLabel / qdrantExePathPanel
        qdrantExePathLabel.AutoSize = true;
        qdrantExePathLabel.Anchor = AnchorStyles.Left;
        qdrantExePathLabel.Text = "qdrant.exe Pfad (optional)";
        layout.Controls.Add(qdrantExePathLabel, 0, 16);
        qdrantExePathPanel.Dock = DockStyle.Fill;
        qdrantExePathPanel.Height = 30;
        qdrantExePathPanel.Controls.Add(qdrantExePathTextBox);
        qdrantExePathPanel.Controls.Add(qdrantExePathBrowseButton);
        layout.Controls.Add(qdrantExePathPanel, 1, 16);
        qdrantExePathTextBox.Width = 320;
        qdrantExePathBrowseButton.Left = 326;
        qdrantExePathBrowseButton.Width = 30;
        qdrantExePathBrowseButton.Text = "...";
        qdrantExePathBrowseButton.Click += QdrantExePathBrowseButton_Click;

        // serverExePathLabel / serverExePathPanel
        serverExePathLabel.AutoSize = true;
        serverExePathLabel.Anchor = AnchorStyles.Left;
        serverExePathLabel.Text = "DocRag.Server.exe Pfad (optional)";
        layout.Controls.Add(serverExePathLabel, 0, 17);
        serverExePathPanel.Dock = DockStyle.Fill;
        serverExePathPanel.Height = 30;
        serverExePathPanel.Controls.Add(serverExePathTextBox);
        serverExePathPanel.Controls.Add(serverExePathBrowseButton);
        layout.Controls.Add(serverExePathPanel, 1, 17);
        serverExePathTextBox.Width = 320;
        serverExePathBrowseButton.Left = 326;
        serverExePathBrowseButton.Width = 30;
        serverExePathBrowseButton.Text = "...";
        serverExePathBrowseButton.Click += ServerExePathBrowseButton_Click;

        // buttonPanel
        buttonPanel.Dock = DockStyle.Bottom;
        buttonPanel.Height = 45;
        buttonPanel.FlowDirection = FlowDirection.RightToLeft;
        buttonPanel.Controls.Add(okButton);
        buttonPanel.Controls.Add(cancelButton);

        // okButton
        okButton.Text = "Speichern";
        okButton.Width = 100;
        okButton.DialogResult = DialogResult.OK;
        okButton.Click += OkButton_Click;

        // cancelButton
        cancelButton.Text = "Abbrechen";
        cancelButton.Width = 100;
        cancelButton.DialogResult = DialogResult.Cancel;

        // SettingsForm
        AcceptButton = okButton;
        CancelButton = cancelButton;
        ClientSize = new System.Drawing.Size(620, 860);
        Controls.Add(layout);
        Controls.Add(buttonPanel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "SettingsForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Einstellungen";

        qdrantExePathPanel.ResumeLayout(false);
        serverExePathPanel.ResumeLayout(false);
        rootsButtonsPanel.ResumeLayout(false);
        rootsPanel.ResumeLayout(false);
        buttonPanel.ResumeLayout(false);
        layout.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private TableLayoutPanel layout;

    private Label rootsLabel;
    private Panel rootsPanel;
    private ListBox rootsList;
    private FlowLayoutPanel rootsButtonsPanel;
    private Button addRootButton;
    private Button removeRootButton;

    private Label lmStudioUrlLabel;
    private TextBox lmStudioUrlTextBox;
    private Label embeddingModelLabel;
    private ComboBox embeddingModelComboBox;
    private Label chatModelLabel;
    private ComboBox chatModelComboBox;

    private Label serverPortLabel;
    private NumericUpDown serverPortNumeric;
    private Label qdrantHttpPortLabel;
    private NumericUpDown qdrantHttpPortNumeric;
    private Label qdrantGrpcPortLabel;
    private NumericUpDown qdrantGrpcPortNumeric;

    private Label chunkSizeLabel;
    private NumericUpDown chunkSizeNumeric;
    private Label chunkOverlapLabel;
    private NumericUpDown chunkOverlapNumeric;
    private Label topKLabel;
    private NumericUpDown topKNumeric;
    private Label batchSizeLabel;
    private NumericUpDown batchSizeNumeric;
    private Label parallelismLabel;
    private NumericUpDown parallelismNumeric;
    private FlowLayoutPanel optimizePanel;
    private Button optimizeButton;
    private Label optimizeStatusLabel;

    private Label contextTokensLabel;
    private NumericUpDown contextTokensNumeric;
    private Label maxContextCharsLabel;
    private NumericUpDown maxContextCharsNumeric;
    private FlowLayoutPanel contextPanel;
    private Button contextButton;
    private Label contextStatusLabel;

    private Label qdrantExePathLabel;
    private Panel qdrantExePathPanel;
    private TextBox qdrantExePathTextBox;
    private Button qdrantExePathBrowseButton;

    private Label serverExePathLabel;
    private Panel serverExePathPanel;
    private TextBox serverExePathTextBox;
    private Button serverExePathBrowseButton;

    private FlowLayoutPanel buttonPanel;
    private Button okButton;
    private Button cancelButton;
}
