using GymManager.App.Infrastructure;
using GymManager.Domain.Entities;
using GymManager.Domain.Exceptions;
using GymManager.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymManager.App.Services;

/// <summary>
/// 私教课会员业务服务（CRUD + 费用记录 + 消课记录）。
/// </summary>
public sealed class PrivateTrainingMemberService
{
    private readonly DbContextProvider _dbProvider;

    public PrivateTrainingMemberService(DbContextProvider dbProvider)
    {
        _dbProvider = dbProvider;
    }

    public async Task<List<PrivateTrainingMember>> SearchAsync(string? keyword, CancellationToken cancellationToken = default)
    {
        keyword = keyword?.Trim();

        await using var db = _dbProvider.CreateDbContext();

        IQueryable<PrivateTrainingMember> query = db.PrivateTrainingMembers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || x.Phone.Contains(keyword));
        }

        return await query
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PrivateTrainingMember?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = _dbProvider.CreateDbContext();

        return await db.PrivateTrainingMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<PrivateTrainingFeeRecord>> GetFeeRecordsAsync(int memberId, CancellationToken cancellationToken = default)
    {
        await using var db = _dbProvider.CreateDbContext();

        return await db.PrivateTrainingFeeRecords
            .AsNoTracking()
            .Where(x => x.MemberId == memberId)
            .OrderByDescending(x => x.PaidAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<PrivateTrainingSessionRecord>> GetSessionRecordsAsync(int memberId, CancellationToken cancellationToken = default)
    {
        await using var db = _dbProvider.CreateDbContext();

        return await db.PrivateTrainingSessionRecords
            .AsNoTracking()
            .Where(x => x.MemberId == memberId)
            .OrderByDescending(x => x.UsedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task CreateAsync(
        string name,
        Gender gender,
        string phone,
        decimal initialPaidAmount,
        int totalSessions,
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

        if (initialPaidAmount < 0)
        {
            throw new DomainValidationException("已交费用不能为负数。");
        }

        if (totalSessions < 0)
        {
            throw new DomainValidationException("总课程数不能为负数。");
        }

        await using var db = _dbProvider.CreateDbContext();

        var member = new PrivateTrainingMember
        {
            Name = name,
            Gender = gender,
            Phone = phone,
            PaidAmount = 0,
            TotalSessions = totalSessions,
            UsedSessions = 0
        };

        db.PrivateTrainingMembers.Add(member);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 如果创建时填写了初始缴费，则生成一条费用记录，保证“费用记录”可追溯。
        if (initialPaidAmount > 0)
        {
            db.PrivateTrainingFeeRecords.Add(new PrivateTrainingFeeRecord
            {
                MemberId = member.Id,
                Amount = initialPaidAmount,
                PaidAt = DateTime.Now,
                Note = "初始缴费"
            });

            member.PaidAmount += initialPaidAmount;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task UpdateAsync(
        int id,
        string name,
        Gender gender,
        string phone,
        int totalSessions,
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

        if (totalSessions < 0)
        {
            throw new DomainValidationException("总课程数不能为负数。");
        }

        await using var db = _dbProvider.CreateDbContext();

        var entity = await db.PrivateTrainingMembers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            throw new DomainValidationException("未找到该私教课会员。");
        }

        if (totalSessions < entity.UsedSessions)
        {
            throw new DomainValidationException("总课程数不能小于已使用课程数。");
        }

        entity.Name = name;
        entity.Gender = gender;
        entity.Phone = phone;
        entity.TotalSessions = totalSessions;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = _dbProvider.CreateDbContext();

        var entity = await db.PrivateTrainingMembers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        db.PrivateTrainingMembers.Remove(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddFeeAsync(
        int memberId,
        decimal amount,
        DateTime paidAt,
        string? note,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new DomainValidationException("缴费金额必须大于 0。");
        }

        note = note?.Trim();

        await using var db = _dbProvider.CreateDbContext();

        var member = await db.PrivateTrainingMembers.FirstOrDefaultAsync(x => x.Id == memberId, cancellationToken)
            .ConfigureAwait(false);

        if (member is null)
        {
            throw new DomainValidationException("未找到该私教课会员。");
        }

        db.PrivateTrainingFeeRecords.Add(new PrivateTrainingFeeRecord
        {
            MemberId = memberId,
            Amount = amount,
            PaidAt = paidAt,
            Note = note
        });

        member.PaidAmount += amount;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ConsumeSessionsAsync(
        int memberId,
        int sessionsUsed,
        DateTime usedAt,
        string? note,
        CancellationToken cancellationToken = default)
    {
        if (sessionsUsed < 1)
        {
            throw new DomainValidationException("消课次数必须大于等于 1。");
        }

        note = note?.Trim();

        await using var db = _dbProvider.CreateDbContext();

        var member = await db.PrivateTrainingMembers.FirstOrDefaultAsync(x => x.Id == memberId, cancellationToken)
            .ConfigureAwait(false);

        if (member is null)
        {
            throw new DomainValidationException("未找到该私教课会员。");
        }

        if (member.UsedSessions + sessionsUsed > member.TotalSessions)
        {
            throw new DomainValidationException("消课后将导致剩余课程为负数，请检查总课程数或本次消课次数。");
        }

        db.PrivateTrainingSessionRecords.Add(new PrivateTrainingSessionRecord
        {
            MemberId = memberId,
            SessionsUsed = sessionsUsed,
            UsedAt = usedAt,
            Note = note
        });

        member.UsedSessions += sessionsUsed;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

