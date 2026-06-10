using System.Collections.Concurrent;

namespace FamilyAssistant.Services;

public class RecipeImportCandidateControlService
{
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _runningCandidates = new();

    public void Register(int candidateId, CancellationTokenSource cancellationTokenSource)
    {
        _runningCandidates[candidateId] = cancellationTokenSource;
    }

    public void Unregister(int candidateId)
    {
        _runningCandidates.TryRemove(candidateId, out _);
    }

    public void CancelIfRunning(int candidateId)
    {
        if (_runningCandidates.TryGetValue(candidateId, out var cancellationTokenSource))
        {
            cancellationTokenSource.Cancel();
        }
    }
}
