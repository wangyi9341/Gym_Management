using GymManager.Domain.Entities;

namespace GymManager.Data.Repositories;

public interface IPrivateTrainingMemberRepository
{
    Task<List<PrivateTrainingMember>> GetListAsync(string? keyword, CancellationToken cancellationToken = default);
    Task<PrivateTrainingMember?> GetByIdAsync(int id, bool includeRecords, CancellationToken cancellationToken = default);
    Task AddAsync(PrivateTrainingMember member, CancellationToken cancellationToken = default);
    Task UpdateAsync(PrivateTrainingMember member, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task AddFeeRecordAsync(PrivateTrainingFeeRecord record, CancellationToken cancellationToken = default);
    Task AddSessionRecordAsync(PrivateTrainingSessionRecord record, CancellationToken cancellationToken = default);

    Task<List<PrivateTrainingFeeRecord>> GetFeeRecordsAsync(int memberId, CancellationToken cancellationToken = default);
    Task<List<PrivateTrainingSessionRecord>> GetSessionRecordsAsync(int memberId, CancellationToken cancellationToken = default);
}

