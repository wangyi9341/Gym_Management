using GymManager.App.Infrastructure;
using GymManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymManager.App.Services;

public sealed class DashboardSnapshot
{
    public int CoachCount { get; init; }
    public int PrivateTrainingMemberCount { get; init; }
    public int AnnualCardMemberCount { get; init; }

    public int AnnualCardExpiringCount { get; init; }
    public int AnnualCardExpiredCount { get; init; }
    public int LowRemainingSessionsCount { get; init; }

    public List<AnnualCardMember> ExpiringAnnualCards { get; init; } = new();
    public List<PrivateTrainingMember> LowRemainingSessionsMembers { get; init; } = new();
}

/// <summary>
/// 首页仪表盘数据聚合服务。
/// </summary>
public sealed class DashboardService
{
    private readonly DbContextProvider _dbProvider;

    public DashboardService(DbContextProvider dbProvider)
    {
        _dbProvider = dbProvider;
    }

    public async Task<DashboardSnapshot> GetSnapshotAsync(
        int annualCardExpiringDays,
        int lowRemainingThreshold,
        CancellationToken cancellationToken = default)
    {
        annualCardExpiringDays = Math.Max(0, annualCardExpiringDays);
        lowRemainingThreshold = Math.Max(0, lowRemainingThreshold);

        var today = DateTime.Today;
        var expiringStart = today;
        var expiringEndExclusive = today.AddDays(annualCardExpiringDays + 1);

        await using var db = _dbProvider.CreateDbContext();

        // 注意：
        // EF Core 的 DbContext 不是线程安全的，不建议在同一个 DbContext 上并发执行多个异步查询。
        // 为兼容 SQLite / SQL Server，这里按顺序执行各查询。
        var coachCount = await db.Coaches.CountAsync(cancellationToken).ConfigureAwait(false);
        var ptCount = await db.PrivateTrainingMembers.CountAsync(cancellationToken).ConfigureAwait(false);
        var annualCount = await db.AnnualCardMembers.CountAsync(cancellationToken).ConfigureAwait(false);

        var expiringCount = await db.AnnualCardMembers
            .AsNoTracking()
            .CountAsync(x => x.EndDate >= expiringStart && x.EndDate < expiringEndExclusive, cancellationToken)
            .ConfigureAwait(false);

        var expiredCount = await db.AnnualCardMembers
            .AsNoTracking()
            .CountAsync(x => x.EndDate < today, cancellationToken)
            .ConfigureAwait(false);

        var expiringList = await db.AnnualCardMembers
            .AsNoTracking()
            .Where(x => x.EndDate >= expiringStart && x.EndDate < expiringEndExclusive)
            .OrderBy(x => x.EndDate)
            .Take(20)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var lowRemainingCount = await db.PrivateTrainingMembers
            .AsNoTracking()
            .CountAsync(x => (x.TotalSessions - x.UsedSessions) <= lowRemainingThreshold, cancellationToken)
            .ConfigureAwait(false);

        var lowRemainingList = await db.PrivateTrainingMembers
            .AsNoTracking()
            .Where(x => (x.TotalSessions - x.UsedSessions) <= lowRemainingThreshold)
            .OrderBy(x => x.TotalSessions - x.UsedSessions)
            .Take(20)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DashboardSnapshot
        {
            CoachCount = coachCount,
            PrivateTrainingMemberCount = ptCount,
            AnnualCardMemberCount = annualCount,
            AnnualCardExpiringCount = expiringCount,
            AnnualCardExpiredCount = expiredCount,
            LowRemainingSessionsCount = lowRemainingCount,
            ExpiringAnnualCards = expiringList,
            LowRemainingSessionsMembers = lowRemainingList
        };
    }
}
