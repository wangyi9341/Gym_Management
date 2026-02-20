using GymManager.Domain.Entities;

namespace GymManager.Data.Repositories;

public interface IAnnualCardMemberRepository
{
    Task<List<AnnualCardMember>> GetListAsync(string? keyword, CancellationToken cancellationToken = default);
    Task<AnnualCardMember?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(AnnualCardMember member, CancellationToken cancellationToken = default);
    Task UpdateAsync(AnnualCardMember member, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<List<AnnualCardMember>> GetExpiringAsync(DateTime today, int days, CancellationToken cancellationToken = default);
    Task<List<AnnualCardMember>> GetExpiredAsync(DateTime today, CancellationToken cancellationToken = default);
}

