using GymManager.Data.Db;
using GymManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymManager.Data.Repositories;

public sealed class PrivateTrainingMemberRepository : IPrivateTrainingMemberRepository
{
    private readonly GymDbContext _db;

    public PrivateTrainingMemberRepository(GymDbContext db)
    {
        _db = db;
    }

    public async Task<List<PrivateTrainingMember>> GetListAsync(string? keyword, CancellationToken cancellationToken = default)
    {
        keyword = keyword?.Trim();

        IQueryable<PrivateTrainingMember> query = _db.PrivateTrainingMembers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || x.Phone.Contains(keyword));
        }

        return await query
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PrivateTrainingMember?> GetByIdAsync(int id, bool includeRecords, CancellationToken cancellationToken = default)
    {
        IQueryable<PrivateTrainingMember> query = _db.PrivateTrainingMembers.AsNoTracking();

        if (includeRecords)
        {
            query = query
                .Include(x => x.FeeRecords)
                .Include(x => x.SessionRecords);
        }

        return await query
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(PrivateTrainingMember member, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);

        await _db.PrivateTrainingMembers.AddAsync(member, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(PrivateTrainingMember member, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);

        _db.PrivateTrainingMembers.Update(member);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.PrivateTrainingMembers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        _db.PrivateTrainingMembers.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddFeeRecordAsync(PrivateTrainingFeeRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await _db.PrivateTrainingFeeRecords.AddAsync(record, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddSessionRecordAsync(PrivateTrainingSessionRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await _db.PrivateTrainingSessionRecords.AddAsync(record, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PrivateTrainingFeeRecord>> GetFeeRecordsAsync(int memberId, CancellationToken cancellationToken = default)
    {
        return await _db.PrivateTrainingFeeRecords
            .AsNoTracking()
            .Where(x => x.MemberId == memberId)
            .OrderByDescending(x => x.PaidAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<PrivateTrainingSessionRecord>> GetSessionRecordsAsync(int memberId, CancellationToken cancellationToken = default)
    {
        return await _db.PrivateTrainingSessionRecords
            .AsNoTracking()
            .Where(x => x.MemberId == memberId)
            .OrderByDescending(x => x.UsedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

