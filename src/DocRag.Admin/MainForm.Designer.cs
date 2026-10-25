namespace DocRag.Admin;

partial class MainForm
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
        components = new System.ComponentModel.Container();
        statusPanel = new FlowLayoutPanel();
        qdrantStatusLabel = new Label();
        serverStatusLabel = new Label();
        ingestStatusLabel = new Label();
        buttonPanel = new FlowLayoutPanel();
        startButton = new Button();
        stopButton = new Button();
        reindexButton = new Button();
        clearDbButton = new Button();
        settingsButton = new Button();
        openWebButton = new Button();
        logBox = new TextBox();
        trayIcon = new NotifyIcon(components);
        trayMenu = new ContextMenuStrip(components);
        openMenuItem = new ToolStripMenuItem();
        exitMenuItem = new ToolStripMenuItem();
        statusTimer = new System.Windows.Forms.Timer(components);

        statusPanel.SuspendLayout();
        buttonPanel.SuspendLayout();
        trayMenu.SuspendLayout();
        SuspendLayout();

        // statusPanel
        statusPanel.Controls.Add(qdrantStatusLabel);
        statusPanel.Controls.Add(serverStatusLabel);
        statusPanel.Controls.Add(ingestStatusLabel);
        statusPanel.Dock = DockStyle.Top;
        statusPanel.Height = 64;
        statusPanel.SetFlowBreak(serverStatusLabel, true);
        statusPanel.Name = "statusPanel";
        statusPanel.Padding = new Padding(10);

        // qdrantStatusLabel
        qdrantStatusLabel.AutoSize = true;
        qdrantStatusLabel.Margin = new Padding(0, 5, 30, 0);
        qdrantStatusLabel.Name = "qdrantStatusLabel";
        qdrantStatusLabel.Text = "Qdrant: gestoppt";

        // serverStatusLabel
        serverStatusLabel.AutoSize = true;
        serverStatusLabel.Margin = new Padding(0, 5, 30, 0);
        serverStatusLabel.Name = "serverStatusLabel";
        serverStatusLabel.Text = "Server: gestoppt";

        // ingestStatusLabel
        ingestStatusLabel.AutoSize = true;
        ingestStatusLabel.Margin = new Padding(0, 5, 0, 0);
        ingestStatusLabel.Name = "ingestStatusLabel";
        ingestStatusLabel.Text = "Indexierung: -";

        // buttonPanel
        buttonPanel.Controls.Add(startButton);
        buttonPanel.Controls.Add(stopButton);
        buttonPanel.Controls.Add(reindexButton);
        buttonPanel.Controls.Add(clearDbButton);
        buttonPanel.Controls.Add(settingsButton);
        buttonPanel.Controls.Add(openWebButton);
        buttonPanel.Dock = DockStyle.Top;
        buttonPanel.Height = 50;
        buttonPanel.Name = "buttonPanel";
        buttonPanel.Padding = new Padding(10);

        // startButton
        startButton.Name = "startButton";
        startButton.Text = "Start Alles";
        startButton.Width = 110;
        startButton.Click += StartButton_Click;

        // stopButton
        stopButton.Name = "stopButton";
        stopButton.Text = "Stop Alles";
        stopButton.Width = 110;
        stopButton.Click += StopButton_Click;

        // reindexButton
        reindexButton.Name = "reindexButton";
        reindexButton.Text = "Reindex jetzt";
        reindexButton.Width = 120;
        reindexButton.Click += ReindexButton_Click;

        // clearDbButton
        clearDbButton.Name = "clearDbButton";
        clearDbButton.Text = "DB komplett leeren";
        clearDbButton.Width = 140;
        clearDbButton.Click += ClearDbButton_Click;

        // settingsButton
        settingsButton.Name = "settingsButton";
        settingsButton.Text = "Einstellungen";
        settingsButton.Width = 110;
        settingsButton.Click += SettingsButton_Click;

        // openWebButton
        openWebButton.Name = "openWebButton";
        openWebButton.Text = "Web-UI öffnen";
        openWebButton.Width = 120;
        openWebButton.Click += OpenWebButton_Click;

        // logBox
        logBox.Dock = DockStyle.Fill;
        logBox.Font = new System.Drawing.Font("Consolas", 9F);
        logBox.Multiline = true;
        logBox.Name = "logBox";
        logBox.ReadOnly = true;
        logBox.ScrollBars = ScrollBars.Vertical;

        // trayIcon
        trayIcon.ContextMenuStrip = trayMenu;
        trayIcon.Icon = SystemIcons.Application;
        trayIcon.Text = "docRAG Admin";
        trayIcon.Visible = true;
        trayIcon.DoubleClick += TrayIcon_DoubleClick;

        // trayMenu
        trayMenu.Items.AddRange(new ToolStripItem[] { openMenuItem, exitMenuItem });
        trayMenu.Name = "trayMenu";

        // openMenuItem
        openMenuItem.Name = "openMenuItem";
        openMenuItem.Text = "Öffnen";
        openMenuItem.Click += OpenMenuItem_Click;

        // exitMenuItem
        exitMenuItem.Name = "exitMenuItem";
        exitMenuItem.Text = "Beenden";
        exitMenuItem.Click += ExitMenuItem_Click;

        // statusTimer
        statusTimer.Interval = 2000;
        statusTimer.Tick += StatusTimer_Tick;

        // MainForm
        ClientSize = new System.Drawing.Size(780, 560);
        Controls.Add(logBox);
        Controls.Add(buttonPanel);
        Controls.Add(statusPanel);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "docRAG Admin";

        statusPanel.ResumeLayout(false);
        statusPanel.PerformLayout();
        buttonPanel.ResumeLayout(false);
        trayMenu.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private FlowLayoutPanel statusPanel;
    private Label qdrantStatusLabel;
    private Label serverStatusLabel;
    private Label ingestStatusLabel;
    private FlowLayoutPanel buttonPanel;
    private Button startButton;
    private Button stopButton;
    private Button reindexButton;
    private Button clearDbButton;
    private Button settingsButton;
    private Button openWebButton;
    private TextBox logBox;
    private NotifyIcon trayIcon;
    private ContextMenuStrip trayMenu;
    private ToolStripMenuItem openMenuItem;
    private ToolStripMenuItem exitMenuItem;
    private System.Windows.Forms.Timer statusTimer;
}
