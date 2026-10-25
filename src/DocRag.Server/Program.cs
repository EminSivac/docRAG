using DocRag.Server.Api;
using DocRag.Server.Clients;
using DocRag.Server.Ingestion;
using DocRag.Server.Retrieval;
using DocRag.Shared;

var appConfig = ConfigStore.Load();

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(appConfig.ServerPort);
});

builder.Services.AddSingleton(appConfig);
builder.Services.AddSingleton(new IndexManifest(ConfigPaths.ManifestDbPath));
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<AppConfig>();
    return new QdrantService(cfg.QdrantHost, cfg.QdrantGrpcPort, cfg.GetEffectiveCollectionName());
});
builder.Services.AddHttpClient();
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<AppConfig>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(LmStudioClient));
    return new LmStudioClient(httpClient, cfg.LmStudioBaseUrl, cfg.EmbeddingModel, cfg.ChatModel);
});
builder.Services.AddSingleton<IngestionService>();
builder.Services.AddSingleton<RetrievalService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapDocRagEndpoints();

app.Run();
