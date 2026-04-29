using Microsoft.EntityFrameworkCore;
using OneGood.Core.Interfaces;
using OneGood.Core.Models;
using OneGood.Infrastructure.Data;
using System;
using System.Threading.Tasks;

namespace OneGood.Infrastructure.Repositories;

public class OutcomeRepository : IOutcomeTracker
{
    private readonly OneGoodDbContext _db;
    public OutcomeRepository(OneGoodDbContext db) => _db = db;

    public async Task<Outcome?> GetOutcomeAsync(Guid actionId)
        => await _db.Outcomes.FirstOrDefaultAsync(o => o.DailyActionId == actionId);

    public async Task CheckAndUpdateOutcomesAsync()
    {
        // Placeholder for future logic
    }

    public async Task SaveOutcomeAsync(Outcome outcome)
    {
        var existing = await _db.Outcomes.FirstOrDefaultAsync(o => o.DailyActionId == outcome.DailyActionId);
        if (existing is null)
        {
            _db.Outcomes.Add(outcome);
        }
        else
        {
            _db.Entry(existing).CurrentValues.SetValues(outcome);
        }
        await _db.SaveChangesAsync();
    }
}
