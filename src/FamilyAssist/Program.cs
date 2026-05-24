using FamilyAssist.Components;
using FamilyAssist.Configuration;
using FamilyAssist.Data;
using FamilyAssist.Localization;
using FamilyAssist.Middleware;
using FamilyAssist.Models;
using FamilyAssist.Services;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
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

// Localization (IStringLocalizer<SharedResource>)
builder.Services.AddLocalization();
builder.Services.AddTransient<MudLocalizer, AppMudLocalizer>();

// EF Core + SQLite (fallback to legacy DB name for upgrades from HassCompanion)
var dataDir = builder.Environment.IsDevelopment()
    ? Path.Combine(Directory.GetCurrentDirectory(), "data")
    : "/data";
var legacyDbPath = Path.Combine(dataDir, "hasscompanion.db");
var dbPath = File.Exists(legacyDbPath)
    ? legacyDbPath
    : Path.Combine(dataDir, "familyassist.db");

Directory.CreateDirectory(dataDir);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Home Assistant API client (Singleton — holds long-lived WebSocket)
builder.Services.AddHttpClient("HomeAssistant");
builder.Services.AddSingleton<IHomeAssistantService, HomeAssistantService>();

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

// Attachment storage (image resize + disk storage)
builder.Services.AddSingleton<AttachmentStorageService>();

// HA Theme (reads dark/light from HA WebSocket)
builder.Services.AddSingleton<HaThemeService>();

// Background services
builder.Services.AddHostedService<TaskGenerationService>();
builder.Services.AddHostedService<HomeAssistantSyncService>();
builder.Services.AddHostedService<HaWebSocketStartupService>();
builder.Services.AddHostedService<AttachmentCleanupService>();
builder.Services.AddHostedService<HaEventTriggerService>();

// Database health state (tracks migration errors for error middleware)
builder.Services.AddSingleton<DatabaseHealthState>();

var app = builder.Build();

// --- Middleware ---
// Database migration error page (must be first — short-circuits everything if DB is broken)
app.UseMiddleware<DatabaseMigrationErrorMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

// Localization: read language from DB (AppSettings.Language), fallback to "en"
var supportedCultures = new[] { "en", "de", "fr" };
app.UseRequestLocalization(opts =>
{
    opts.SetDefaultCulture(AppSettingsCultureProvider.DefaultLanguage);
    opts.AddSupportedCultures(supportedCultures);
    opts.AddSupportedUICultures(supportedCultures);
    opts.RequestCultureProviders.Clear();
    opts.RequestCultureProviders.Add(new AppSettingsCultureProvider());
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// --- API Endpoints ---
// Image proxy: serves HA images with proper auth (avoids token exposure to client)
// Auth: Ingress-protected (Add-on only assumption)
app.MapGet("/api/ha-image", async (string? path, IHomeAssistantService haService, HttpContext httpContext, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(path))
        return Results.BadRequest("path parameter required");

    // Only allow proxying HA API paths (prevent SSRF)
    if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest("path must start with /api/");

    var result = await haService.GetImageAsync(path, ct);
    if (result is null)
        return Results.NotFound();

    // Cache for 1 hour
    httpContext.Response.Headers.CacheControl = "public, max-age=3600";
    return Results.File(result.Value.Data, result.Value.ContentType);
});

// Attachment proxy: serves task comment attachments from disk
app.MapGet("/api/attachment/{attachmentId:int}", async (int attachmentId, AppDbContext db, AttachmentStorageService storage, HttpContext httpContext, CancellationToken ct) =>
{
    var attachment = await db.TaskAttachments.FindAsync([attachmentId], ct);
    if (attachment is null)
        return Results.NotFound();

    var fullPath = storage.GetFullPath(attachment.FilePath);
    if (!File.Exists(fullPath))
        return Results.NotFound();

    httpContext.Response.Headers.CacheControl = "public, max-age=86400";
    return Results.File(fullPath, attachment.ContentType, attachment.FileName);
});

// Upload endpoint: receives files via HTTP POST (bypasses SignalR for large camera images)
// Antiforgery disabled: JS-based upload from Blazor client requires this
app.MapPost("/api/upload-attachment", async (HttpRequest request, AppDbContext db, AttachmentStorageService storage, CancellationToken ct) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("Expected multipart/form-data");

    var form = await request.ReadFormAsync(ct);
    var taskIdStr = form["taskId"].FirstOrDefault();
    var commentIdStr = form["commentId"].FirstOrDefault();

    if (!int.TryParse(taskIdStr, out var taskId) || !int.TryParse(commentIdStr, out var commentId))
        return Results.BadRequest("Missing or invalid taskId/commentId");

    // Validate that the task exists
    var task = await db.ChoreTasks.FindAsync([taskId], ct);
    if (task is null)
        return Results.NotFound("Task not found");

    // Validate that the comment belongs to this task
    var comment = await db.TaskComments.FirstOrDefaultAsync(c => c.Id == commentId && c.ChoreTaskId == taskId, ct);
    if (comment is null)
        return Results.NotFound("Comment not found or does not belong to this task");

    if (form.Files.Count == 0)
        return Results.BadRequest("No files provided");

    var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp", "image/heic", "image/heif" };
    var results = new List<object>();

    foreach (var file in form.Files)
    {
        if (file.Length == 0)
            continue;

        if (file.Length > 10 * 1024 * 1024)
            return Results.BadRequest($"File '{file.FileName}' exceeds 10 MB limit");

        if (!allowedContentTypes.Any(ct => file.ContentType.StartsWith(ct, StringComparison.OrdinalIgnoreCase)))
            return Results.BadRequest($"File type '{file.ContentType}' not allowed. Only images are accepted.");

        // Sanitize filename (strip path components)
        var safeFileName = Path.GetFileName(file.FileName);

        await using var stream = file.OpenReadStream();
        var (relativePath, contentType, fileSize) = await storage.StoreImageAsync(taskId, stream, safeFileName, ct);

        var attachment = new TaskAttachment
        {
            TaskCommentId = commentId,
            FileName = safeFileName,
            ContentType = contentType,
            FilePath = relativePath,
            FileSize = fileSize,
            CreatedAt = DateTime.UtcNow
        };
        db.TaskAttachments.Add(attachment);
        await db.SaveChangesAsync(ct);

        results.Add(new { attachment.Id, attachment.FileName, attachment.FileSize });
    }

    return Results.Ok(results);
}).DisableAntiforgery();

// Debug: simulate a state_changed event (only available in Development)
if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/debug/simulate-state-change", async (HttpRequest request, IHomeAssistantService haService) =>
    {
        var body = await request.ReadFromJsonAsync<SimulateStateChangeRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.EntityId))
            return Results.BadRequest("entityId required");

        await haService.SimulateStateChangeAsync(body.EntityId, body.OldState ?? "off", body.NewState ?? "on");
        return Results.Ok(new { message = $"Simulated: {body.EntityId} {body.OldState ?? "off"} → {body.NewState ?? "on"}" });
    }).DisableAntiforgery();
}

// --- Database Migration ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var dbHealth = scope.ServiceProvider.GetRequiredService<DatabaseHealthState>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Database migration failed. The application will serve an error page until restarted.");
        dbHealth.SetError(ex);
    }
}

app.Run();

// ─── Request DTOs ───
record SimulateStateChangeRequest(string EntityId, string? OldState, string? NewState);
