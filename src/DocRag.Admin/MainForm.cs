using System.Net.Http.Json;
using DocRag.Shared;

namespace DocRag.Admin;

public partial class MainForm : Form
{
    private readonly ProcessManager _processManager = new();
    private readonly HttpClient _http = new();
    private AppConfig _config;

    public MainForm()
    {
        InitializeComponent();

        _config = ConfigStore.Load();
        _processManager.LogReceived += OnLogReceived;
        statusTimer.Start();
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void TrayIcon_DoubleClick(object? sender, EventArgs e) => RestoreFromTray();

    private void OpenMenuItem_Click(object? sender, EventArgs e) => RestoreFromTray();

    private void ExitMenuItem_Click(object? sender, EventArgs e)
    {
        trayIcon.Visible = false;
        Application.Exit();
    }

    private void StartButton_Click(object? sender, EventArgs e) => StartAll();

    private void StopButton_Click(object? sender, EventArgs e) => _processManager.StopAll();

    private async void ReindexButton_Click(object? sender, EventArgs e) => await TriggerReindexAsync();

    private async void ClearDbButton_Click(object? sender, EventArgs e) => await ClearDatabaseAsync();

    private void SettingsButton_Click(object? sender, EventArgs e) => OpenSettings();

    private void OpenWebButton_Click(object? sender, EventArgs e) => OpenWebUi();

    private async void StatusTimer_Tick(object? sender, EventArgs e) => await RefreshStatusAsync();

    private void StartAll()
    {
        _config = ConfigStore.Load();
        _processManager.StartQdrant(_config);
        _processManager.StartServer(_config);
    }

    private void OpenSettings()
    {
        using var settingsForm = new SettingsForm(_config);
        if (settingsForm.ShowDialog(this) == DialogResult.OK)
        {
            _config = settingsForm.ResultConfig;
            ConfigStore.Save(_config);
            AppendLog("Admin", "Konfiguration gespeichert. Bitte Server neu starten, damit Änderungen wirksam werden.");
        }
    }

    private void OpenWebUi()
    {
        var url = $"http://localhost:{_config.ServerPort}";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    }

    private async Task TriggerReindexAsync()
    {
        try
        {
            var response = await _http.PostAsync($"http://localhost:{_config.ServerPort}/api/ingest/start", null);
            AppendLog("Admin", response.IsSuccessStatusCode
                ? "Indexierung gestartet."
                : $"Indexierung konnte nicht gestartet werden ({response.StatusCode}).");
        }
        catch (Exception ex)
        {
            AppendLog("Admin", $"Server nicht erreichbar: {ex.Message}");
        }
    }

    private async Task ClearDatabaseAsync()
    {
        var confirm = MessageBox.Show(
            this,
            "Das löscht ALLE indizierten Daten unwiderruflich: alle Qdrant-Collections dieser App " +
            "(für alle jemals verwendeten Embedding-Modelle) und das lokale Ingestion-Manifest. " +
            "Die Originaldokumente auf der Festplatte sind davon nicht betroffen, aber der nächste " +
            "Reindex verarbeitet wieder alle Dateien von vorn.\n\nWirklich alles löschen?",
            "DB komplett leeren",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        try
        {
            var response = await _http.PostAsync($"http://localhost:{_config.ServerPort}/api/ingest/clear", null);
            if (response.IsSuccessStatusCode)
            {
                AppendLog("Admin", "Datenbank vollständig geleert.");
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                AppendLog("Admin", $"Leeren fehlgeschlagen ({response.StatusCode}): {body}");
            }
        }
        catch (Exception ex)
        {
            AppendLog("Admin", $"Server nicht erreichbar: {ex.Message}");
        }
    }

    private async Task RefreshStatusAsync()
    {
        qdrantStatusLabel.Text = $"Qdrant: {(_processManager.IsQdrantRunning ? "läuft" : "gestoppt")}";
        serverStatusLabel.Text = $"Server: {(_processManager.IsServerRunning ? "läuft" : "gestoppt")}";

        if (!_processManager.IsServerRunning)
        {
            ingestStatusLabel.Text = "Indexierung: Server offline";
            return;
        }

        try
        {
            var status = await _http.GetFromJsonAsync<IngestStatus>(
                $"http://localhost:{_config.ServerPort}/api/ingest/status");

            if (status is null)
            {
                ingestStatusLabel.Text = "Indexierung: unbekannt";
                return;
            }

            ingestStatusLabel.Text = status switch
            {
                { IsRunning: true } =>
                    $"Indexierung läuft: {status.ProcessedFiles + status.SkippedFiles + status.FailedFiles}/{status.TotalFiles} " +
                    $"geprüft ({status.ProcessedFiles} verarbeitet, {status.SkippedFiles} übersprungen, {status.FailedFiles} Fehler)",
                { StartedAt: null } =>
                    "Indexierung: noch nie gelaufen",
                _ =>
                    $"Indexierung fertig: {status.ProcessedFiles} verarbeitet, {status.SkippedFiles} übersprungen, " +
                    $"{status.FailedFiles} Fehler (von {status.TotalFiles} Dateien), {status.TotalChunksIndexed} Chunks gesamt"
            };
        }
        catch
        {
            ingestStatusLabel.Text = "Indexierung: Server nicht erreichbar";
        }
    }

    private void OnLogReceived(string source, string line) => AppendLog(source, line);

    private void AppendLog(string source, string line)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(source, line));
            return;
        }

        logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] [{source}] {line}{Environment.NewLine}");
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        statusTimer.Stop();
        _processManager.StopAll();
        trayIcon.Visible = false;
        base.OnFormClosing(e);
    }
}
