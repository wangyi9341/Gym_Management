using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using GymManager.App.Infrastructure;
using GymManager.Domain.Entities;
using GymManager.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymManager.App.Services;

public sealed record ExcelImportResult(
    int Added,
    int Updated,
    int Skipped,
    int PauseRecordsAdded,
    int PauseRecordsSkipped,
    int RenewRecordsAdded,
    int RenewRecordsSkipped,
    IReadOnlyList<string> Errors);

public sealed record PrivateTrainingExcelImportResult(
    int MembersAdded,
    int MembersUpdated,
    int MembersSkipped,
    int FeeRecordsAdded,
    int FeeRecordsSkipped,
    int SessionRecordsAdded,
    int SessionRecordsSkipped,
    IReadOnlyList<string> Errors);

public sealed class ExcelTransferService
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly DbContextProvider _dbProvider;

    public ExcelTransferService(DbContextProvider dbProvider)
    {
        _dbProvider = dbProvider;
    }

    // SQLite 默认参数上限较低（常见为 999），当导入/导出涉及大量 Id/Phone 的 IN 查询时，
    // 可能出现 “too many SQL variables”。这里统一使用批量查询的 batch size。
    private static int GetInClauseBatchSize(DbContext db)
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

    public async Task ExportAnnualCardMembersAsync(string filePath, CancellationToken cancellationToken = default)
    {
        filePath = (filePath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("导出路径不能为空。", nameof(filePath));
        }

        await using var db = _dbProvider.CreateDbContext();
        var list = await db.AnnualCardMembers
            .AsNoTracking()
            .OrderBy(x => x.EndDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var pauseRecords = await db.AnnualCardPauseRecords
            .AsNoTracking()
            .OrderByDescending(x => x.PauseStartDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var renewRecords = await db.AnnualCardRenewRecords
            .AsNoTracking()
            .OrderByDescending(x => x.RenewedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        EnsureDirectoryExists(filePath);

        await Task.Run(() =>
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("年卡会员");

            WriteHeader(ws, new[]
            {
                "Id",
                "姓名",
                "性别",
                "电话",
                "开通日期",
                "截止日期"
            });

            var row = 2;
            foreach (var item in list)
            {
                ws.Cell(row, 1).Value = item.Id;
                ws.Cell(row, 2).Value = item.Name;
                ws.Cell(row, 3).Value = GenderToText(item.Gender);
                ws.Cell(row, 4).Value = item.Phone;

                ws.Cell(row, 5).Value = item.StartDate.Date;
                ws.Cell(row, 5).Style.DateFormat.Format = DateFormat;

                ws.Cell(row, 6).Value = item.EndDate.Date;
                ws.Cell(row, 6).Style.DateFormat.Format = DateFormat;

                row++;
            }

            ws.Columns().AdjustToContents();

            var wsPauses = wb.Worksheets.Add("停卡记录");
            WriteHeader(wsPauses, new[]
            {
                "Id",
                "会员Id",
                "会员姓名",
                "会员电话",
                "停卡开始日期",
                "恢复日期",
                "停卡天数",
                "停卡前截止日期",
                "停卡后截止日期",
                "备注",
                "创建时间"
            });

            row = 2;
            foreach (var p in pauseRecords)
            {
                wsPauses.Cell(row, 1).Value = p.Id;
                wsPauses.Cell(row, 2).Value = p.MemberId;
                wsPauses.Cell(row, 3).Value = p.MemberName;
                wsPauses.Cell(row, 4).Value = p.MemberPhone;

                wsPauses.Cell(row, 5).Value = p.PauseStartDate.Date;
                wsPauses.Cell(row, 5).Style.DateFormat.Format = DateFormat;

                wsPauses.Cell(row, 6).Value = p.ResumeDate.Date;
                wsPauses.Cell(row, 6).Style.DateFormat.Format = DateFormat;

                wsPauses.Cell(row, 7).Value = p.PauseDays;

                wsPauses.Cell(row, 8).Value = p.EndDateBefore.Date;
                wsPauses.Cell(row, 8).Style.DateFormat.Format = DateFormat;

                wsPauses.Cell(row, 9).Value = p.EndDateAfter.Date;
                wsPauses.Cell(row, 9).Style.DateFormat.Format = DateFormat;

                wsPauses.Cell(row, 10).Value = p.Note ?? string.Empty;

                wsPauses.Cell(row, 11).Value = p.CreatedAt;
                wsPauses.Cell(row, 11).Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss";

                row++;
            }

            wsPauses.Columns().AdjustToContents();

            var wsRenews = wb.Worksheets.Add("续费记录");
            WriteHeader(wsRenews, new[]
            {
                "Id",
                "会员Id",
                "会员姓名",
                "会员电话",
                "续费时间",
                "续费前开通日期",
                "续费前截止日期",
                "续费后开通日期",
                "续费后截止日期",
                "备注",
                "创建时间"
            });

            row = 2;
            foreach (var r in renewRecords)
            {
                wsRenews.Cell(row, 1).Value = r.Id;
                wsRenews.Cell(row, 2).Value = r.MemberId;
                wsRenews.Cell(row, 3).Value = r.MemberName;
                wsRenews.Cell(row, 4).Value = r.MemberPhone;

                wsRenews.Cell(row, 5).Value = r.RenewedAt;
                wsRenews.Cell(row, 5).Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss";

                wsRenews.Cell(row, 6).Value = r.StartDateBefore.Date;
                wsRenews.Cell(row, 6).Style.DateFormat.Format = DateFormat;

                wsRenews.Cell(row, 7).Value = r.EndDateBefore.Date;
                wsRenews.Cell(row, 7).Style.DateFormat.Format = DateFormat;

                wsRenews.Cell(row, 8).Value = r.StartDateAfter.Date;
                wsRenews.Cell(row, 8).Style.DateFormat.Format = DateFormat;

                wsRenews.Cell(row, 9).Value = r.EndDateAfter.Date;
                wsRenews.Cell(row, 9).Style.DateFormat.Format = DateFormat;

                wsRenews.Cell(row, 10).Value = r.Note ?? string.Empty;

                wsRenews.Cell(row, 11).Value = r.CreatedAt;
                wsRenews.Cell(row, 11).Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss";

                row++;
            }

            wsRenews.Columns().AdjustToContents();
            wb.SaveAs(filePath);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ExcelImportResult> ImportAnnualCardMembersAsync(string filePath, CancellationToken cancellationToken = default)
    {
        filePath = (filePath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("导入路径不能为空。", nameof(filePath));
        }

        var errors = new List<string>();

        var rows = await Task.Run(() => ReadAnnualCardImportRows(filePath, errors), cancellationToken)
            .ConfigureAwait(false);

        var pauseRows = await Task.Run(() => ReadAnnualCardPauseImportRows(filePath, errors), cancellationToken)
            .ConfigureAwait(false);

        var renewRows = await Task.Run(() => ReadAnnualCardRenewImportRows(filePath, errors), cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0 && pauseRows.Count == 0 && renewRows.Count == 0)
        {
            return new ExcelImportResult(0, 0, 0, 0, 0, 0, 0, errors);
        }

        var added = 0;
        var updated = 0;
        var skipped = 0;
        var pauseAdded = 0;
        var pauseSkipped = 0;
        var renewAdded = 0;
        var renewSkipped = 0;

        await using var db = _dbProvider.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var batchSize = GetInClauseBatchSize(db);

        try
        {
            if (rows.Count > 0)
            {
                var importIds = rows.Where(x => x.Id > 0).Select(x => x.Id).Distinct().ToList();
                var importPhones = rows.Where(x => !string.IsNullOrWhiteSpace(x.Phone)).Select(x => x.Phone).Distinct().ToList();

                var existingById = new Dictionary<int, AnnualCardMember>();
                if (importIds.Count > 0)
                {
                    foreach (var batch in importIds.Chunk(batchSize))
                    {
                        var chunk = batch;
                        var list = await db.AnnualCardMembers
                            .Where(x => chunk.Contains(x.Id))
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

                        foreach (var item in list)
                        {
                            existingById[item.Id] = item;
                        }
                    }
                }

                var existingPhoneList = new List<AnnualCardMember>();
                if (importPhones.Count > 0)
                {
                    foreach (var batch in importPhones.Chunk(batchSize))
                    {
                        var chunk = batch;
                        var list = await db.AnnualCardMembers
                            .Where(x => chunk.Contains(x.Phone))
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

                        existingPhoneList.AddRange(list);
                    }
                }

                var phoneGroups = existingPhoneList
                    .GroupBy(x => x.Phone)
                    .ToDictionary(x => x.Key, x => x.ToList());

                foreach (var row in rows)
                {
                    var validationError = ValidateAnnualCardRow(row);
                    if (validationError is not null)
                    {
                        errors.Add($"第 {row.RowNumber} 行：{validationError}");
                        skipped++;
                        continue;
                    }

                    var entity = default(AnnualCardMember);

                    if (row.Id > 0 && existingById.TryGetValue(row.Id, out var byId))
                    {
                        entity = byId;
                        updated++;
                    }
                    else if (!string.IsNullOrWhiteSpace(row.Phone) && phoneGroups.TryGetValue(row.Phone, out var candidates))
                    {
                        if (candidates.Count == 1)
                        {
                            entity = candidates[0];
                            updated++;
                        }
                        else
                        {
                            errors.Add($"第 {row.RowNumber} 行：电话 {row.Phone} 在数据库中存在 {candidates.Count} 条记录，无法判断更新对象，请填写 Id。");
                            skipped++;
                            continue;
                        }
                    }
                    else
                    {
                        entity = new AnnualCardMember();
                        db.AnnualCardMembers.Add(entity);
                        added++;
                    }

                    entity.Name = row.Name;
                    entity.Gender = row.Gender;
                    entity.Phone = row.Phone;
                    entity.StartDate = row.StartDate.Date;
                    entity.EndDate = row.EndDate.Date;
                }

                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            if (pauseRows.Count > 0)
            {
                var recordMemberIds = pauseRows
                    .Select(x => x.MemberId)
                    .Where(x => x is > 0)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToList();

                var recordPhones = pauseRows
                    .Select(x => x.MemberPhone)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct()
                    .ToList();

                HashSet<int> existingMemberIds;
                if (recordMemberIds.Count == 0)
                {
                    existingMemberIds = new HashSet<int>();
                }
                else
                {
                    var existingMemberIdList = new List<int>();
                    foreach (var batch in recordMemberIds.Chunk(batchSize))
                    {
                        var chunk = batch;
                        var list = await db.AnnualCardMembers
                            .AsNoTracking()
                            .Where(x => chunk.Contains(x.Id))
                            .Select(x => x.Id)
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

                        existingMemberIdList.AddRange(list);
                    }

                    existingMemberIds = existingMemberIdList.ToHashSet();
                }

                Dictionary<string, List<int>> recordPhoneGroups;
                if (recordPhones.Count == 0)
                {
                    recordPhoneGroups = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    var phoneMembers = new List<(int Id, string Phone)>();
                    foreach (var batch in recordPhones.Chunk(batchSize))
                    {
                        var chunk = batch;
                        var list = await db.AnnualCardMembers
                            .AsNoTracking()
                            .Where(x => chunk.Contains(x.Phone))
                            .Select(x => new { x.Id, x.Phone })
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

                        phoneMembers.AddRange(list.Select(x => (x.Id, x.Phone)));
                    }

                    recordPhoneGroups = phoneMembers
                        .GroupBy(x => x.Phone)
                        .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToList());
                }

                int? ResolveMemberId(int? memberId, string memberPhone, int rowNumber, out string? errorMessage)
                {
                    errorMessage = null;

                    if (memberId is > 0 && existingMemberIds.Contains(memberId.Value))
                    {
                        return memberId.Value;
                    }

                    var phone = (memberPhone ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(phone))
                    {
                        errorMessage = $"[停卡记录] 第 {rowNumber} 行：缺少会员电话/会员Id，无法定位会员。";
                        return null;
                    }

                    if (!recordPhoneGroups.TryGetValue(phone, out var candidates) || candidates.Count == 0)
                    {
                        errorMessage = $"[停卡记录] 第 {rowNumber} 行：找不到电话为 {phone} 的会员。";
                        return null;
                    }

                    if (candidates.Count != 1)
                    {
                        errorMessage = $"[停卡记录] 第 {rowNumber} 行：电话 {phone} 在数据库中存在 {candidates.Count} 条记录，无法定位会员，请使用会员Id。";
                        return null;
                    }

                    return candidates[0];
                }

                var resolvedPauses = new List<(int RowNumber, int MemberId, string MemberName, string MemberPhone, int PauseDays, DateTime PauseStartDate, DateTime ResumeDate, DateTime EndDateBefore, DateTime EndDateAfter, string? Note, DateTime? CreatedAt)>();

                foreach (var row in pauseRows)
                {
                    var memberId = ResolveMemberId(row.MemberId, row.MemberPhone, row.RowNumber, out var resolveError);
                    if (memberId is null)
                    {
                        errors.Add(resolveError!);
                        pauseSkipped++;
                        continue;
                    }

                    if (row.PauseDays < 1)
                    {
                        errors.Add($"[停卡记录] 第 {row.RowNumber} 行：停卡天数必须 >= 1。");
                        pauseSkipped++;
                        continue;
                    }

                    if (row.PauseStartDate == default)
                    {
                        errors.Add($"[停卡记录] 第 {row.RowNumber} 行：停卡开始日期不能为空。");
                        pauseSkipped++;
                        continue;
                    }

                    var pauseStart = row.PauseStartDate.Date;
                    var resumeDate = row.ResumeDate == default ? pauseStart.AddDays(row.PauseDays) : row.ResumeDate.Date;
                    if (resumeDate <= pauseStart)
                    {
                        errors.Add($"[停卡记录] 第 {row.RowNumber} 行：恢复日期必须晚于停卡开始日期。");
                        pauseSkipped++;
                        continue;
                    }

                    var before = row.EndDateBefore == default ? default : row.EndDateBefore.Date;
                    var after = row.EndDateAfter == default ? default : row.EndDateAfter.Date;

                    if (before == default && after == default)
                    {
                        errors.Add($"[停卡记录] 第 {row.RowNumber} 行：缺少停卡前/后截止日期。请使用系统导出的模板。");
                        pauseSkipped++;
                        continue;
                    }

                    if (before == default)
                    {
                        before = after.AddDays(-row.PauseDays);
                    }

                    if (after == default)
                    {
                        after = before.AddDays(row.PauseDays);
                    }

                    resolvedPauses.Add((
                        RowNumber: row.RowNumber,
                        MemberId: memberId.Value,
                        MemberName: row.MemberName,
                        MemberPhone: row.MemberPhone,
                        PauseDays: row.PauseDays,
                        PauseStartDate: pauseStart,
                        ResumeDate: resumeDate,
                        EndDateBefore: before,
                        EndDateAfter: after,
                        Note: row.Note,
                        CreatedAt: row.CreatedAt));
                }

                if (resolvedPauses.Count > 0)
                {
                    var involvedMemberIds = resolvedPauses
                        .Select(x => x.MemberId)
                        .Distinct()
                        .ToList();

                    var involvedMembers = new List<AnnualCardMember>();
                    foreach (var batch in involvedMemberIds.Chunk(batchSize))
                    {
                        var chunk = batch;
                        var list = await db.AnnualCardMembers
                            .Where(x => chunk.Contains(x.Id))
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

                        involvedMembers.AddRange(list);
                    }

                    var memberById = involvedMembers.ToDictionary(x => x.Id);

                    var pauseKeys = new HashSet<PauseKey>();
                    foreach (var batch in involvedMemberIds.Chunk(batchSize))
                    {
                        var chunk = batch;
                        var existingPauses = await db.AnnualCardPauseRecords
                            .AsNoTracking()
                            .Where(x => chunk.Contains(x.MemberId))
                            .Select(x => new { x.MemberId, x.PauseStartDate, x.ResumeDate, x.PauseDays, x.EndDateAfter, x.Note })
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

                        foreach (var p in existingPauses)
                        {
                            pauseKeys.Add(new PauseKey(
                                MemberId: p.MemberId,
                                PauseStartDate: p.PauseStartDate.Date,
                                ResumeDate: p.ResumeDate.Date,
                                PauseDays: p.PauseDays,
                                EndDateAfter: p.EndDateAfter.Date,
                                Note: NormalizeNote(p.Note)));
                        }
                    }

                    foreach (var row in resolvedPauses)
                    {
                        if (!memberById.TryGetValue(row.MemberId, out var member))
                        {
                            errors.Add($"[停卡记录] 第 {row.RowNumber} 行：找不到会员Id {row.MemberId}。");
                            pauseSkipped++;
                            continue;
                        }

                        var note = NormalizeNote(row.Note);
                        var key = new PauseKey(
                            MemberId: row.MemberId,
                            PauseStartDate: row.PauseStartDate,
                            ResumeDate: row.ResumeDate,
                            PauseDays: row.PauseDays,
                            EndDateAfter: row.EndDateAfter,
                            Note: note);

                        if (!pauseKeys.Add(key))
                        {
                            pauseSkipped++;
                            continue;
                        }

                        var record = new AnnualCardPauseRecord
                        {
                            MemberId = row.MemberId,
                            MemberName = string.IsNullOrWhiteSpace(row.MemberName) ? member.Name : row.MemberName.Trim(),
                            MemberPhone = string.IsNullOrWhiteSpace(row.MemberPhone) ? member.Phone : row.MemberPhone.Trim(),
                            PauseStartDate = row.PauseStartDate,
                            ResumeDate = row.ResumeDate,
                            PauseDays = row.PauseDays,
                            EndDateBefore = row.EndDateBefore,
                            EndDateAfter = row.EndDateAfter,
                            Note = note
                        };

                        if (row.CreatedAt is not null && row.CreatedAt.Value != default)
                        {
                            record.CreatedAt = row.CreatedAt.Value;
                        }

                        db.AnnualCardPauseRecords.Add(record);

                        if (row.EndDateAfter.Date > member.EndDate.Date)
                        {
                            member.EndDate = row.EndDateAfter.Date;
                        }

                        pauseAdded++;
                    }

                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            if (renewRows.Count > 0)
            {
                var recordMemberIds = renewRows
                    .Select(x => x.MemberId)
                    .Where(x => x is > 0)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToList();

                var recordPhones = renewRows
                    .Select(x => x.MemberPhone)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct()
                    .ToList();

                HashSet<int> existingMemberIds;
                if (recordMemberIds.Count == 0)
                {
                    existingMemberIds = new HashSet<int>();
                }
                else
                {
                    var existingMemberIdList = new List<int>();
                    foreach (var batch in recordMemberIds.Chunk(batchSize))
                    {
                        var chunk = batch;
                        var list = await db.AnnualCardMembers
                            .AsNoTracking()
                            .Where(x => chunk.Contains(x.Id))
                            .Select(x => x.Id)
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

                        existingMemberIdList.AddRange(list);
                    }

                    existingMemberIds = existingMemberIdList.ToHashSet();
                }

                Dictionary<string, List<int>> recordPhoneGroups;
                if (recordPhones.Count == 0)
                {
                    recordPhoneGroups = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    var phoneMembers = new List<(int Id, string Phone)>();
                    foreach (var batch in recordPhones.Chunk(batchSize))
                    {
                        var chunk = batch;
                        var list = await db.AnnualCardMembers
                            .AsNoTracking()
                            .Where(x => chunk.Contains(x.Phone))
                            .Select(x => new { x.Id, x.Phone })
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

                        phoneMembers.AddRange(list.Select(x => (x.Id, x.Phone)));
                    }

                    recordPhoneGroups = phoneMembers
                        .GroupBy(x => x.Phone)
                        .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToList());
                }

                int? ResolveMemberId(int? memberId, string memberPhone, int rowNumber, out string? errorMessage)
                {
                    errorMessage = null;

                    if (memberId is > 0 && existingMemberIds.Contains(memberId.Value))
                    {
                        return memberId.Value;
                    }

                    var phone = (memberPhone ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(phone))
                    {
                        errorMessage = $"[续费记录] 第 {rowNumber} 行：缺少会员电话/会员Id，无法定位会员。";
                        return null;
                    }

                    if (!recordPhoneGroups.TryGetValue(phone, out var candidates) || candidates.Count == 0)
                    {
                        errorMessage = $"[续费记录] 第 {rowNumber} 行：找不到电话为 {phone} 的会员。";
                        return null;
                    }

                    if (candidates.Count != 1)
                    {
                        errorMessage = $"[续费记录] 第 {rowNumber} 行：电话 {phone} 在数据库中存在 {candidates.Count} 条记录，无法定位会员，请使用会员Id。";
                        return null;
                    }

                    return candidates[0];
                }

                var resolvedRenews = new List<(int RowNumber, int MemberId, string MemberName, string MemberPhone, DateTime RenewedAt, DateTime StartDateBefore, DateTime EndDateBefore, DateTime StartDateAfter, DateTime EndDateAfter, string? Note, DateTime? CreatedAt)>();

                foreach (var row in renewRows)
                {
                    var memberId = ResolveMemberId(row.MemberId, row.MemberPhone, row.RowNumber, out var resolveError);
                    if (memberId is null)
                    {
                        errors.Add(resolveError!);
                        renewSkipped++;
                        continue;
                    }

                    if (row.RenewedAt == default)
                    {
                        errors.Add($"[续费记录] 第 {row.RowNumber} 行：续费时间不能为空。");
                        renewSkipped++;
                        continue;
                    }

                    var startBefore = row.StartDateBefore.Date;
                    var endBefore = row.EndDateBefore.Date;
                    var startAfter = row.StartDateAfter.Date;
                    var endAfter = row.EndDateAfter.Date;

                    if (startBefore == default || endBefore == default || startAfter == default || endAfter == default)
                    {
                        errors.Add($"[续费记录] 第 {row.RowNumber} 行：缺少续费前/后开通日期或截止日期。请使用系统导出的模板。");
                        renewSkipped++;
                        continue;
                    }

                    if (endBefore < startBefore)
                    {
                        errors.Add($"[续费记录] 第 {row.RowNumber} 行：续费前截止日期不能早于开通日期。");
                        renewSkipped++;
                        continue;
                    }

                    if (endAfter < startAfter)
                    {
                        errors.Add($"[续费记录] 第 {row.RowNumber} 行：续费后截止日期不能早于开通日期。");
                        renewSkipped++;
                        continue;
                    }

                    if (endAfter <= endBefore)
                    {
                        errors.Add($"[续费记录] 第 {row.RowNumber} 行：续费后截止日期必须晚于续费前截止日期。");
                        renewSkipped++;
                        continue;
                    }

                    resolvedRenews.Add((
                        RowNumber: row.RowNumber,
                        MemberId: memberId.Value,
                        MemberName: row.MemberName,
                        MemberPhone: row.MemberPhone,
                        RenewedAt: row.RenewedAt,
                        StartDateBefore: startBefore,
                        EndDateBefore: endBefore,
                        StartDateAfter: startAfter,
                        EndDateAfter: endAfter,
                        Note: row.Note,
                        CreatedAt: row.CreatedAt));
                }

                if (resolvedRenews.Count > 0)
                {
                    var involvedMemberIds = resolvedRenews
                        .Select(x => x.MemberId)
                        .Distinct()
                        .ToList();

                    var involvedMembers = new List<AnnualCardMember>();
                    foreach (var batch in involvedMemberIds.Chunk(batchSize))
                    {
                        var chunk = batch;
                        var list = await db.AnnualCardMembers
                            .Where(x => chunk.Contains(x.Id))
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

                        involvedMembers.AddRange(list);
                    }

                    var memberById = involvedMembers.ToDictionary(x => x.Id);

                    var renewKeys = new HashSet<RenewKey>();
                    foreach (var batch in involvedMemberIds.Chunk(batchSize))
                    {
                        var chunk = batch;
                        var existingRenews = await db.AnnualCardRenewRecords
                            .AsNoTracking()
                            .Where(x => chunk.Contains(x.MemberId))
                            .Select(x => new
                            {
                                x.MemberId,
                                x.RenewedAt,
                                x.StartDateBefore,
                                x.EndDateBefore,
                                x.StartDateAfter,
                                x.EndDateAfter,
                                x.Note
                            })
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

                        foreach (var r in existingRenews)
                        {
                            renewKeys.Add(new RenewKey(
                                MemberId: r.MemberId,
                                RenewedAt: r.RenewedAt.Date,
                                StartDateBefore: r.StartDateBefore.Date,
                                EndDateBefore: r.EndDateBefore.Date,
                                StartDateAfter: r.StartDateAfter.Date,
                                EndDateAfter: r.EndDateAfter.Date,
                                Note: NormalizeNote(r.Note)));
                        }
                    }

                    foreach (var row in resolvedRenews)
                    {
                        if (!memberById.TryGetValue(row.MemberId, out var member))
                        {
                            errors.Add($"[续费记录] 第 {row.RowNumber} 行：找不到会员Id {row.MemberId}。");
                            renewSkipped++;
                            continue;
                        }

                        var note = NormalizeNote(row.Note);
                        var key = new RenewKey(
                            MemberId: row.MemberId,
                            RenewedAt: row.RenewedAt.Date,
                            StartDateBefore: row.StartDateBefore.Date,
                            EndDateBefore: row.EndDateBefore.Date,
                            StartDateAfter: row.StartDateAfter.Date,
                            EndDateAfter: row.EndDateAfter.Date,
                            Note: note);

                        if (!renewKeys.Add(key))
                        {
                            renewSkipped++;
                            continue;
                        }

                        var record = new AnnualCardRenewRecord
                        {
                            MemberId = row.MemberId,
                            MemberName = string.IsNullOrWhiteSpace(row.MemberName) ? member.Name : row.MemberName.Trim(),
                            MemberPhone = string.IsNullOrWhiteSpace(row.MemberPhone) ? member.Phone : row.MemberPhone.Trim(),
                            RenewedAt = row.RenewedAt,
                            StartDateBefore = row.StartDateBefore.Date,
                            EndDateBefore = row.EndDateBefore.Date,
                            StartDateAfter = row.StartDateAfter.Date,
                            EndDateAfter = row.EndDateAfter.Date,
                            Note = note
                        };

                        if (row.CreatedAt is not null && row.CreatedAt.Value != default)
                        {
                            record.CreatedAt = row.CreatedAt.Value;
                        }

                        db.AnnualCardRenewRecords.Add(record);

                        // 导入历史续费记录时，尽量保持会员当前的开通/截止日期不倒退（取最大 EndDateAfter）。
                        if (row.EndDateAfter.Date > member.EndDate.Date)
                        {
                            member.StartDate = row.StartDateAfter.Date;
                            member.EndDate = row.EndDateAfter.Date;
                        }

                        renewAdded++;
                    }

                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            errors.Add($"保存失败：{ex.Message}");
            try
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }

        return new ExcelImportResult(added, updated, skipped, pauseAdded, pauseSkipped, renewAdded, renewSkipped, errors);
    }

    public async Task ExportPrivateTrainingMembersAsync(string filePath, CancellationToken cancellationToken = default)
    {
        filePath = (filePath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("导出路径不能为空。", nameof(filePath));
        }

        await using var db = _dbProvider.CreateDbContext();

        var members = await db.PrivateTrainingMembers
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var fees = await db.PrivateTrainingFeeRecords
            .AsNoTracking()
            .OrderByDescending(x => x.PaidAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sessions = await db.PrivateTrainingSessionRecords
            .AsNoTracking()
            .OrderByDescending(x => x.UsedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        EnsureDirectoryExists(filePath);

        var memberById = members.ToDictionary(x => x.Id);

        await Task.Run(() =>
        {
            using var wb = new XLWorkbook();

            var wsMembers = wb.Worksheets.Add("私教会员");
            WriteHeader(wsMembers, new[]
            {
                "Id",
                "姓名",
                "性别",
                "电话",
                "已交费用",
                "总课程",
                "已用",
                "更新时间"
            });

            var r = 2;
            foreach (var m in members)
            {
                wsMembers.Cell(r, 1).Value = m.Id;
                wsMembers.Cell(r, 2).Value = m.Name;
                wsMembers.Cell(r, 3).Value = GenderToText(m.Gender);
                wsMembers.Cell(r, 4).Value = m.Phone;
                wsMembers.Cell(r, 5).Value = m.PaidAmount;
                wsMembers.Cell(r, 6).Value = m.TotalSessions;
                wsMembers.Cell(r, 7).Value = m.UsedSessions;

                wsMembers.Cell(r, 8).Value = m.UpdatedAt;
                wsMembers.Cell(r, 8).Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss";

                r++;
            }

            wsMembers.Columns().AdjustToContents();

            var wsFees = wb.Worksheets.Add("缴费记录");
            WriteHeader(wsFees, new[]
            {
                "会员Id",
                "会员姓名",
                "会员电话",
                "金额",
                "缴费日期",
                "备注",
                "创建时间"
            });

            r = 2;
            foreach (var fee in fees)
            {
                memberById.TryGetValue(fee.MemberId, out var m);
                wsFees.Cell(r, 1).Value = fee.MemberId;
                wsFees.Cell(r, 2).Value = m?.Name ?? string.Empty;
                wsFees.Cell(r, 3).Value = m?.Phone ?? string.Empty;
                wsFees.Cell(r, 4).Value = fee.Amount;

                wsFees.Cell(r, 5).Value = fee.PaidAt.Date;
                wsFees.Cell(r, 5).Style.DateFormat.Format = DateFormat;

                wsFees.Cell(r, 6).Value = fee.Note ?? string.Empty;
                wsFees.Cell(r, 7).Value = fee.CreatedAt;
                wsFees.Cell(r, 7).Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss";
                r++;
            }

            wsFees.Columns().AdjustToContents();

            var wsSessions = wb.Worksheets.Add("消课记录");
            WriteHeader(wsSessions, new[]
            {
                "会员Id",
                "会员姓名",
                "会员电话",
                "消耗课次",
                "消课日期",
                "备注",
                "创建时间"
            });

            r = 2;
            foreach (var session in sessions)
            {
                memberById.TryGetValue(session.MemberId, out var m);
                wsSessions.Cell(r, 1).Value = session.MemberId;
                wsSessions.Cell(r, 2).Value = m?.Name ?? string.Empty;
                wsSessions.Cell(r, 3).Value = m?.Phone ?? string.Empty;
                wsSessions.Cell(r, 4).Value = session.SessionsUsed;

                wsSessions.Cell(r, 5).Value = session.UsedAt.Date;
                wsSessions.Cell(r, 5).Style.DateFormat.Format = DateFormat;

                wsSessions.Cell(r, 6).Value = session.Note ?? string.Empty;
                wsSessions.Cell(r, 7).Value = session.CreatedAt;
                wsSessions.Cell(r, 7).Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss";
                r++;
            }

            wsSessions.Columns().AdjustToContents();

            wb.SaveAs(filePath);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PrivateTrainingExcelImportResult> ImportPrivateTrainingMembersAsync(
        string filePath,
        bool overwriteExisting = false,
        CancellationToken cancellationToken = default)
    {
        filePath = (filePath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("导入路径不能为空。", nameof(filePath));
        }

        var errors = new List<string>();

        var rows = await Task.Run(() => ReadPrivateTrainingMemberImportRows(filePath, errors), cancellationToken)
            .ConfigureAwait(false);

        var feeRows = await Task.Run(() => ReadPrivateTrainingFeeImportRows(filePath, errors), cancellationToken)
            .ConfigureAwait(false);

        var sessionRows = await Task.Run(() => ReadPrivateTrainingSessionImportRows(filePath, errors), cancellationToken)
            .ConfigureAwait(false);

        if (overwriteExisting && rows.Count == 0)
        {
            throw new InvalidOperationException("覆盖导入需要包含“私教会员”工作表数据。");
        }

        if (rows.Count == 0 && feeRows.Count == 0 && sessionRows.Count == 0)
        {
            return new PrivateTrainingExcelImportResult(0, 0, 0, 0, 0, 0, 0, errors);
        }

        var membersAdded = 0;
        var membersUpdated = 0;
        var membersSkipped = 0;

        var feeAdded = 0;
        var feeSkipped = 0;
        var sessionAdded = 0;
        var sessionSkipped = 0;

        await using var db = _dbProvider.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var batchSize = GetInClauseBatchSize(db);

        if (overwriteExisting)
        {
            await db.PrivateTrainingFeeRecords.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await db.PrivateTrainingSessionRecords.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await db.PrivateTrainingMembers.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        }

        var importIds = rows.Where(x => x.Id > 0).Select(x => x.Id).Distinct().ToList();
        var importPhones = rows.Where(x => !string.IsNullOrWhiteSpace(x.Phone)).Select(x => x.Phone).Distinct().ToList();

        var existingById = new Dictionary<int, PrivateTrainingMember>();
        if (importIds.Count > 0)
        {
            foreach (var batch in importIds.Chunk(batchSize))
            {
                var chunk = batch;
                var list = await db.PrivateTrainingMembers
                    .Where(x => chunk.Contains(x.Id))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                foreach (var item in list)
                {
                    existingById[item.Id] = item;
                }
            }
        }

        var existingPhoneList = new List<PrivateTrainingMember>();
        if (importPhones.Count > 0)
        {
            foreach (var batch in importPhones.Chunk(batchSize))
            {
                var chunk = batch;
                var list = await db.PrivateTrainingMembers
                    .Where(x => chunk.Contains(x.Phone))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                existingPhoneList.AddRange(list);
            }
        }

        var phoneGroups = existingPhoneList
            .GroupBy(x => x.Phone)
            .ToDictionary(x => x.Key, x => x.ToList());

        foreach (var row in rows)
        {
            var validationError = ValidatePrivateTrainingMemberRow(row);
            if (validationError is not null)
            {
                errors.Add($"第 {row.RowNumber} 行：{validationError}");
                membersSkipped++;
                continue;
            }

            var entity = default(PrivateTrainingMember);

            if (row.Id > 0 && existingById.TryGetValue(row.Id, out var byId))
            {
                entity = byId;
                membersUpdated++;
            }
            else if (!string.IsNullOrWhiteSpace(row.Phone) && phoneGroups.TryGetValue(row.Phone, out var candidates))
            {
                if (candidates.Count == 1)
                {
                    entity = candidates[0];
                    membersUpdated++;
                }
                else
                {
                    errors.Add($"第 {row.RowNumber} 行：电话 {row.Phone} 在数据库中存在 {candidates.Count} 条记录，无法判断更新对象，请填写 Id。");
                    membersSkipped++;
                    continue;
                }
            }
            else
            {
                entity = new PrivateTrainingMember
                {
                    PaidAmount = 0,
                    UsedSessions = 0
                };
                db.PrivateTrainingMembers.Add(entity);
                membersAdded++;
            }

            if (row.TotalSessions < entity.UsedSessions)
            {
                errors.Add($"第 {row.RowNumber} 行：总课程 {row.TotalSessions} 不能小于数据库已有已用课程 {entity.UsedSessions}（电话 {row.Phone}）。");
                membersSkipped++;
                continue;
            }

            entity.Name = row.Name;
            entity.Gender = row.Gender;
            entity.Phone = row.Phone;
            entity.TotalSessions = row.TotalSessions;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (feeRows.Count > 0 || sessionRows.Count > 0)
        {
            var recordMemberIds = feeRows
                .Select(x => x.MemberId)
                .Concat(sessionRows.Select(x => x.MemberId))
                .Where(x => x is > 0)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var recordPhones = feeRows
                .Select(x => x.MemberPhone)
                .Concat(sessionRows.Select(x => x.MemberPhone))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct()
                .ToList();

            HashSet<int> existingMemberIds;
            if (recordMemberIds.Count == 0)
            {
                existingMemberIds = new HashSet<int>();
            }
            else
            {
                var existingMemberIdList = new List<int>();
                foreach (var batch in recordMemberIds.Chunk(batchSize))
                {
                    var chunk = batch;
                    var list = await db.PrivateTrainingMembers
                        .AsNoTracking()
                        .Where(x => chunk.Contains(x.Id))
                        .Select(x => x.Id)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    existingMemberIdList.AddRange(list);
                }

                existingMemberIds = existingMemberIdList.ToHashSet();
            }

            Dictionary<string, List<int>> recordPhoneGroups;
            if (recordPhones.Count == 0)
            {
                recordPhoneGroups = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                var phoneMembers = new List<(int Id, string Phone)>();
                foreach (var batch in recordPhones.Chunk(batchSize))
                {
                    var chunk = batch;
                    var list = await db.PrivateTrainingMembers
                        .AsNoTracking()
                        .Where(x => chunk.Contains(x.Phone))
                        .Select(x => new { x.Id, x.Phone })
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    phoneMembers.AddRange(list.Select(x => (x.Id, x.Phone)));
                }

                recordPhoneGroups = phoneMembers
                    .GroupBy(x => x.Phone)
                    .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToList());
            }

            int? ResolveMemberId(int? memberId, string memberPhone, int rowNumber, string section, out string? errorMessage)
            {
                errorMessage = null;

                if (memberId is > 0 && existingMemberIds.Contains(memberId.Value))
                {
                    return memberId.Value;
                }

                var phone = (memberPhone ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(phone))
                {
                    errorMessage = $"{section} 第 {rowNumber} 行：缺少会员电话/会员Id，无法定位会员。";
                    return null;
                }

                if (!recordPhoneGroups.TryGetValue(phone, out var candidates) || candidates.Count == 0)
                {
                    errorMessage = $"{section} 第 {rowNumber} 行：找不到电话为 {phone} 的会员。";
                    return null;
                }

                if (candidates.Count != 1)
                {
                    errorMessage = $"{section} 第 {rowNumber} 行：电话 {phone} 在数据库中存在 {candidates.Count} 条记录，无法定位会员，请使用会员Id。";
                    return null;
                }

                return candidates[0];
            }

            var resolvedFees = new List<(int RowNumber, int MemberId, decimal Amount, DateTime PaidAt, string? Note)>();
            foreach (var row in feeRows)
            {
                var memberId = ResolveMemberId(row.MemberId, row.MemberPhone, row.RowNumber, "[缴费记录]", out var resolveError);
                if (memberId is null)
                {
                    errors.Add(resolveError!);
                    feeSkipped++;
                    continue;
                }

                resolvedFees.Add((row.RowNumber, memberId.Value, row.Amount, row.PaidAt, row.Note));
            }

            var resolvedSessions = new List<(int RowNumber, int MemberId, int SessionsUsed, DateTime UsedAt, string? Note)>();
            foreach (var row in sessionRows)
            {
                var memberId = ResolveMemberId(row.MemberId, row.MemberPhone, row.RowNumber, "[消课记录]", out var resolveError);
                if (memberId is null)
                {
                    errors.Add(resolveError!);
                    sessionSkipped++;
                    continue;
                }

                resolvedSessions.Add((row.RowNumber, memberId.Value, row.SessionsUsed, row.UsedAt, row.Note));
            }

            var involvedMemberIds = resolvedFees
                .Select(x => x.MemberId)
                .Concat(resolvedSessions.Select(x => x.MemberId))
                .Distinct()
                .ToList();

            if (involvedMemberIds.Count > 0)
            {
                var involvedMembers = new List<PrivateTrainingMember>();
                foreach (var batch in involvedMemberIds.Chunk(batchSize))
                {
                    var chunk = batch;
                    var list = await db.PrivateTrainingMembers
                        .Where(x => chunk.Contains(x.Id))
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    involvedMembers.AddRange(list);
                }

                var memberById = involvedMembers.ToDictionary(x => x.Id);

                var feeKeys = new HashSet<FeeKey>();
                var sessionKeys = new HashSet<SessionKey>();

                if (!overwriteExisting)
                {
                    foreach (var batch in involvedMemberIds.Chunk(batchSize))
                    {
                        var chunk = batch;
                        var existingFees = await db.PrivateTrainingFeeRecords
                            .AsNoTracking()
                            .Where(x => chunk.Contains(x.MemberId))
                            .Select(x => new { x.MemberId, x.Amount, x.PaidAt, x.Note })
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

                        foreach (var fee in existingFees)
                        {
                            feeKeys.Add(new FeeKey(
                                MemberId: fee.MemberId,
                                Amount: fee.Amount,
                                PaidAt: fee.PaidAt.Date,
                                Note: NormalizeNote(fee.Note)));
                        }
                    }

                    foreach (var batch in involvedMemberIds.Chunk(batchSize))
                    {
                        var chunk = batch;
                        var existingSessions = await db.PrivateTrainingSessionRecords
                            .AsNoTracking()
                            .Where(x => chunk.Contains(x.MemberId))
                            .Select(x => new { x.MemberId, x.SessionsUsed, x.UsedAt, x.Note })
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

                        foreach (var session in existingSessions)
                        {
                            sessionKeys.Add(new SessionKey(
                                MemberId: session.MemberId,
                                SessionsUsed: session.SessionsUsed,
                                UsedAt: session.UsedAt.Date,
                                Note: NormalizeNote(session.Note)));
                        }
                    }
                }

                foreach (var row in resolvedFees)
                {
                    if (row.Amount <= 0)
                    {
                        errors.Add($"[缴费记录] 第 {row.RowNumber} 行：金额必须大于 0。");
                        feeSkipped++;
                        continue;
                    }

                    if (row.PaidAt == default)
                    {
                        errors.Add($"[缴费记录] 第 {row.RowNumber} 行：缴费日期不能为空。");
                        feeSkipped++;
                        continue;
                    }

                    if (!memberById.TryGetValue(row.MemberId, out var member))
                    {
                        errors.Add($"[缴费记录] 第 {row.RowNumber} 行：找不到会员Id {row.MemberId}。");
                        feeSkipped++;
                        continue;
                    }

                    var note = NormalizeNote(row.Note);
                    var key = new FeeKey(
                        MemberId: row.MemberId,
                        Amount: row.Amount,
                        PaidAt: row.PaidAt.Date,
                        Note: note);
                    if (!feeKeys.Add(key))
                    {
                        feeSkipped++;
                        continue;
                    }

                    db.PrivateTrainingFeeRecords.Add(new PrivateTrainingFeeRecord
                    {
                        MemberId = row.MemberId,
                        Amount = row.Amount,
                        PaidAt = row.PaidAt.Date,
                        Note = note
                    });

                    member.PaidAmount += row.Amount;
                    feeAdded++;
                }

                foreach (var row in resolvedSessions)
                {
                    if (row.SessionsUsed < 1)
                    {
                        errors.Add($"[消课记录] 第 {row.RowNumber} 行：消耗课次必须 >= 1。");
                        sessionSkipped++;
                        continue;
                    }

                    if (row.UsedAt == default)
                    {
                        errors.Add($"[消课记录] 第 {row.RowNumber} 行：消课日期不能为空。");
                        sessionSkipped++;
                        continue;
                    }

                    if (!memberById.TryGetValue(row.MemberId, out var member))
                    {
                        errors.Add($"[消课记录] 第 {row.RowNumber} 行：找不到会员Id {row.MemberId}。");
                        sessionSkipped++;
                        continue;
                    }

                    var note = NormalizeNote(row.Note);
                    var key = new SessionKey(
                        MemberId: row.MemberId,
                        SessionsUsed: row.SessionsUsed,
                        UsedAt: row.UsedAt.Date,
                        Note: note);
                    if (!sessionKeys.Add(key))
                    {
                        sessionSkipped++;
                        continue;
                    }

                    var before = member.UsedSessions;
                    if (before + row.SessionsUsed > member.TotalSessions)
                    {
                        errors.Add($"[消课记录] 第 {row.RowNumber} 行：导入后将导致会员 {member.Phone} 已用课程超过总课程（总 {member.TotalSessions}，现有已用 {before}，本次新增 {row.SessionsUsed}）。");
                        sessionSkipped++;
                        continue;
                    }

                    db.PrivateTrainingSessionRecords.Add(new PrivateTrainingSessionRecord
                    {
                        MemberId = row.MemberId,
                        SessionsUsed = row.SessionsUsed,
                        UsedAt = row.UsedAt.Date,
                        Note = note
                    });

                    member.UsedSessions = before + row.SessionsUsed;
                    sessionAdded++;
                }

                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new PrivateTrainingExcelImportResult(
            MembersAdded: membersAdded,
            MembersUpdated: membersUpdated,
            MembersSkipped: membersSkipped,
            FeeRecordsAdded: feeAdded,
            FeeRecordsSkipped: feeSkipped,
            SessionRecordsAdded: sessionAdded,
            SessionRecordsSkipped: sessionSkipped,
            Errors: errors);
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void WriteHeader(IXLWorksheet ws, IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
        }

        ws.Row(1).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1);
    }

    private static string GenderToText(Gender gender) => gender switch
    {
        Gender.Male => "男",
        Gender.Female => "女",
        _ => "未知"
    };

    private static Gender ParseGender(string? input)
    {
        input = (input ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return Gender.Unknown;
        }

        var normalized = input.ToLowerInvariant();
        return normalized switch
        {
            "男" or "male" or "m" or "1" => Gender.Male,
            "女" or "female" or "f" or "2" => Gender.Female,
            "未知" or "unknown" or "0" => Gender.Unknown,
            _ => Gender.Unknown
        };
    }

    private static string? NormalizeNote(string? note)
    {
        note = (note ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(note) ? null : note;
    }

    private static string? ValidateAnnualCardRow(AnnualCardImportRow row)
    {
        if (string.IsNullOrWhiteSpace(row.Name))
        {
            return "姓名不能为空。";
        }

        if (string.IsNullOrWhiteSpace(row.Phone))
        {
            return "电话不能为空。";
        }

        if (row.StartDate == default)
        {
            return "开通日期不能为空。";
        }

        if (row.EndDate == default)
        {
            return "截止日期不能为空。";
        }

        if (row.EndDate.Date < row.StartDate.Date)
        {
            return "截止日期不能早于开通日期。";
        }

        return null;
    }

    private static string? ValidatePrivateTrainingMemberRow(PrivateTrainingMemberImportRow row)
    {
        if (string.IsNullOrWhiteSpace(row.Name))
        {
            return "姓名不能为空。";
        }

        if (string.IsNullOrWhiteSpace(row.Phone))
        {
            return "电话不能为空。";
        }

        if (row.TotalSessions < 0)
        {
            return "总课程不能为负数。";
        }

        return null;
    }

    private static List<AnnualCardImportRow> ReadAnnualCardImportRows(string filePath, List<string> errors)
    {
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheets.FirstOrDefault(x => x.Name.Equals("年卡会员", StringComparison.OrdinalIgnoreCase))
                 ?? wb.Worksheets.FirstOrDefault();

        if (ws is null)
        {
            errors.Add("Excel 文件中未找到工作表。");
            return new List<AnnualCardImportRow>();
        }

        var headerMap = ReadHeaderMap(ws);
        var colId = FindColumn(headerMap, "Id", "ID", "编号");
        var colName = FindColumn(headerMap, "姓名", "Name");
        var colGender = FindColumn(headerMap, "性别", "Gender");
        var colPhone = FindColumn(headerMap, "电话", "手机号", "Phone");
        var colStart = FindColumn(headerMap, "开通日期", "开通", "StartDate", "开始日期");
        var colEnd = FindColumn(headerMap, "截止日期", "截止", "EndDate", "到期日期");

        if (colName is null || colPhone is null || colStart is null || colEnd is null)
        {
            errors.Add("年卡会员工作表缺少必要列：姓名/电话/开通日期/截止日期。请使用系统导出的模板。");
            return new List<AnnualCardImportRow>();
        }

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        var list = new List<AnnualCardImportRow>();

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = ws.Row(rowNumber);

            var name = GetCellString(row, colName);
            var phone = GetCellString(row, colPhone);
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(phone))
            {
                continue;
            }

            var id = GetCellInt(row, colId) ?? 0;
            var genderText = GetCellString(row, colGender);
            var startDate = GetCellDateTime(row, colStart) ?? default;
            var endDate = GetCellDateTime(row, colEnd) ?? default;

            list.Add(new AnnualCardImportRow(
                RowNumber: rowNumber,
                Id: id,
                Name: (name ?? string.Empty).Trim(),
                Gender: ParseGender(genderText),
                Phone: (phone ?? string.Empty).Trim(),
                StartDate: startDate,
                EndDate: endDate));
        }

        return list;
    }

    private static List<AnnualCardPauseImportRow> ReadAnnualCardPauseImportRows(string filePath, List<string> errors)
    {
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheets.FirstOrDefault(x => x.Name.Equals("停卡记录", StringComparison.OrdinalIgnoreCase));
        if (ws is null)
        {
            return new List<AnnualCardPauseImportRow>();
        }

        var headerMap = ReadHeaderMap(ws);
        var colMemberId = FindColumn(headerMap, "会员Id", "MemberId");
        var colMemberName = FindColumn(headerMap, "会员姓名", "姓名", "MemberName");
        var colMemberPhone = FindColumn(headerMap, "会员电话", "电话", "手机号", "MemberPhone", "Phone");

        var colPauseStart = FindColumn(headerMap, "停卡开始日期", "停卡开始", "PauseStartDate", "StartDate");
        var colResume = FindColumn(headerMap, "恢复日期", "恢复", "ResumeDate");
        var colPauseDays = FindColumn(headerMap, "停卡天数", "天数", "PauseDays");
        var colEndBefore = FindColumn(headerMap, "停卡前截止日期", "停卡前截止", "EndDateBefore");
        var colEndAfter = FindColumn(headerMap, "停卡后截止日期", "停卡后截止", "EndDateAfter");
        var colNote = FindColumn(headerMap, "备注", "Note");
        var colCreatedAt = FindColumn(headerMap, "创建时间", "CreatedAt");

        if (colPauseStart is null || colPauseDays is null)
        {
            errors.Add("停卡记录工作表缺少必要列：停卡开始日期/停卡天数。请使用系统导出的模板。");
            return new List<AnnualCardPauseImportRow>();
        }

        if (colMemberId is null && colMemberPhone is null)
        {
            errors.Add("停卡记录工作表缺少必要列：会员Id/会员电话。请使用系统导出的模板。");
            return new List<AnnualCardPauseImportRow>();
        }

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        var list = new List<AnnualCardPauseImportRow>();

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = ws.Row(rowNumber);

            var memberId = GetCellInt(row, colMemberId);
            var memberPhone = GetCellString(row, colMemberPhone);

            var pauseStart = GetCellDateTime(row, colPauseStart) ?? default;
            var pauseDays = GetCellInt(row, colPauseDays) ?? 0;

            // 允许工作表中存在空行
            if ((memberId is null || memberId <= 0)
                && string.IsNullOrWhiteSpace(memberPhone)
                && pauseStart == default
                && pauseDays == 0)
            {
                continue;
            }

            var name = GetCellString(row, colMemberName);
            var resume = GetCellDateTime(row, colResume) ?? default;
            var endBefore = GetCellDateTime(row, colEndBefore) ?? default;
            var endAfter = GetCellDateTime(row, colEndAfter) ?? default;
            var note = GetCellString(row, colNote);
            var createdAt = GetCellDateTime(row, colCreatedAt);

            list.Add(new AnnualCardPauseImportRow(
                RowNumber: rowNumber,
                MemberId: memberId,
                MemberName: (name ?? string.Empty).Trim(),
                MemberPhone: (memberPhone ?? string.Empty).Trim(),
                PauseStartDate: pauseStart,
                ResumeDate: resume,
                PauseDays: pauseDays,
                EndDateBefore: endBefore,
                EndDateAfter: endAfter,
                Note: string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                CreatedAt: createdAt));
        }

        return list;
    }

    private static List<AnnualCardRenewImportRow> ReadAnnualCardRenewImportRows(string filePath, List<string> errors)
    {
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheets.FirstOrDefault(x => x.Name.Equals("续费记录", StringComparison.OrdinalIgnoreCase));
        if (ws is null)
        {
            return new List<AnnualCardRenewImportRow>();
        }

        var headerMap = ReadHeaderMap(ws);
        var colMemberId = FindColumn(headerMap, "会员Id", "MemberId");
        var colMemberName = FindColumn(headerMap, "会员姓名", "姓名", "MemberName");
        var colMemberPhone = FindColumn(headerMap, "会员电话", "电话", "手机号", "MemberPhone", "Phone");

        var colRenewedAt = FindColumn(headerMap, "续费时间", "续费日期", "RenewedAt", "RenewAt");
        var colStartBefore = FindColumn(headerMap, "续费前开通日期", "StartDateBefore");
        var colEndBefore = FindColumn(headerMap, "续费前截止日期", "EndDateBefore");
        var colStartAfter = FindColumn(headerMap, "续费后开通日期", "StartDateAfter");
        var colEndAfter = FindColumn(headerMap, "续费后截止日期", "EndDateAfter");
        var colNote = FindColumn(headerMap, "备注", "Note");
        var colCreatedAt = FindColumn(headerMap, "创建时间", "CreatedAt");

        if (colRenewedAt is null || colStartBefore is null || colEndBefore is null || colStartAfter is null || colEndAfter is null)
        {
            errors.Add("续费记录工作表缺少必要列：续费时间、续费前/后开通日期、续费前/后截止日期。请使用系统导出的模板。");
            return new List<AnnualCardRenewImportRow>();
        }

        if (colMemberId is null && colMemberPhone is null)
        {
            errors.Add("续费记录工作表缺少必要列：会员Id/会员电话。请使用系统导出的模板。");
            return new List<AnnualCardRenewImportRow>();
        }

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        var list = new List<AnnualCardRenewImportRow>();

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = ws.Row(rowNumber);

            var memberId = GetCellInt(row, colMemberId);
            var memberPhone = GetCellString(row, colMemberPhone);

            var renewedAt = GetCellDateTime(row, colRenewedAt) ?? default;
            var startBefore = GetCellDateTime(row, colStartBefore) ?? default;
            var endBefore = GetCellDateTime(row, colEndBefore) ?? default;
            var startAfter = GetCellDateTime(row, colStartAfter) ?? default;
            var endAfter = GetCellDateTime(row, colEndAfter) ?? default;

            // 允许工作表中存在空行
            if ((memberId is null || memberId <= 0)
                && string.IsNullOrWhiteSpace(memberPhone)
                && renewedAt == default
                && startBefore == default
                && endBefore == default
                && startAfter == default
                && endAfter == default)
            {
                continue;
            }

            var name = GetCellString(row, colMemberName);
            var note = GetCellString(row, colNote);
            var createdAt = GetCellDateTime(row, colCreatedAt);

            list.Add(new AnnualCardRenewImportRow(
                RowNumber: rowNumber,
                MemberId: memberId,
                MemberName: (name ?? string.Empty).Trim(),
                MemberPhone: (memberPhone ?? string.Empty).Trim(),
                RenewedAt: renewedAt,
                StartDateBefore: startBefore,
                EndDateBefore: endBefore,
                StartDateAfter: startAfter,
                EndDateAfter: endAfter,
                Note: string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                CreatedAt: createdAt));
        }

        return list;
    }

    private static List<PrivateTrainingMemberImportRow> ReadPrivateTrainingMemberImportRows(string filePath, List<string> errors)
    {
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheets.FirstOrDefault(x => x.Name.Equals("私教会员", StringComparison.OrdinalIgnoreCase))
                 ?? wb.Worksheets.FirstOrDefault();

        if (ws is null)
        {
            errors.Add("Excel 文件中未找到工作表。");
            return new List<PrivateTrainingMemberImportRow>();
        }

        var headerMap = ReadHeaderMap(ws);
        var colId = FindColumn(headerMap, "Id", "ID", "编号");
        var colName = FindColumn(headerMap, "姓名", "Name");
        var colGender = FindColumn(headerMap, "性别", "Gender");
        var colPhone = FindColumn(headerMap, "电话", "手机号", "Phone");
        var colTotalSessions = FindColumn(headerMap, "总课程", "TotalSessions", "课程总数");

        if (colName is null || colPhone is null || colTotalSessions is null)
        {
            errors.Add("私教会员工作表缺少必要列：姓名/电话/总课程。请使用系统导出的模板。");
            return new List<PrivateTrainingMemberImportRow>();
        }

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        var list = new List<PrivateTrainingMemberImportRow>();

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = ws.Row(rowNumber);

            var name = GetCellString(row, colName);
            var phone = GetCellString(row, colPhone);
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(phone))
            {
                continue;
            }

            var id = GetCellInt(row, colId) ?? 0;
            var genderText = GetCellString(row, colGender);
            var totalSessions = GetCellInt(row, colTotalSessions) ?? -1;

            list.Add(new PrivateTrainingMemberImportRow(
                RowNumber: rowNumber,
                Id: id,
                Name: (name ?? string.Empty).Trim(),
                Gender: ParseGender(genderText),
                Phone: (phone ?? string.Empty).Trim(),
                TotalSessions: totalSessions));
        }

        return list;
    }

    private static List<PrivateTrainingFeeImportRow> ReadPrivateTrainingFeeImportRows(string filePath, List<string> errors)
    {
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheets.FirstOrDefault(x => x.Name.Equals("缴费记录", StringComparison.OrdinalIgnoreCase));

        if (ws is null)
        {
            return new List<PrivateTrainingFeeImportRow>();
        }

        var headerMap = ReadHeaderMap(ws);
        var colMemberId = FindColumn(headerMap, "会员Id", "MemberId");
        var colPhone = FindColumn(headerMap, "会员电话", "电话", "Phone");
        var colAmount = FindColumn(headerMap, "金额", "Amount");
        var colPaidAt = FindColumn(headerMap, "缴费日期", "缴费时间", "时间", "PaidAt");
        var colNote = FindColumn(headerMap, "备注", "Note");

        if ((colMemberId is null && colPhone is null) || colAmount is null || colPaidAt is null)
        {
            errors.Add("缴费记录工作表缺少必要列：会员Id/会员电话、金额、缴费日期。请使用系统导出的模板。");
            return new List<PrivateTrainingFeeImportRow>();
        }

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        var list = new List<PrivateTrainingFeeImportRow>();

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = ws.Row(rowNumber);

            var memberId = GetCellInt(row, colMemberId);
            var phone = GetCellString(row, colPhone);
            var amount = GetCellDecimal(row, colAmount) ?? 0m;
            var paidAt = GetCellDateTime(row, colPaidAt) ?? default;
            var note = GetCellString(row, colNote);

            var hasAny =
                (memberId is > 0) ||
                !string.IsNullOrWhiteSpace(phone) ||
                amount != 0m ||
                paidAt != default ||
                !string.IsNullOrWhiteSpace(note);
            if (!hasAny)
            {
                continue;
            }

            list.Add(new PrivateTrainingFeeImportRow(
                RowNumber: rowNumber,
                MemberId: memberId,
                MemberPhone: (phone ?? string.Empty).Trim(),
                Amount: amount,
                PaidAt: paidAt,
                Note: note));
        }

        return list;
    }

    private static List<PrivateTrainingSessionImportRow> ReadPrivateTrainingSessionImportRows(string filePath, List<string> errors)
    {
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheets.FirstOrDefault(x => x.Name.Equals("消课记录", StringComparison.OrdinalIgnoreCase));

        if (ws is null)
        {
            return new List<PrivateTrainingSessionImportRow>();
        }

        var headerMap = ReadHeaderMap(ws);
        var colMemberId = FindColumn(headerMap, "会员Id", "MemberId");
        var colPhone = FindColumn(headerMap, "会员电话", "电话", "Phone");
        var colSessionsUsed = FindColumn(headerMap, "消耗课次", "消耗", "SessionsUsed");
        var colUsedAt = FindColumn(headerMap, "消课日期", "消课时间", "时间", "UsedAt");
        var colNote = FindColumn(headerMap, "备注", "Note");

        if ((colMemberId is null && colPhone is null) || colSessionsUsed is null || colUsedAt is null)
        {
            errors.Add("消课记录工作表缺少必要列：会员Id/会员电话、消耗课次、消课日期。请使用系统导出的模板。");
            return new List<PrivateTrainingSessionImportRow>();
        }

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        var list = new List<PrivateTrainingSessionImportRow>();

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = ws.Row(rowNumber);

            var memberId = GetCellInt(row, colMemberId);
            var phone = GetCellString(row, colPhone);
            var sessionsUsed = GetCellInt(row, colSessionsUsed) ?? 0;
            var usedAt = GetCellDateTime(row, colUsedAt) ?? default;
            var note = GetCellString(row, colNote);

            var hasAny =
                (memberId is > 0) ||
                !string.IsNullOrWhiteSpace(phone) ||
                sessionsUsed != 0 ||
                usedAt != default ||
                !string.IsNullOrWhiteSpace(note);
            if (!hasAny)
            {
                continue;
            }

            list.Add(new PrivateTrainingSessionImportRow(
                RowNumber: rowNumber,
                MemberId: memberId,
                MemberPhone: (phone ?? string.Empty).Trim(),
                SessionsUsed: sessionsUsed,
                UsedAt: usedAt,
                Note: note));
        }

        return list;
    }

    private static Dictionary<string, int> ReadHeaderMap(IXLWorksheet ws)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var row = ws.Row(1);

        foreach (var cell in row.CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            map[header] = cell.Address.ColumnNumber;
        }

        return map;
    }

    private static int? FindColumn(IReadOnlyDictionary<string, int> headerMap, params string[] names)
    {
        foreach (var name in names)
        {
            if (headerMap.TryGetValue(name, out var col))
            {
                return col;
            }
        }

        return null;
    }

    private static string? GetCellString(IXLRow row, int? columnNumber)
    {
        if (columnNumber is null)
        {
            return null;
        }

        return row.Cell(columnNumber.Value).GetString().Trim();
    }

    private static int? GetCellInt(IXLRow row, int? columnNumber)
    {
        if (columnNumber is null)
        {
            return null;
        }

        var cell = row.Cell(columnNumber.Value);
        if (cell.TryGetValue<int>(out var i))
        {
            return i;
        }

        var s = cell.GetString().Trim();
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out i))
        {
            return i;
        }

        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
        {
            return (int)Math.Round(d, MidpointRounding.AwayFromZero);
        }

        return null;
    }

    private static decimal? GetCellDecimal(IXLRow row, int? columnNumber)
    {
        if (columnNumber is null)
        {
            return null;
        }

        var cell = row.Cell(columnNumber.Value);
        if (cell.TryGetValue<decimal>(out var v))
        {
            return v;
        }

        if (cell.TryGetValue<double>(out var d))
        {
            return (decimal)d;
        }

        var s = cell.GetString().Trim();
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v))
        {
            return v;
        }

        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out v))
        {
            return v;
        }

        return null;
    }

    private static DateTime? GetCellDateTime(IXLRow row, int? columnNumber)
    {
        if (columnNumber is null)
        {
            return null;
        }

        var cell = row.Cell(columnNumber.Value);
        if (cell.TryGetValue<DateTime>(out var dt))
        {
            return dt;
        }

        var s = cell.GetString().Trim();
        if (DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out dt))
        {
            return dt;
        }

        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dt))
        {
            return dt;
        }

        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
        {
            try
            {
                return DateTime.FromOADate(d);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private sealed record AnnualCardImportRow(
        int RowNumber,
        int Id,
        string Name,
        Gender Gender,
        string Phone,
        DateTime StartDate,
        DateTime EndDate);

    private sealed record AnnualCardPauseImportRow(
        int RowNumber,
        int? MemberId,
        string MemberName,
        string MemberPhone,
        DateTime PauseStartDate,
        DateTime ResumeDate,
        int PauseDays,
        DateTime EndDateBefore,
        DateTime EndDateAfter,
        string? Note,
        DateTime? CreatedAt);

    private sealed record AnnualCardRenewImportRow(
        int RowNumber,
        int? MemberId,
        string MemberName,
        string MemberPhone,
        DateTime RenewedAt,
        DateTime StartDateBefore,
        DateTime EndDateBefore,
        DateTime StartDateAfter,
        DateTime EndDateAfter,
        string? Note,
        DateTime? CreatedAt);

    private sealed record PrivateTrainingMemberImportRow(
        int RowNumber,
        int Id,
        string Name,
        Gender Gender,
        string Phone,
        int TotalSessions);

    private sealed record PrivateTrainingFeeImportRow(
        int RowNumber,
        int? MemberId,
        string MemberPhone,
        decimal Amount,
        DateTime PaidAt,
        string? Note);

    private sealed record PrivateTrainingSessionImportRow(
        int RowNumber,
        int? MemberId,
        string MemberPhone,
        int SessionsUsed,
        DateTime UsedAt,
        string? Note);

    private readonly record struct PauseKey(
        int MemberId,
        DateTime PauseStartDate,
        DateTime ResumeDate,
        int PauseDays,
        DateTime EndDateAfter,
        string? Note);

    private readonly record struct RenewKey(
        int MemberId,
        DateTime RenewedAt,
        DateTime StartDateBefore,
        DateTime EndDateBefore,
        DateTime StartDateAfter,
        DateTime EndDateAfter,
        string? Note);

    private readonly record struct FeeKey(
        int MemberId,
        decimal Amount,
        DateTime PaidAt,
        string? Note);

    private readonly record struct SessionKey(
        int MemberId,
        int SessionsUsed,
        DateTime UsedAt,
        string? Note);
}
