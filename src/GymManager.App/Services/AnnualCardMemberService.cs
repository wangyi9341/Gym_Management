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

    private const int MaxPauseDays = 3650;

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

        var list = await query
            .OrderBy(x => x.EndDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        await FillActivePauseInfoAsync(db, list, DateTime.Today, cancellationToken).ConfigureAwait(false);
        return list;
    }

    public async Task<List<AnnualCardPauseRecord>> GetPauseRecordsAsync(int memberId, CancellationToken cancellationToken = default)
    {
        await using var db = _dbProvider.CreateDbContext();

        return await db.AnnualCardPauseRecords
            .AsNoTracking()
            .Where(x => x.MemberId == memberId)
            .OrderByDescending(x => x.PauseStartDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<AnnualCardRenewRecord>> GetRenewRecordsAsync(int memberId, CancellationToken cancellationToken = default)
    {
        await using var db = _dbProvider.CreateDbContext();

        return await db.AnnualCardRenewRecords
            .AsNoTracking()
            .Where(x => x.MemberId == memberId)
            .OrderByDescending(x => x.RenewedAt)
            .ThenByDescending(x => x.CreatedAt)
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
        var now = DateTime.Now;

        var oldStartDate = entity.StartDate.Date;
        var oldEndDate = entity.EndDate.Date;

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

        var newStartDate = entity.StartDate.Date;
        var newEndDate = entity.EndDate.Date;

        db.AnnualCardRenewRecords.Add(new AnnualCardRenewRecord
        {
            MemberId = entity.Id,
            MemberName = entity.Name,
            MemberPhone = entity.Phone,
            RenewedAt = now,
            StartDateBefore = oldStartDate,
            EndDateBefore = oldEndDate,
            StartDateAfter = newStartDate,
            EndDateAfter = newEndDate
        });

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

    public async Task PauseAsync(int memberId, int pauseDays, CancellationToken cancellationToken = default)
    {
        if (pauseDays < 1 || pauseDays > MaxPauseDays)
        {
            throw new DomainValidationException($"停卡天数必须在 1~{MaxPauseDays} 之间。");
        }

        var today = DateTime.Today;
        var resumeDate = today.AddDays(pauseDays);

        await using var db = _dbProvider.CreateDbContext();

        var member = await db.AnnualCardMembers.FirstOrDefaultAsync(x => x.Id == memberId, cancellationToken)
            .ConfigureAwait(false);

        if (member is null)
        {
            throw new DomainValidationException("未找到该年卡会员。");
        }

        if (member.EndDate.Date < today)
        {
            throw new DomainValidationException("该会员年卡已过期，无法停卡。");
        }

        var hasActivePause = await db.AnnualCardPauseRecords
            .AsNoTracking()
            .AnyAsync(x => x.MemberId == memberId && x.PauseStartDate <= today && x.ResumeDate > today, cancellationToken)
            .ConfigureAwait(false);

        if (hasActivePause)
        {
            throw new DomainValidationException("该会员正在停卡中，无需重复停卡。");
        }

        var oldEndDate = member.EndDate.Date;
        var newEndDate = oldEndDate.AddDays(pauseDays);

        member.EndDate = newEndDate;

        db.AnnualCardPauseRecords.Add(new AnnualCardPauseRecord
        {
            MemberId = memberId,
            MemberName = member.Name,
            MemberPhone = member.Phone,
            PauseStartDate = today,
            ResumeDate = resumeDate,
            PauseDays = pauseDays,
            EndDateBefore = oldEndDate,
            EndDateAfter = newEndDate
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task FillActivePauseInfoAsync(
        DbContext db,
        IReadOnlyList<AnnualCardMember> members,
        DateTime today,
        CancellationToken cancellationToken)
    {
        if (members.Count == 0)
        {
            return;
        }

        var ids = members.Select(x => x.Id).Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        // 规则 A：停卡按约定天数生效（[PauseStartDate, ResumeDate) 视为停卡中）
        var baseDate = today.Date;

        // NOTE:
        // SQLite 默认参数上限较低（常见为 999）。当会员数量 > 999 时，`ids.Contains(...)` 可能触发
        // “too many SQL variables”，导致年卡页面加载崩溃。
        // 因此这里按批次查询，保证大数据量稳定性。
        var batchSize = GetInClauseBatchSize(db);

        var active = new List<(int MemberId, DateTime PauseStartDate, DateTime ResumeDate, DateTime CreatedAt)>();
        foreach (var batch in ids.Chunk(batchSize))
        {
            var chunk = batch; // avoid capturing iterator variable
            var list = await db.Set<AnnualCardPauseRecord>()
                .AsNoTracking()
                .Where(x => chunk.Contains(x.MemberId) && x.PauseStartDate <= baseDate && x.ResumeDate > baseDate)
                .Select(x => new { x.MemberId, x.PauseStartDate, x.ResumeDate, x.CreatedAt })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var item in list)
            {
                active.Add((item.MemberId, item.PauseStartDate, item.ResumeDate, item.CreatedAt));
            }
        }

        var activeByMemberId = active
            .GroupBy(x => x.MemberId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(y => y.PauseStartDate)
                    .ThenByDescending(y => y.CreatedAt)
                    .First());

        foreach (var m in members)
        {
            if (activeByMemberId.TryGetValue(m.Id, out var r))
            {
                m.PauseStartDate = r.PauseStartDate.Date;
                m.ResumeDate = r.ResumeDate.Date;
            }
            else
            {
                m.PauseStartDate = null;
                m.ResumeDate = null;
            }
        }

        static int GetInClauseBatchSize(DbContext db)
        {
            if (db.Database.IsSqlite())
            {
                // Keep some headroom for other parameters.
                return 900;
            }

            if (db.Database.IsSqlServer())
            {
                // SQL Server parameter limit is 2100.
                return 2000;
            }

            return 900;
        }
    }
}
