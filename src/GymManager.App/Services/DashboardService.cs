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

        var coachCountTask = db.Coaches.CountAsync(cancellationToken);
        var ptCountTask = db.PrivateTrainingMembers.CountAsync(cancellationToken);
        var annualCountTask = db.AnnualCardMembers.CountAsync(cancellationToken);

        var expiringQuery = db.AnnualCardMembers
            .AsNoTracking()
            .Where(x => x.EndDate >= expiringStart && x.EndDate < expiringEndExclusive)
            .OrderBy(x => x.EndDate);

        var expiredCountTask = db.AnnualCardMembers
            .AsNoTracking()
            .CountAsync(x => x.EndDate < today, cancellationToken);

        var expiringListTask = expiringQuery
            .Take(20)
            .ToListAsync(cancellationToken);

        var lowRemainingQuery = db.PrivateTrainingMembers
            .AsNoTracking()
            .Where(x => (x.TotalSessions - x.UsedSessions) <= lowRemainingThreshold)
            .OrderBy(x => x.TotalSessions - x.UsedSessions);

        var lowRemainingCountTask = lowRemainingQuery.CountAsync(cancellationToken);
        var lowRemainingListTask = lowRemainingQuery.Take(20).ToListAsync(cancellationToken);

        await Task.WhenAll(
            coachCountTask, ptCountTask, annualCountTask,
            expiredCountTask, expiringListTask,
            lowRemainingCountTask, lowRemainingListTask).ConfigureAwait(false);

        return new DashboardSnapshot
        {
            CoachCount = coachCountTask.Result,
            PrivateTrainingMemberCount = ptCountTask.Result,
            AnnualCardMemberCount = annualCountTask.Result,
            AnnualCardExpiringCount = expiringListTask.Result.Count,
            AnnualCardExpiredCount = expiredCountTask.Result,
            LowRemainingSessionsCount = lowRemainingCountTask.Result,
            ExpiringAnnualCards = expiringListTask.Result,
            LowRemainingSessionsMembers = lowRemainingListTask.Result
        };
    }
}

