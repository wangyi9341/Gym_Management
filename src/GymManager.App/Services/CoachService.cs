using GymManager.App.Infrastructure;
using GymManager.Domain.Entities;
using GymManager.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace GymManager.App.Services;

/// <summary>
/// 教练业务服务（CRUD + 规则校验）。
/// </summary>
public sealed class CoachService
{
    private readonly DbContextProvider _dbProvider;

    public CoachService(DbContextProvider dbProvider)
    {
        _dbProvider = dbProvider;
    }

    public async Task<List<Coach>> SearchAsync(string? keyword, CancellationToken cancellationToken = default)
    {
        keyword = keyword?.Trim();

        await using var db = _dbProvider.CreateDbContext();

        IQueryable<Coach> query = db.Coaches.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.EmployeeNo.Contains(keyword) || x.Name.Contains(keyword));
        }

        return await query
            .OrderBy(x => x.EmployeeNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task CreateAsync(string employeeNo, string name, CancellationToken cancellationToken = default)
    {
        employeeNo = (employeeNo ?? string.Empty).Trim();
        name = (name ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(employeeNo))
        {
            throw new DomainValidationException("工号不能为空。");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("教练姓名不能为空。");
        }

        await using var db = _dbProvider.CreateDbContext();

        var exists = await db.Coaches.AnyAsync(x => x.EmployeeNo == employeeNo, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            throw new DomainValidationException($"工号“{employeeNo}”已存在，请更换工号。");
        }

        db.Coaches.Add(new Coach
        {
            EmployeeNo = employeeNo,
            Name = name
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(string employeeNo, string name, CancellationToken cancellationToken = default)
    {
        employeeNo = (employeeNo ?? string.Empty).Trim();
        name = (name ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(employeeNo))
        {
            throw new DomainValidationException("工号不能为空。");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("教练姓名不能为空。");
        }

        await using var db = _dbProvider.CreateDbContext();

        var entity = await db.Coaches.FirstOrDefaultAsync(x => x.EmployeeNo == employeeNo, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            throw new DomainValidationException($"未找到工号为“{employeeNo}”的教练。");
        }

        entity.Name = name;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string employeeNo, CancellationToken cancellationToken = default)
    {
        employeeNo = (employeeNo ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(employeeNo))
        {
            return;
        }

        await using var db = _dbProvider.CreateDbContext();

        var entity = await db.Coaches.FirstOrDefaultAsync(x => x.EmployeeNo == employeeNo, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        db.Coaches.Remove(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

