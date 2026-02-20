using GymManager.Data.Db;
using GymManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymManager.Data.Repositories;

public sealed class CoachRepository : ICoachRepository
{
    private readonly GymDbContext _db;

    public CoachRepository(GymDbContext db)
    {
        _db = db;
    }

    public Task<bool> ExistsAsync(string employeeNo, CancellationToken cancellationToken = default)
    {
        employeeNo = (employeeNo ?? string.Empty).Trim();
        return _db.Coaches.AnyAsync(x => x.EmployeeNo == employeeNo, cancellationToken);
    }

    public async Task<List<Coach>> GetListAsync(string? keyword, CancellationToken cancellationToken = default)
    {
        keyword = keyword?.Trim();

        IQueryable<Coach> query = _db.Coaches.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.EmployeeNo.Contains(keyword) || x.Name.Contains(keyword));
        }

        return await query
            .OrderBy(x => x.EmployeeNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<Coach?> GetByEmployeeNoAsync(string employeeNo, CancellationToken cancellationToken = default)
    {
        employeeNo = (employeeNo ?? string.Empty).Trim();

        return _db.Coaches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmployeeNo == employeeNo, cancellationToken);
    }

    public async Task AddAsync(Coach coach, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coach);

        await _db.Coaches.AddAsync(coach, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Coach coach, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coach);

        _db.Coaches.Update(coach);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string employeeNo, CancellationToken cancellationToken = default)
    {
        employeeNo = (employeeNo ?? string.Empty).Trim();

        var entity = await _db.Coaches.FirstOrDefaultAsync(x => x.EmployeeNo == employeeNo, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        _db.Coaches.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

