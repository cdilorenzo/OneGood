using OneGood.Core.Enums;
using OneGood.Core.Models;

namespace OneGood.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<UserProfile?> GetProfileAsync(Guid userId);
    Task<UserProfile?> GetByEmailAsync(string email);
    Task<List<Guid>> GetRecentCauseIdsAsync(Guid userId, int days);
    Task RecordActionAsync(UserAction userAction);
    Task UpdateProfileAsync(UserProfile profile);
    Task CreateAsync(User user);
    Task CreateAsync(UserProfile profile);
    Task UpdateAsync(UserProfile profile);
    Task<bool> DeleteAsync(Guid profileId);
}
