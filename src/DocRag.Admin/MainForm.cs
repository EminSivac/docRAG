using System.Net.Http.Json;
using DocRag.Shared;

namespace DocRag.Admin;

public partial class MainForm : Form
{
    private readonly ProcessManager _processManager = new();
    private readonly HttpClient _http = new();
    private AppConfig _config;
    private Task<bool>? _buildTask;

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

    private void StopButton_Click(object? sender, EventArgs e) => _processManager.StopAll(_config);

    private async void ReindexButton_Click(object? sender, EventArgs e) => await TriggerReindexAsync();

    private async void ClearDbButton_Click(object? sender, EventArgs e) => await ClearDatabaseAsync();

    private void SettingsButton_Click(object? sender, EventArgs e) => OpenSettings();

    private void OpenWebButton_Click(object? sender, EventArgs e) => OpenWebUi();

    private async void StatusTimer_Tick(object? sender, EventArgs e) => await RefreshStatusAsync();

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _buildTask = _processManager.BuildServerAsync();
    }

    private async void StartAll()
    {
        if (_buildTask is not null) await _buildTask;
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
                { IsRunning: true } => FormatRunningStatus(status),
                { StartedAt: null } =>
                    "Indexierung: noch nie gelaufen",
                _ =>
                    $"Indexierung fertig: {status.ProcessedFiles} verarbeitet, {status.SkippedFiles} übersprungen, " +
                    $"{status.FailedFiles} Fehler (von {status.TotalFiles} Dateien), {status.TotalChunksIndexed} Chunks gesamt" +
                    FormatDuration(status)
            };
        }
        catch
        {
            ingestStatusLabel.Text = "Indexierung: Server nicht erreichbar";
        }
    }

    private static string FormatRunningStatus(IngestStatus status)
    {
        var done = status.ProcessedFiles + status.SkippedFiles + status.FailedFiles;
        var text = $"Indexierung läuft: {done}/{status.TotalFiles} geprüft " +
                   $"({status.ProcessedFiles} verarbeitet, {status.SkippedFiles} übersprungen, {status.FailedFiles} Fehler)";

        if (status.StartedAt is not { } started)
        {
            return text;
        }

        var elapsed = DateTime.UtcNow - started;
        if (elapsed.TotalSeconds < 2 || done == 0)
        {
            return text + " – berechne Geschwindigkeit …";
        }

        var filesPerSec = done / elapsed.TotalSeconds;
        var chunksPerSec = status.TotalChunksIndexed / elapsed.TotalSeconds;
        var remaining = Math.Max(0, status.TotalFiles - done);
        var eta = TimeSpan.FromSeconds(remaining / filesPerSec);

        return text +
               $" | {filesPerSec:0.0} Dateien/s, {chunksPerSec:0.0} Chunks/s" +
               $" | vergangen {FormatSpan(elapsed)}, noch ca. {FormatSpan(eta)}";
    }

    private static string FormatDuration(IngestStatus status) =>
        status is { StartedAt: { } started, FinishedAt: { } finished }
            ? $" | Dauer {FormatSpan(finished - started)}"
            : "";

    private static string FormatSpan(TimeSpan span) =>
        span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes:00}min"
        : span.TotalMinutes >= 1 ? $"{span.Minutes}min {span.Seconds:00}s"
        : $"{Math.Max(1, (int)span.TotalSeconds)}s";

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
        _processManager.StopAll(_config);
        trayIcon.Visible = false;
        base.OnFormClosing(e);
    }
}
