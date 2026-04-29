using Microsoft.EntityFrameworkCore;
using OneGood.Core.Interfaces;
using OneGood.Core.Models;
using OneGood.Infrastructure.Data;

namespace OneGood.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly OneGoodDbContext _db;

    public UserRepository(OneGoodDbContext db) => _db = db;

    public async Task<User?> GetByIdAsync(Guid id)
        => await _db.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == id);

    public async Task<UserProfile?> GetProfileAsync(Guid userId)
        => await _db.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId || p.Id == userId);

    public async Task<UserProfile?> GetByEmailAsync(string email)
        => await _db.UserProfiles
            .FirstOrDefaultAsync(p => p.Email == email);

    public async Task<List<Guid>> GetRecentCauseIdsAsync(Guid userId, int days)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        return await _db.UserActions
            .Where(ua => ua.UserId == userId && ua.CompletedAt >= cutoff)
            .Include(ua => ua.DailyAction)
            .Select(ua => ua.DailyAction.CauseId)
            .Distinct()
            .ToListAsync();
    }

    public async Task RecordActionAsync(UserAction userAction)
    {
        _db.UserActions.Add(userAction);

        // Update profile stats
        var profile = await _db.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userAction.UserId || p.Id == userAction.UserId);

        if (profile is not null)
        {
            profile.ActionsCompleted++;
            profile.CurrentStreak = CalculateStreak(profile.CurrentStreak, DateTime.UtcNow);
            if (userAction.AmountDonated.HasValue)
                profile.TotalDonated += userAction.AmountDonated.Value;
        }

        await _db.SaveChangesAsync();
    }

    public async Task UpdateProfileAsync(UserProfile profile)
    {
        _db.UserProfiles.Update(profile);
        await _db.SaveChangesAsync();
    }

    public async Task CreateAsync(User user)
    {
        user.CreatedAt = DateTime.UtcNow;
        user.LastActiveAt = DateTime.UtcNow;
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
    }

    public async Task CreateAsync(UserProfile profile)
    {
        _db.UserProfiles.Add(profile);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(UserProfile profile)
    {
        _db.UserProfiles.Update(profile);
        await _db.SaveChangesAsync();
    }

    private static int CalculateStreak(int currentStreak, DateTime actionDate)
    {
        // Simple streak logic - in production, check last action date
        return currentStreak + 1;
    }
}
