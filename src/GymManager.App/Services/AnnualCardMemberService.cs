using GymManager.App.Infrastructure;
using GymManager.Domain.Entities;
using GymManager.Domain.Exceptions;
using GymManager.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymManager.App.Services;

/// <summary>
/// 年卡会员业务服务（CRUD + 到期提醒 + 续费）。
/// </summary>
public sealed class AnnualCardMemberService
{
    private readonly DbContextProvider _dbProvider;

    public AnnualCardMemberService(DbContextProvider dbProvider)
    {
        _dbProvider = dbProvider;
    }

    public async Task<List<AnnualCardMember>> SearchAsync(string? keyword, CancellationToken cancellationToken = default)
    {
        keyword = keyword?.Trim();

        await using var db = _dbProvider.CreateDbContext();

        IQueryable<AnnualCardMember> query = db.AnnualCardMembers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || x.Phone.Contains(keyword));
        }

        return await query
            .OrderBy(x => x.EndDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<AnnualCardMember>> GetExpiringAsync(int expiringDays, CancellationToken cancellationToken = default)
    {
        expiringDays = Math.Max(0, expiringDays);

        var today = DateTime.Today;
        var start = today;
        var endExclusive = today.AddDays(expiringDays + 1);

        await using var db = _dbProvider.CreateDbContext();

        return await db.AnnualCardMembers
            .AsNoTracking()
            .Where(x => x.EndDate >= start && x.EndDate < endExclusive)
            .OrderBy(x => x.EndDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<AnnualCardMember>> GetExpiredAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;

        await using var db = _dbProvider.CreateDbContext();

        return await db.AnnualCardMembers
            .AsNoTracking()
            .Where(x => x.EndDate < today)
            .OrderByDescending(x => x.EndDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task CreateAsync(
        string name,
        Gender gender,
        string phone,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        name = (name ?? string.Empty).Trim();
        phone = (phone ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("会员姓名不能为空。");
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new DomainValidationException("电话号不能为空。");
        }

        startDate = startDate.Date;
        endDate = endDate.Date;

        if (endDate < startDate)
        {
            throw new DomainValidationException("年卡截止时间不能早于开通时间。");
        }

        await using var db = _dbProvider.CreateDbContext();

        db.AnnualCardMembers.Add(new AnnualCardMember
        {
            Name = name,
            Gender = gender,
            Phone = phone,
            StartDate = startDate,
            EndDate = endDate
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(
        int id,
        string name,
        Gender gender,
        string phone,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        name = (name ?? string.Empty).Trim();
        phone = (phone ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("会员姓名不能为空。");
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new DomainValidationException("电话号不能为空。");
        }

        startDate = startDate.Date;
        endDate = endDate.Date;

        if (endDate < startDate)
        {
            throw new DomainValidationException("年卡截止时间不能早于开通时间。");
        }

        await using var db = _dbProvider.CreateDbContext();

        var entity = await db.AnnualCardMembers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            throw new DomainValidationException("未找到该年卡会员。");
        }

        entity.Name = name;
        entity.Gender = gender;
        entity.Phone = phone;
        entity.StartDate = startDate;
        entity.EndDate = endDate;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RenewAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = _dbProvider.CreateDbContext();

        var entity = await db.AnnualCardMembers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            throw new DomainValidationException("未找到该年卡会员。");
        }

        var today = DateTime.Today;

        // 续费规则：
        // - 未过期：从原截止日期顺延 1 年
        // - 已过期：从今天重新开通 1 年
        if (entity.EndDate.Date >= today)
        {
            entity.EndDate = entity.EndDate.Date.AddYears(1);
        }
        else
        {
            entity.StartDate = today;
            entity.EndDate = today.AddYears(1);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = _dbProvider.CreateDbContext();

        var entity = await db.AnnualCardMembers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        db.AnnualCardMembers.Remove(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

