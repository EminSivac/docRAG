using DocRag.Server.Ingestion;
using DocRag.Server.Retrieval;
using DocRag.Shared;

namespace DocRag.Server.Api;

public static class Endpoints
{
    public static void MapDocRagEndpoints(this WebApplication app)
    {
        app.MapPost("/api/chat", async (ChatRequest request, RetrievalService retrieval, CancellationToken ct) =>
        {
            var response = await retrieval.AnswerAsync(request.Question, request.Deep, ct);
            return Results.Ok(response);
        });

        app.MapPost("/api/ingest/start", (IngestionService ingestion, IHostApplicationLifetime lifetime, ILogger<Program> logger) =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await ingestion.RunAsync(lifetime.ApplicationStopping);
                }
                catch (InvalidOperationException)
                {
                    // already running
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unerwarteter Fehler beim Starten der Indexierung");
                }
            });

            return Results.Accepted(value: new { message = "Indexierung gestartet." });
        });

        app.MapGet("/api/ingest/status", (IngestionService ingestion) =>
        {
            return Results.Ok(ingestion.GetStatus());
        });

        app.MapPost("/api/ingest/clear", async (IngestionService ingestion, CancellationToken ct) =>
        {
            try
            {
                await ingestion.ClearAllAsync(ct);
                return Results.Ok(new { message = "Datenbank vollständig geleert." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
        });

        // Liefert eine indexierte PDF inline aus (kein Download), damit der Browser-eigene Viewer sie anzeigt.
        // Der Sprung zur Seite erfolgt clientseitig über #page=N.
        app.MapGet("/api/file", (string path, AppConfig config) =>
        {
            if (string.IsNullOrWhiteSpace(path)
                || !string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return Results.NotFound();
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                return Results.NotFound();
            }

            var allowed = config.DocumentRoots.Any(root =>
            {
                if (string.IsNullOrWhiteSpace(root)) return false;
                var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                               + Path.DirectorySeparatorChar;
                return fullPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
            });

            if (!allowed || !File.Exists(fullPath)) return Results.NotFound();

            // Ohne Dateiname im Content-Disposition => Browser zeigt inline an.
            return Results.File(fullPath, "application/pdf", enableRangeProcessing: true);
        });

        app.MapGet("/api/config",(AppConfig config) => Results.Ok(config));

        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
    }
}
