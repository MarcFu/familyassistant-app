using HassCompanion.Components;
using HassCompanion.Configuration;
using HassCompanion.Data;
using HassCompanion.Services;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---
// In Add-on mode, SUPERVISOR_TOKEN env variable overrides config
var supervisorToken = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN");
if (!string.IsNullOrEmpty(supervisorToken))
{
    builder.Configuration["HomeAssistant:BaseUrl"] = "http://supervisor/core";
    builder.Configuration["HomeAssistant:Token"] = supervisorToken;
}

builder.Services.Configure<HomeAssistantOptions>(
    builder.Configuration.GetSection(HomeAssistantOptions.SectionName));

// --- Services ---
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// EF Core + SQLite
var dbPath = Path.Combine(
    builder.Environment.IsDevelopment()
        ? Path.Combine(Directory.GetCurrentDirectory(), "data")
        : "/data",
    "hasscompanion.db");

Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Home Assistant API client
builder.Services.AddHttpClient<IHomeAssistantService, HomeAssistantService>();

// Task generation (scoped — used by background service + UI)
builder.Services.AddScoped<TaskGenerator>();

// Icon generation (Ollama + keyword fallback)
builder.Services.AddHttpClient<OllamaIconService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5); // SVG generation can take 2-3 min with large models
});
builder.Services.AddScoped<ChoreIconGenerator>();

// User context (scoped per Blazor circuit)
builder.Services.AddScoped<UserContextService>();

// Background services
builder.Services.AddHostedService<TaskGenerationService>();
builder.Services.AddHostedService<HomeAssistantSyncService>();

var app = builder.Build();

// --- Middleware ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// --- API Endpoints ---
// Image proxy: serves HA images with proper auth (avoids token exposure to client)
app.MapGet("/api/ha-image", async (string path, IHomeAssistantService haService, HttpContext httpContext, CancellationToken ct) =>
{
    var result = await haService.GetImageAsync(path, ct);
    if (result is null)
        return Results.NotFound();

    // Cache for 1 hour
    httpContext.Response.Headers.CacheControl = "public, max-age=3600";
    return Results.File(result.Value.Data, result.Value.ContentType);
});

// --- Database Migration ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
