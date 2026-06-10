using System.Diagnostics;
using System.Text.Json;
using FamilyAssistant.Data;
using FamilyAssistant.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistant.Services;

public class RecipeImportTraceService(IServiceScopeFactory scopeFactory, ILogger<RecipeImportTraceService> logger)
{
    private const int MaxRawTextLength = 250_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task<T> TraceAsync<T>(
        RecipeImportTraceContext? context,
        string stepName,
        string stage,
        object? input,
        Func<Task<T>> action,
        Func<T, object?>? output = null)
    {
        if (context is not { Enabled: true })
        {
            return await action();
        }

        var entryId = await StartEntryAsync(context, stepName, stage, input);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await action();
            stopwatch.Stop();
            await CompleteEntryAsync(entryId, "Success", output is null ? result : output(result), null, stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await CompleteEntryAsync(entryId, "Error", null, ex.Message, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task TracePointAsync(
        RecipeImportTraceContext? context,
        string stepName,
        string stage,
        object? input,
        object? output = null)
    {
        if (context is not { Enabled: true })
        {
            return;
        }

        var entryId = await StartEntryAsync(context, stepName, stage, input);
        await CompleteEntryAsync(entryId, "Success", output, null, 0);
    }

    private async Task<int> StartEntryAsync(RecipeImportTraceContext context, string stepName, string stage, object? input)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var nextSortOrder = await db.RecipeImportTraceEntries
            .Where(e => e.CandidateId == context.CandidateId && e.AttemptNumber == context.AttemptNumber)
            .Select(e => (int?)e.SortOrder)
            .MaxAsync() ?? 0;

        var entry = new RecipeImportTraceEntry
        {
            CandidateId = context.CandidateId,
            AttemptNumber = context.AttemptNumber,
            SortOrder = nextSortOrder + 1,
            StepName = stepName,
            Stage = stage,
            StartedAt = DateTime.UtcNow,
            Status = "Running",
            InputJson = Serialize(input)
        };
        db.RecipeImportTraceEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }

    private async Task CompleteEntryAsync(int entryId, string status, object? output, string? errorMessage, long durationMs)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entry = await db.RecipeImportTraceEntries.FirstOrDefaultAsync(e => e.Id == entryId);
            if (entry is null)
            {
                return;
            }

            entry.Status = status;
            entry.CompletedAt = DateTime.UtcNow;
            entry.DurationMs = durationMs;
            entry.OutputJson = Serialize(output);
            entry.ErrorMessage = errorMessage;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Completing recipe import trace entry {EntryId} failed.", entryId);
        }
    }

    private static string? Serialize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Serialize(Sanitize(value), JsonOptions);
        }
        catch
        {
            return JsonSerializer.Serialize(new { value = value.ToString() }, JsonOptions);
        }
    }

    private static object Sanitize(object value)
    {
        if (value is string text)
        {
            return Truncate(text);
        }

        return value;
    }

    public static object TracedText(string? value) => new
    {
        value = Truncate(value ?? string.Empty),
        truncated = (value?.Length ?? 0) > MaxRawTextLength,
        originalLength = value?.Length ?? 0
    };

    private static string Truncate(string value)
        => value.Length <= MaxRawTextLength ? value : value[..MaxRawTextLength];
}
