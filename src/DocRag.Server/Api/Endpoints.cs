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
            var response = await retrieval.AnswerAsync(request.Question, ct);
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

        app.MapGet("/api/config", (AppConfig config) => Results.Ok(config));

        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
    }
}
