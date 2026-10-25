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

    private static string? FindServerProject()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var csproj = Path.Combine(dir.FullName, "src", "DocRag.Server", "DocRag.Server.csproj");
            if (File.Exists(csproj)) return csproj;

            csproj = Path.Combine(dir.FullName, "DocRag.Server", "DocRag.Server.csproj");
            if (File.Exists(csproj)) return csproj;
        }

        return null;
    }

    public async Task<bool> BuildServerAsync()
    {
        if (_server.IsRunning)
        {
            LogReceived?.Invoke("Build", "Server läuft bereits - Kompilieren übersprungen.");
            return true;
        }

        var csproj = FindServerProject();
        if (csproj is null)
        {
            LogReceived?.Invoke("Build", "DocRag.Server.csproj nicht gefunden - Kompilieren übersprungen.");
            return false;
        }

        LogReceived?.Invoke("Build", "Kompiliere Server ...");

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{csproj}\" -c Debug -nologo -v q",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data)) LogReceived?.Invoke("Build", e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data)) LogReceived?.Invoke("Build", e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            var ok = process.ExitCode == 0;
            LogReceived?.Invoke("Build", ok ? "Server erfolgreich kompiliert." : $"Kompilieren fehlgeschlagen (Exit-Code {process.ExitCode}).");
            return ok;
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke("Build", $"Kompilieren fehlgeschlagen: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Kills leftover qdrant.exe / DocRag.Server.exe processes (e.g. orphans of an earlier admin session)
    /// that would block the storage lock or the ports. Only processes started from our own executables
    /// are touched, and never the ones this manager currently runs.
    /// </summary>
    public int KillStrayProcesses(AppConfig config)
    {
        var killed = 0;
        killed += KillStrays("qdrant", FindQdrantExe(config), _qdrant, "Qdrant");
        killed += KillStrays("DocRag.Server", FindServerExe(config), _server, "Server");
        return killed;
    }

    private int KillStrays(string processName, string? exePath, ManagedProcess managed, string logName)
    {
        if (exePath is null) return 0;
        var expected = Path.GetFullPath(exePath);
        var killed = 0;

        foreach (var candidate in Process.GetProcessesByName(processName))
        {
            try
            {
                if (managed.Process is { HasExited: false } own && own.Id == candidate.Id) continue;

                var path = candidate.MainModule?.FileName;
                if (path is null || !string.Equals(Path.GetFullPath(path), expected, StringComparison.OrdinalIgnoreCase)) continue;

                candidate.Kill(entireProcessTree: true);
                candidate.WaitForExit(5000);
                killed++;
                LogReceived?.Invoke(logName, $"Alter Prozess beendet (PID {candidate.Id}).");
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke(logName, $"Alter Prozess (PID {candidate.Id}) konnte nicht beendet werden: {ex.Message}");
            }
            finally
            {
                candidate.Dispose();
            }
        }

        return killed;
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

        KillStrayProcesses(config);

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

        KillStrays("DocRag.Server", exePath, _server, "Server");

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
            ChildProcessJob.Add(process);
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

    /// <summary>Stops the managed processes and also any orphaned instances left over from earlier sessions.</summary>
    public void StopAll(AppConfig? config = null)
    {
        StopServer();
        StopQdrant();

        if (config is not null)
        {
            KillStrayProcesses(config);
        }
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
