using System.Diagnostics;
using DocRag.Shared;

namespace DocRag.Admin;

public sealed class ManagedProcess
{
    public required string Name { get; init; }
    public Process? Process { get; set; }
    public bool IsRunning => Process is { HasExited: false };
    public bool StoppedIntentionally { get; set; }
}

public sealed class ProcessManager
{
    public event Action<string, string>? LogReceived; // (processName, line)

    private readonly ManagedProcess _qdrant = new() { Name = "Qdrant" };
    private readonly ManagedProcess _server = new() { Name = "Server" };

    public bool IsQdrantRunning => _qdrant.IsRunning;
    public bool IsServerRunning => _server.IsRunning;

    public string? FindQdrantExe(AppConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.QdrantExePath) && File.Exists(config.QdrantExePath))
        {
            return config.QdrantExePath;
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "tools", "qdrant", "qdrant.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tools", "qdrant", "qdrant.exe"),
        };

        return candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
    }

    public string? FindServerExe(AppConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.ServerExePath) && File.Exists(config.ServerExePath))
        {
            return config.ServerExePath;
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Server", "DocRag.Server.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DocRag.Server", "bin", "Debug", "net10.0", "DocRag.Server.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DocRag.Server", "bin", "Release", "net10.0", "DocRag.Server.exe"),
        };

        return candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
    }

    public bool StartQdrant(AppConfig config)
    {
        if (_qdrant.IsRunning) return true;

        var exePath = FindQdrantExe(config);
        if (exePath is null)
        {
            LogReceived?.Invoke("Qdrant", "qdrant.exe wurde nicht gefunden. Bitte Pfad in den Einstellungen setzen.");
            return false;
        }

        var storageDir = Path.Combine(ConfigPaths.DataDirectory, "qdrant-storage");
        Directory.CreateDirectory(storageDir);

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            EnvironmentVariables =
            {
                ["QDRANT__STORAGE__STORAGE_PATH"] = storageDir,
                ["QDRANT__SERVICE__HTTP_PORT"] = config.QdrantHttpPort.ToString(),
                ["QDRANT__SERVICE__GRPC_PORT"] = config.QdrantGrpcPort.ToString(),
            }
        };

        return StartProcess(_qdrant, psi);
    }

    public bool StartServer(AppConfig config)
    {
        if (_server.IsRunning) return true;

        var exePath = FindServerExe(config);
        if (exePath is null)
        {
            LogReceived?.Invoke("Server", "DocRag.Server.exe wurde nicht gefunden. Bitte Pfad in den Einstellungen setzen.");
            return false;
        }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        return StartProcess(_server, psi);
    }

    private bool StartProcess(ManagedProcess managed, ProcessStartInfo psi)
    {
        try
        {
            managed.StoppedIntentionally = false;

            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null) LogReceived?.Invoke(managed.Name, e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null) LogReceived?.Invoke(managed.Name, e.Data);
            };
            process.Exited += (_, _) =>
            {
                if (!managed.StoppedIntentionally)
                {
                    LogReceived?.Invoke(managed.Name,
                        $"!!! UNERWARTET BEENDET (Exit-Code {process.ExitCode}) — siehe Fehlermeldung oben im Log, z.B. Port bereits belegt.");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            managed.Process = process;
            LogReceived?.Invoke(managed.Name, $"Gestartet (PID {process.Id}).");
            return true;
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke(managed.Name, $"Start fehlgeschlagen: {ex.Message}");
            return false;
        }
    }

    public void StopQdrant() => Stop(_qdrant);
    public void StopServer() => Stop(_server);

    public void StopAll()
    {
        StopServer();
        StopQdrant();
    }

    private void Stop(ManagedProcess managed)
    {
        if (managed.Process is not { HasExited: false } process)
        {
            return;
        }

        try
        {
            managed.StoppedIntentionally = true;
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
            LogReceived?.Invoke(managed.Name, "Gestoppt.");
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke(managed.Name, $"Stop fehlgeschlagen: {ex.Message}");
        }
    }
}
