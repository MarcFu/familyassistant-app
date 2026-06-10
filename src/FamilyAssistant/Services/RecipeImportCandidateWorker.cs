using System.Text.Json;
using FamilyAssistant.Data;
using FamilyAssistant.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistant.Services;

public class RecipeImportCandidateWorker(
    IServiceScopeFactory scopeFactory,
    RecipeImportCandidateControlService controlService,
    ILogger<RecipeImportCandidateWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StaleHeartbeatAge = TimeSpan.FromMinutes(2);
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await MarkInterruptedCandidatesAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessNextCandidateAsync(stoppingToken);
                if (!processed)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Recipe import worker failed while processing candidates.");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task MarkInterruptedCandidatesAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var staleBefore = DateTime.UtcNow - StaleHeartbeatAge;

        var activeCandidates = await db.RecipeImportCandidates
            .Where(c => c.Status == RecipeImportCandidateStatus.Fetching
                || c.Status == RecipeImportCandidateStatus.Parsing
                || c.Status == RecipeImportCandidateStatus.EnhancingWithAi)
            .Where(c => c.LockedBy != null && c.LockedBy != _workerId
                || c.HeartbeatAt == null
                || c.HeartbeatAt < staleBefore)
            .ToListAsync(ct);

        foreach (var candidate in activeCandidates)
        {
            candidate.Status = RecipeImportCandidateStatus.Interrupted;
            candidate.ProgressMessage = "Import wurde unterbrochen und kann fortgesetzt werden.";
            candidate.ErrorMessage = "Der vorherige Importprozess wurde beendet, bevor der Kandidat fertig war.";
            candidate.LockedBy = null;
            candidate.UpdatedAt = DateTime.UtcNow;
        }

        if (activeCandidates.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<bool> ProcessNextCandidateAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var candidate = await db.RecipeImportCandidates
            .Where(c => c.Status == RecipeImportCandidateStatus.Requested)
            .OrderBy(c => c.CreatedAt)
            .FirstOrDefaultAsync(stoppingToken);
        if (candidate is null)
        {
            return false;
        }

        using var candidateCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        controlService.Register(candidate.Id, candidateCts);
        var ct = candidateCts.Token;

        var importService = scope.ServiceProvider.GetRequiredService<RecipeImportService>();
        var traceSettings = scope.ServiceProvider.GetRequiredService<ITraceSettingsService>();
        var traceService = scope.ServiceProvider.GetRequiredService<RecipeImportTraceService>();
        try
        {
            candidate.AttemptCount++;
            candidate.StartedAt = DateTime.UtcNow;
            candidate.HeartbeatAt = DateTime.UtcNow;
            candidate.PausedAt = null;
            candidate.CompletedAt = null;
            candidate.LockedBy = _workerId;
            candidate.PauseRequested = false;
            await UpdateCandidateAsync(db, candidate, RecipeImportCandidateStatus.Fetching, 5, "Import angefordert", ct);
            var trace = new RecipeImportTraceContext(
                candidate.Id,
                candidate.AttemptCount,
                await traceSettings.IsRecipeImportTraceEnabledAsync(ct));

            var progress = new Progress<RecipeImportProgress>(update =>
            {
                _ = UpdateProgressAsync(candidate.Id, update, CancellationToken.None);
            });

            RecipeImportDraft draft;
            if (candidate.SourceType == RecipeSourceType.Url)
            {
                if (string.IsNullOrWhiteSpace(candidate.SourceUrl))
                {
                    throw new InvalidOperationException("Recipe import URL is empty.");
                }

                draft = await importService.ImportFromUrlAsync(candidate.SourceUrl, candidate.Quality, candidate.TargetLanguage, progress, trace, ct);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(candidate.SourceText))
                {
                    throw new InvalidOperationException("Recipe import text is empty.");
                }

                draft = await importService.ImportFromTextAsync(candidate.SourceText, candidate.SourceType, candidate.Quality, candidate.TargetLanguage, progress, trace, ct);
            }

            candidate.Status = RecipeImportCandidateStatus.ProposalReady;
            candidate.ProgressPercent = 100;
            candidate.ProgressMessage = "Vorschlag bereit";
            candidate.ProposalName = string.IsNullOrWhiteSpace(draft.Name) ? null : draft.Name;
            candidate.DraftJson = JsonSerializer.Serialize(draft, JsonOptions);
            candidate.QualityReport = draft.QualityReport;
            candidate.ErrorMessage = null;
            candidate.CompletedAt = DateTime.UtcNow;
            candidate.HeartbeatAt = DateTime.UtcNow;
            candidate.LockedBy = null;
            candidate.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(stoppingToken);
            await traceService.TracePointAsync(
                trace,
                "ProposalStored",
                RecipeImportStage.Completed.ToString(),
                null,
                new
                {
                    candidate.Id,
                    candidate.Status,
                    candidate.ProposalName,
                    candidate.QualityReport,
                    draft
                });
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            if (await IsPauseRequestedAsync(candidate.Id, CancellationToken.None))
            {
                await MarkPausedAsync(candidate.Id, CancellationToken.None);
            }
            else
            {
                throw;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Recipe import candidate {CandidateId} failed.", candidate.Id);
            candidate.Status = RecipeImportCandidateStatus.Failed;
            candidate.ProgressMessage = "Import fehlgeschlagen";
            candidate.ErrorMessage = ex.Message;
            candidate.CompletedAt = DateTime.UtcNow;
            candidate.HeartbeatAt = DateTime.UtcNow;
            candidate.LockedBy = null;
            candidate.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(stoppingToken);
        }
        finally
        {
            controlService.Unregister(candidate.Id);
        }

        return true;
    }

    private async Task UpdateProgressAsync(int candidateId, RecipeImportProgress progress, CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var candidate = await db.RecipeImportCandidates.FirstOrDefaultAsync(c => c.Id == candidateId, ct);
            if (candidate is null || IsTerminalStatus(candidate.Status))
            {
                return;
            }

            candidate.Status = progress.Stage switch
            {
                RecipeImportStage.DownloadPage => RecipeImportCandidateStatus.Fetching,
                RecipeImportStage.FindRecipeData or RecipeImportStage.ParseMetadata or RecipeImportStage.ParseIngredients or RecipeImportStage.ParseSteps => RecipeImportCandidateStatus.Parsing,
                RecipeImportStage.PrepareDraft
                    or RecipeImportStage.AiExtraction
                    or RecipeImportStage.AiValidation
                    or RecipeImportStage.AiRefinement
                    or RecipeImportStage.FinalValidation => RecipeImportCandidateStatus.EnhancingWithAi,
                _ => candidate.Status
            };
            candidate.ProgressPercent = progress.Percent;
            candidate.ProgressMessage = progress.Message;
            candidate.HeartbeatAt = DateTime.UtcNow;
            candidate.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Updating recipe import progress failed for candidate {CandidateId}.", candidateId);
        }
    }

    private async Task<bool> IsPauseRequestedAsync(int candidateId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.RecipeImportCandidates
            .AsNoTracking()
            .AnyAsync(c => c.Id == candidateId && c.PauseRequested, ct);
    }

    private async Task MarkPausedAsync(int candidateId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var candidate = await db.RecipeImportCandidates.FirstOrDefaultAsync(c => c.Id == candidateId, ct);
        if (candidate is null)
        {
            return;
        }

        candidate.Status = RecipeImportCandidateStatus.Paused;
        candidate.ProgressMessage = "Import pausiert.";
        candidate.PausedAt = DateTime.UtcNow;
        candidate.LockedBy = null;
        candidate.PauseRequested = false;
        candidate.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static async Task UpdateCandidateAsync(
        AppDbContext db,
        RecipeImportCandidate candidate,
        RecipeImportCandidateStatus status,
        int percent,
        string message,
        CancellationToken ct)
    {
        candidate.Status = status;
        candidate.ProgressPercent = percent;
        candidate.ProgressMessage = message;
        candidate.HeartbeatAt = DateTime.UtcNow;
        candidate.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static bool IsTerminalStatus(RecipeImportCandidateStatus status) => status is RecipeImportCandidateStatus.ProposalReady
        or RecipeImportCandidateStatus.Failed
        or RecipeImportCandidateStatus.Cancelled
        or RecipeImportCandidateStatus.Accepted
        or RecipeImportCandidateStatus.Paused
        or RecipeImportCandidateStatus.Interrupted;
}
