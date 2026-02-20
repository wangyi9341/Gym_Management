using GymManager.Data.Db;
using GymManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymManager.Data.Repositories;

public sealed class AnnualCardMemberRepository : IAnnualCardMemberRepository
{
    private readonly GymDbContext _db;

    public AnnualCardMemberRepository(GymDbContext db)
    {
        _db = db;
    }

    public async Task<List<AnnualCardMember>> GetListAsync(string? keyword, CancellationToken cancellationToken = default)
    {
        keyword = keyword?.Trim();

        IQueryable<AnnualCardMember> query = _db.AnnualCardMembers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || x.Phone.Contains(keyword));
        }

        return await query
            .OrderBy(x => x.EndDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<AnnualCardMember?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _db.AnnualCardMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(AnnualCardMember member, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);

        await _db.AnnualCardMembers.AddAsync(member, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(AnnualCardMember member, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);

        _db.AnnualCardMembers.Update(member);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.AnnualCardMembers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        _db.AnnualCardMembers.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AnnualCardMember>> GetExpiringAsync(DateTime today, int days, CancellationToken cancellationToken = default)
    {
        var start = today.Date;
        var endExclusive = today.Date.AddDays(days + 1);

        return await _db.AnnualCardMembers
            .AsNoTracking()
            .Where(x => x.EndDate >= start && x.EndDate < endExclusive)
            .OrderBy(x => x.EndDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<AnnualCardMember>> GetExpiredAsync(DateTime today, CancellationToken cancellationToken = default)
    {
        var baseDate = today.Date;

        return await _db.AnnualCardMembers
            .AsNoTracking()
            .Where(x => x.EndDate < baseDate)
            .OrderByDescending(x => x.EndDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
