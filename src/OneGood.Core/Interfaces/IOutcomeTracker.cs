using OneGood.Core.Models;

namespace OneGood.Core.Interfaces;

public interface IOutcomeTracker
{
    Task<Outcome?> GetOutcomeAsync(Guid actionId);
    Task CheckAndUpdateOutcomesAsync();
    Task SaveOutcomeAsync(Outcome outcome);
}
