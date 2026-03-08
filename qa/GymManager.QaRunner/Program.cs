using System.Diagnostics;
using System.Text;
using ClosedXML.Excel;
using GymManager.App.Config;
using GymManager.App.Infrastructure;
using GymManager.App.Services;
using GymManager.Data.Db;
using GymManager.Domain.Entities;
using GymManager.Domain.Enums;
using GymManager.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

internal static class Program
{
    private sealed record QaRunOptions(
        string RepoRoot,
        string DistDir,
        string TestDbPath,
        bool ExportTemplates,
        string TemplateDir,
        int Seed,
        int CoachCount,
        int PrivateTrainingMemberCount,
        int AnnualCardMemberCount,
        int AnnualCardExpiringDays,
        int LowRemainingSessionsThreshold);

    private sealed record TestResult(string Id, string Name, bool Passed, string Details);

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var repoRoot = FindRepoRoot(Environment.CurrentDirectory)
                       ?? FindRepoRoot(AppContext.BaseDirectory)
                       ?? throw new InvalidOperationException("找不到仓库根目录（无法定位 GymManager.sln）。");

        var distDir = Path.Combine(repoRoot, "dist");
        var testDbPathDefault = Path.Combine(distDir, "Data", "gym_qa.db");

        var options = ParseArgs(args, repoRoot, distDir, testDbPathDefault);

        var reportDir = Path.Combine(options.RepoRoot, "qa", "reports");
        Directory.CreateDirectory(reportDir);

        if (options.ExportTemplates)
        {
            await ExportImportTemplatesAsync(options).ConfigureAwait(false);
            return 0;
        }

        var reportPath = Path.Combine(
            reportDir,
            $"QA_Report_{DateTime.Now:yyyy-MM-dd_HHmmss}.md");

        var results = new List<TestResult>();

        try
        {
            WriteSectionHeader($"GymManager QA Runner ({DateTime.Now:yyyy-MM-dd HH:mm:ss})");
            Console.WriteLine($"RepoRoot: {options.RepoRoot}");
            Console.WriteLine($"DistDir:  {options.DistDir}");
            Console.WriteLine($"DB:       {options.TestDbPath}");
            Console.WriteLine();

            EnsureDistAppSettings(options);

            BackupIfExists(options.TestDbPath);
            await EnsureDatabaseCreatedAsync(options.TestDbPath).ConfigureAwait(false);

            await SeedDataAsync(options).ConfigureAwait(false);

            await RunAutomatedChecksAsync(options, results).ConfigureAwait(false);

            await WriteReportAsync(options, results, reportPath).ConfigureAwait(false);

            Console.WriteLine();
            Console.WriteLine($"报告已生成：{reportPath}");
            Console.WriteLine();

            // Smoke: try to start the dist EXE and ensure it doesn't immediately crash.
            var smokeOk = await SmokeStartExeAsync(options, results).ConfigureAwait(false);
            await WriteReportAsync(options, results, reportPath).ConfigureAwait(false);

            Console.WriteLine(smokeOk
                ? "Smoke：EXE 启动检查完成（已自动关闭进程）。"
                : "Smoke：EXE 启动检查失败（详见报告与日志）。");

            Console.WriteLine();
            Console.WriteLine("下一步（人工验证 UI 建议）：");
            Console.WriteLine($"1) 运行：{Path.Combine(options.DistDir, "力量健身管理系统.exe")}");
            Console.WriteLine("2) 查看首页 Dashboard 计数、到期提醒横幅、各模块列表/搜索/筛选/弹窗编辑。");

            return results.All(r => r.Passed) ? 0 : 2;
        }
        catch (Exception ex)
        {
            results.Add(new TestResult("FATAL", "Runner 未处理异常", false, ex.ToString()));
            await WriteReportAsync(options, results, reportPath).ConfigureAwait(false);
            Console.Error.WriteLine(ex);
            Console.Error.WriteLine($"报告已生成：{reportPath}");
            return 1;
        }
    }

    private static QaRunOptions ParseArgs(string[] args, string repoRoot, string distDir, string testDbPathDefault)
    {
        var seed = 20260223;
        var coachCount = 100;
        var privateTrainingMemberCount = 500;
        var annualCardMemberCount = 400;
        var annualCardExpiringDays = 3;
        var lowRemainingSessionsThreshold = 3;

        var testDbPath = testDbPathDefault;
        var exportTemplates = false;
        var templateDir = distDir;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i].Trim();
            static string NextValue(string[] arr, ref int idx)
            {
                if (idx + 1 >= arr.Length)
                {
                    throw new ArgumentException($"缺少参数值：{arr[idx]}");
                }

                idx++;
                return arr[idx];
            }

            switch (arg)
            {
                case "--export-templates":
                    exportTemplates = true;
                    break;
                case "--template-dir":
                    templateDir = NextValue(args, ref i);
                    break;
                case "--db":
                    testDbPath = NextValue(args, ref i);
                    break;
                case "--seed":
                    seed = int.Parse(NextValue(args, ref i));
                    break;
                case "--coaches":
                    coachCount = int.Parse(NextValue(args, ref i));
                    break;
                case "--pt-members":
                    privateTrainingMemberCount = int.Parse(NextValue(args, ref i));
                    break;
                case "--annual-members":
                    annualCardMemberCount = int.Parse(NextValue(args, ref i));
                    break;
                case "--expiring-days":
                    annualCardExpiringDays = int.Parse(NextValue(args, ref i));
                    break;
                case "--low-remaining":
                    lowRemainingSessionsThreshold = int.Parse(NextValue(args, ref i));
                    break;
            }
        }

        return new QaRunOptions(
            RepoRoot: repoRoot,
            DistDir: distDir,
            TestDbPath: Path.GetFullPath(testDbPath),
            ExportTemplates: exportTemplates,
            TemplateDir: string.IsNullOrWhiteSpace(templateDir) ? distDir : Path.GetFullPath(templateDir),
            Seed: seed,
            CoachCount: Math.Max(0, coachCount),
            PrivateTrainingMemberCount: Math.Max(0, privateTrainingMemberCount),
            AnnualCardMemberCount: Math.Max(0, annualCardMemberCount),
            AnnualCardExpiringDays: Math.Max(0, annualCardExpiringDays),
            LowRemainingSessionsThreshold: Math.Max(0, lowRemainingSessionsThreshold));
    }

    private static string? FindRepoRoot(string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            return null;
        }

        var dir = new DirectoryInfo(startPath);
        while (dir.Exists)
        {
            var slnPath = Path.Combine(dir.FullName, "GymManager.sln");
            var srcPath = Path.Combine(dir.FullName, "src");
            if (File.Exists(slnPath) && Directory.Exists(srcPath))
            {
                return dir.FullName;
            }

            dir = dir.Parent ?? breakDir();
        }

        return null;

        static DirectoryInfo breakDir() => new("__invalid__");
    }

    private static async Task ExportImportTemplatesAsync(QaRunOptions options)
    {
        var outDir = options.TemplateDir;
        Directory.CreateDirectory(outDir);

        var tempDir = Path.Combine(outDir, "_GymManagerTemplateTemp");
        Directory.CreateDirectory(tempDir);

        var tempDbPath = Path.Combine(tempDir, "gym_template.db");

        // Ensure empty database exists (export uses DB query).
        await EnsureDatabaseCreatedAsync(tempDbPath).ConfigureAwait(false);

        var provider = MakeProvider(tempDbPath);
        var excel = new ExcelTransferService(provider);

        var annualPath = Path.Combine(outDir, "力量健身管理系统_年卡会员导入模板.xlsx");
        var ptPath = Path.Combine(outDir, "力量健身管理系统_私教会员导入模板.xlsx");

        await excel.ExportAnnualCardMembersAsync(annualPath).ConfigureAwait(false);
        await excel.ExportPrivateTrainingMembersAsync(ptPath).ConfigureAwait(false);

        TryDeleteFile(tempDbPath);
        TryDeleteFile($"{tempDbPath}-wal");
        TryDeleteFile($"{tempDbPath}-shm");
        TryDeleteDirectory(tempDir);

        Console.WriteLine("已生成导入模板：");
        Console.WriteLine($"- 年卡会员：{annualPath}");
        Console.WriteLine($"- 私教会员（含缴费/消课工作表）：{ptPath}");
        Console.WriteLine();
        Console.WriteLine("提示：建议先用系统“导出”生成的文件作为模板填写，再导入。");
    }

    private static void TryDeleteDirectory(string dirPath)
    {
        try
        {
            if (Directory.Exists(dirPath))
            {
                Directory.Delete(dirPath, recursive: true);
            }
        }
        catch
        {
            // ignore cleanup errors
        }
    }

    private static void EnsureDistAppSettings(QaRunOptions options)
    {
        var configPath = Path.Combine(options.DistDir, "appsettings.json");
        Directory.CreateDirectory(options.DistDir);
        Directory.CreateDirectory(Path.GetDirectoryName(options.TestDbPath)!);

        var settings = new AppSettings
        {
            Database = new DatabaseSettings
            {
                Provider = "SQLite",
                Sqlite = new SqliteSettings
                {
                    DbPath = options.TestDbPath
                }
            },
            Reminder = new ReminderSettings
            {
                AnnualCardExpiringDays = options.AnnualCardExpiringDays,
                LowRemainingSessionsThreshold = options.LowRemainingSessionsThreshold
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(
            settings,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(configPath, json, Encoding.UTF8);
    }

    private static void BackupIfExists(string filePath)
    {
        var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        static void MoveIfExists(string source, string suffix)
        {
            if (!File.Exists(source))
            {
                return;
            }

            File.Move(source, $"{source}{suffix}", overwrite: false);
        }

        // SQLite WAL/SHM might exist even if the main DB file was moved earlier.
        MoveIfExists(filePath, $".bak_{ts}");
        MoveIfExists($"{filePath}-wal", $".bak_{ts}");
        MoveIfExists($"{filePath}-shm", $".bak_{ts}");
    }

    private static async Task EnsureDatabaseCreatedAsync(string dbPath)
    {
        var settings = new AppSettings
        {
            Database = new DatabaseSettings
            {
                Provider = "SQLite",
                Sqlite = new SqliteSettings { DbPath = dbPath }
            }
        };

        var provider = new DbContextProvider(settings);
        await using var db = provider.CreateDbContext();
        await DbInitializer.EnsureCreatedAsync(db).ConfigureAwait(false);
    }

    private static async Task SeedDataAsync(QaRunOptions options)
    {
        var sw = Stopwatch.StartNew();

        var settings = new AppSettings
        {
            Database = new DatabaseSettings
            {
                Provider = "SQLite",
                Sqlite = new SqliteSettings { DbPath = options.TestDbPath }
            }
        };

        var provider = new DbContextProvider(settings);
        await using var db = provider.CreateDbContext();

        // Ensure PRAGMA (foreign_keys/WAL) on this connection too.
        await DbInitializer.EnsureCreatedAsync(db).ConfigureAwait(false);

        db.ChangeTracker.AutoDetectChangesEnabled = false;

        // Clean tables (test DB only).
        await db.Database.ExecuteSqlRawAsync("DELETE FROM PrivateTrainingFeeRecords;").ConfigureAwait(false);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM PrivateTrainingSessionRecords;").ConfigureAwait(false);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM PrivateTrainingMembers;").ConfigureAwait(false);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM AnnualCardMembers;").ConfigureAwait(false);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Coaches;").ConfigureAwait(false);

        var rng = new Random(options.Seed);

        // Coaches
        var coaches = new List<Coach>(capacity: options.CoachCount);
        for (var i = 1; i <= options.CoachCount; i++)
        {
            var employeeNo = $"C{i:0000}";
            var name = $"教练{i:0000}";
            coaches.Add(new Coach { EmployeeNo = employeeNo, Name = name });
        }

        // Add some edge-case coach names.
        if (coaches.Count >= 3)
        {
            coaches[0].Name = "张三·测试";
            coaches[1].Name = "O'Connor";
            coaches[2].EmployeeNo = new string('X', 32);
            coaches[2].Name = "边界工号32位";
        }

        db.Coaches.AddRange(coaches);
        await db.SaveChangesAsync().ConfigureAwait(false);

        // Private training members
        var ptMembers = new List<PrivateTrainingMember>(capacity: options.PrivateTrainingMemberCount);
        for (var i = 1; i <= options.PrivateTrainingMemberCount; i++)
        {
            var gender = (Gender)rng.Next(0, 3); // Unknown/Male/Female
            var name = $"私教会员{i:0000}";
            var phone = $"138{i:00000000}";

            // total sessions: include zeros and some big values
            var total = rng.Next(0, 51); // 0..50
            if (i % 97 == 0) total = 200;
            if (i % 113 == 0) total = 0;

            // used sessions: <= total
            var used = total == 0 ? 0 : rng.Next(0, Math.Min(total, 20) + 1);

            var member = new PrivateTrainingMember
            {
                Name = name,
                Gender = gender,
                Phone = phone,
                TotalSessions = total,
                UsedSessions = used,
                PaidAmount = 0
            };

            // fee records (0..3)
            var feeCount = rng.Next(0, 4);
            decimal paidSum = 0;
            for (var f = 0; f < feeCount; f++)
            {
                var amount = (decimal)(rng.Next(50, 501) * 10); // 500..5000
                paidSum += amount;
                member.FeeRecords.Add(new PrivateTrainingFeeRecord
                {
                    Amount = amount,
                    PaidAt = DateTime.Now.Date.AddDays(-rng.Next(0, 180)).AddHours(rng.Next(8, 22)),
                    Note = f == 0 ? "首单" : "追加"
                });
            }

            member.PaidAmount = paidSum;

            // session records (0..5) with sum == used (try best-effort)
            if (used > 0)
            {
                var remainingToAllocate = used;
                var recordCount = Math.Min(5, used);
                for (var s = 0; s < recordCount; s++)
                {
                    var maxThis = Math.Min(3, remainingToAllocate);
                    var sessionsUsed = (s == recordCount - 1) ? remainingToAllocate : rng.Next(1, maxThis + 1);
                    remainingToAllocate -= sessionsUsed;

                    member.SessionRecords.Add(new PrivateTrainingSessionRecord
                    {
                        SessionsUsed = sessionsUsed,
                        UsedAt = DateTime.Now.Date.AddDays(-rng.Next(0, 120)).AddHours(rng.Next(8, 22)),
                        Note = "消课"
                    });

                    if (remainingToAllocate <= 0)
                    {
                        break;
                    }
                }
            }

            // Make sure used sessions matches actual record sum (critical for UI display).
            member.UsedSessions = member.SessionRecords.Sum(x => x.SessionsUsed);

            ptMembers.Add(member);
        }

        // Add some edge-case PT member names/phones
        if (ptMembers.Count >= 3)
        {
            ptMembers[0].Name = "李四(特殊字符)";
            ptMembers[1].Name = "Name-With-Dash";
            ptMembers[2].Phone = "+86-13800000000";
        }

        db.PrivateTrainingMembers.AddRange(ptMembers);
        await db.SaveChangesAsync().ConfigureAwait(false);

        // Annual card members
        var annualMembers = new List<AnnualCardMember>(capacity: options.AnnualCardMemberCount);
        var today = DateTime.Today;

        for (var i = 1; i <= options.AnnualCardMemberCount; i++)
        {
            var gender = (Gender)rng.Next(0, 3);
            var name = $"年卡会员{i:0000}";
            var phone = $"139{i:00000000}";

            // Distribute statuses:
            // - 40% expired
            // - 20% expiring soon (including today)
            // - 40% normal
            DateTime endDate;
            if (i <= options.AnnualCardMemberCount * 0.4)
            {
                endDate = today.AddDays(-rng.Next(1, 120));
            }
            else if (i <= options.AnnualCardMemberCount * 0.6)
            {
                endDate = today.AddDays(rng.Next(0, options.AnnualCardExpiringDays + 1));
            }
            else
            {
                endDate = today.AddDays(rng.Next(options.AnnualCardExpiringDays + 2, 400));
            }

            // Start date: usually 1 year before end, but include edge cases
            var startDate = endDate.AddYears(-1);
            if (i % 101 == 0) startDate = endDate; // start == end edge case
            if (i % 137 == 0) startDate = today.AddDays(-1); // different

            startDate = startDate.Date;
            endDate = endDate.Date;
            if (startDate > endDate)
            {
                // Keep DB constraint EndDate >= StartDate satisfied.
                startDate = endDate;
            }

            annualMembers.Add(new AnnualCardMember
            {
                Name = name,
                Gender = gender,
                Phone = phone,
                StartDate = startDate,
                EndDate = endDate
            });
        }

        if (annualMembers.Count >= 2)
        {
            annualMembers[0].Name = "王五 / QA";
            annualMembers[1].Name = "测试空格 Name";
        }

        db.AnnualCardMembers.AddRange(annualMembers);
        await db.SaveChangesAsync().ConfigureAwait(false);

        sw.Stop();
        Console.WriteLine($"Seed 完成：Coaches={options.CoachCount}, PT={options.PrivateTrainingMemberCount}, Annual={options.AnnualCardMemberCount}，用时 {sw.ElapsedMilliseconds} ms");
    }

    private static async Task RunAutomatedChecksAsync(QaRunOptions options, List<TestResult> results)
    {
        await RunTestAsync(results, "T001", "数据库可连接", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            await using var db = provider.CreateDbContext();
            if (!await db.Database.CanConnectAsync().ConfigureAwait(false))
            {
                throw new InvalidOperationException("db.Database.CanConnect=false");
            }
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T010", "数据量符合预期", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            await using var db = provider.CreateDbContext();

            var coachCount = await db.Coaches.CountAsync().ConfigureAwait(false);
            var ptCount = await db.PrivateTrainingMembers.CountAsync().ConfigureAwait(false);
            var annualCount = await db.AnnualCardMembers.CountAsync().ConfigureAwait(false);

            Ensure(coachCount == options.CoachCount, $"Coaches 期望 {options.CoachCount}，实际 {coachCount}");
            Ensure(ptCount == options.PrivateTrainingMemberCount, $"PrivateTrainingMembers 期望 {options.PrivateTrainingMemberCount}，实际 {ptCount}");
            Ensure(annualCount == options.AnnualCardMemberCount, $"AnnualCardMembers 期望 {options.AnnualCardMemberCount}，实际 {annualCount}");
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T011", "年卡：SearchAsync 全量加载（大数据量不崩溃）", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            var service = new AnnualCardMemberService(provider);

            // 验证在大量年卡会员时不会触发 SQLite “too many SQL variables”（停卡状态填充会用到 IN 查询）。
            var list = await service.SearchAsync(keyword: null).ConfigureAwait(false);

            Ensure(list.Count == options.AnnualCardMemberCount,
                $"SearchAsync 全量期望 {options.AnnualCardMemberCount}，实际 {list.Count}");

            // Smoke: status 计算不会抛异常（主要验证 PauseStart/Resume 填充后 GetStatus 稳定）。
            var today = DateTime.Today;
            _ = list.Take(10).Select(x => x.GetStatus(today, options.AnnualCardExpiringDays)).ToList();
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T020", "私教课：check constraint（UsedSessions <= TotalSessions）生效", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            await using var db = provider.CreateDbContext();

            db.PrivateTrainingMembers.Add(new PrivateTrainingMember
            {
                Name = "约束测试",
                Gender = Gender.Unknown,
                Phone = "13899990000",
                TotalSessions = 1,
                UsedSessions = 2,
                PaidAmount = 0
            });

            try
            {
                await db.SaveChangesAsync().ConfigureAwait(false);
                throw new InvalidOperationException("期望 SaveChanges 抛出异常，但实际成功了（约束未生效）");
            }
            catch (DbUpdateException)
            {
                // pass
            }
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T021", "教练：工号重复创建会被拦截（业务校验）", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            var service = new CoachService(provider);

            try
            {
                await service.CreateAsync("C0001", "重复").ConfigureAwait(false);
                throw new InvalidOperationException("期望抛出 DomainValidationException，但未抛出。");
            }
            catch (DomainValidationException)
            {
                // pass
            }
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T022", "教练：必填校验（空工号/空姓名）", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            var service = new CoachService(provider);

            await AssertThrowsAsync<DomainValidationException>(
                () => service.CreateAsync("", "教练A"),
                "空工号应被拦截").ConfigureAwait(false);

            await AssertThrowsAsync<DomainValidationException>(
                () => service.CreateAsync("QA_EMPTY_NAME", ""),
                "空姓名应被拦截").ConfigureAwait(false);
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T023", "教练：批量删除（DeleteMany）", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            var service = new CoachService(provider);

            var no1 = $"QA_DEL_{Guid.NewGuid():N}".Substring(0, 16);
            var no2 = $"QA_DEL_{Guid.NewGuid():N}".Substring(0, 16);

            await service.CreateAsync(no1, "QA批量删除1").ConfigureAwait(false);
            await service.CreateAsync(no2, "QA批量删除2").ConfigureAwait(false);

            var deleted = await service.DeleteManyAsync(new[] { no1, no2, "NOT_EXISTS" }).ConfigureAwait(false);
            Ensure(deleted == 2, $"期望删除 2 条，实际 {deleted}");

            await using var db = provider.CreateDbContext();
            var left = await db.Coaches
                .AsNoTracking()
                .CountAsync(x => x.EmployeeNo == no1 || x.EmployeeNo == no2)
                .ConfigureAwait(false);

            Ensure(left == 0, $"批量删除后仍残留 {left} 条教练记录。");
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T030", "私教：消课不能导致剩余为负（业务校验）", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            var service = new PrivateTrainingMemberService(provider);

            await using var db = provider.CreateDbContext();
            var victim = await db.PrivateTrainingMembers
                .AsNoTracking()
                .OrderByDescending(x => x.TotalSessions)
                .FirstAsync().ConfigureAwait(false);

            var remaining = victim.TotalSessions - victim.UsedSessions;
            var tooMuch = remaining + 1;

            try
            {
                await service.ConsumeSessionsAsync(victim.Id, tooMuch, DateTime.Now, "超额消课").ConfigureAwait(false);
                throw new InvalidOperationException("期望抛出 DomainValidationException，但未抛出。");
            }
            catch (DomainValidationException)
            {
                // pass
            }
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T031", "私教：创建 + 初始缴费会生成缴费记录并累计 PaidAmount", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            var service = new PrivateTrainingMemberService(provider);

            var uniquePhone = $"QA-PT-{Guid.NewGuid():N}".Substring(0, 18); // <= 20 chars

            await service.CreateAsync(
                name: "QA私教创建",
                gender: Gender.Unknown,
                phone: uniquePhone,
                initialPaidAmount: 199,
                totalSessions: 10).ConfigureAwait(false);

            await using var db = provider.CreateDbContext();
            var created = await db.PrivateTrainingMembers
                .AsNoTracking()
                .FirstAsync(x => x.Phone == uniquePhone).ConfigureAwait(false);

            Ensure(created.PaidAmount == 199, $"PaidAmount 期望 199，实际 {created.PaidAmount}");

            var feeRecords = await db.PrivateTrainingFeeRecords
                .AsNoTracking()
                .Where(x => x.MemberId == created.Id)
                .ToListAsync().ConfigureAwait(false);

            Ensure(feeRecords.Count == 1, $"缴费记录数期望 1，实际 {feeRecords.Count}");
            Ensure(feeRecords[0].Amount == 199, $"缴费金额期望 199，实际 {feeRecords[0].Amount}");

            // cleanup
            await service.DeleteAsync(created.Id).ConfigureAwait(false);
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T032", "私教：删除会员会级联删除缴费/消课记录（DB 外键）", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            var service = new PrivateTrainingMemberService(provider);

            var uniquePhone = $"QA-PT-DEL-{Guid.NewGuid():N}".Substring(0, 20);

            await service.CreateAsync(
                name: "QA私教级联删除",
                gender: Gender.Male,
                phone: uniquePhone,
                initialPaidAmount: 100,
                totalSessions: 5).ConfigureAwait(false);

            int memberId;
            await using (var db = provider.CreateDbContext())
            {
                memberId = await db.PrivateTrainingMembers
                    .AsNoTracking()
                    .Where(x => x.Phone == uniquePhone)
                    .Select(x => x.Id)
                    .FirstAsync().ConfigureAwait(false);
            }

            await service.AddFeeAsync(memberId, 50, DateTime.Now, "追加").ConfigureAwait(false);
            await service.ConsumeSessionsAsync(memberId, 1, DateTime.Now, "消课").ConfigureAwait(false);

            await service.DeleteAsync(memberId).ConfigureAwait(false);

            await using (var db = provider.CreateDbContext())
            {
                var stillExists = await db.PrivateTrainingMembers
                    .AsNoTracking()
                    .AnyAsync(x => x.Id == memberId).ConfigureAwait(false);
                Ensure(!stillExists, "Member 删除后仍存在。");

                var feeLeft = await db.PrivateTrainingFeeRecords
                    .AsNoTracking()
                    .CountAsync(x => x.MemberId == memberId).ConfigureAwait(false);
                var sessionLeft = await db.PrivateTrainingSessionRecords
                    .AsNoTracking()
                    .CountAsync(x => x.MemberId == memberId).ConfigureAwait(false);

                Ensure(feeLeft == 0, $"缴费记录未级联删除：剩余 {feeLeft}");
                Ensure(sessionLeft == 0, $"消课记录未级联删除：剩余 {sessionLeft}");
            }
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T040", "年卡：结束日期早于开始日期会被拦截（业务校验）", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            var service = new AnnualCardMemberService(provider);

            var start = DateTime.Today;
            var end = start.AddDays(-1);

            try
            {
                await service.CreateAsync("非法年卡", Gender.Unknown, "13999990000", start, end).ConfigureAwait(false);
                throw new InvalidOperationException("期望抛出 DomainValidationException，但未抛出。");
            }
            catch (DomainValidationException)
            {
                // pass
            }
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T041", "年卡：续费规则（未过期顺延/已过期重开）", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            var service = new AnnualCardMemberService(provider);

            var today = DateTime.Today;

            // Not expired
            await service.CreateAsync(
                name: "QA年卡未过期",
                gender: Gender.Female,
                phone: $"QA-AN-{Guid.NewGuid():N}".Substring(0, 20),
                startDate: today.AddMonths(-6),
                endDate: today.AddDays(10)).ConfigureAwait(false);

            // Expired
            await service.CreateAsync(
                name: "QA年卡已过期",
                gender: Gender.Male,
                phone: $"QA-AN-EX-{Guid.NewGuid():N}".Substring(0, 20),
                startDate: today.AddYears(-2),
                endDate: today.AddDays(-1)).ConfigureAwait(false);

            await using var db = provider.CreateDbContext();
            var notExpired = await db.AnnualCardMembers.FirstAsync(x => x.Name == "QA年卡未过期").ConfigureAwait(false);
            var expired = await db.AnnualCardMembers.FirstAsync(x => x.Name == "QA年卡已过期").ConfigureAwait(false);

            var oldNotExpiredEnd = notExpired.EndDate.Date;
            var oldExpiredEnd = expired.EndDate.Date;

            await service.RenewAsync(notExpired.Id).ConfigureAwait(false);
            await service.RenewAsync(expired.Id).ConfigureAwait(false);

            var notExpiredAfter = await db.AnnualCardMembers.AsNoTracking().FirstAsync(x => x.Id == notExpired.Id).ConfigureAwait(false);
            var expiredAfter = await db.AnnualCardMembers.AsNoTracking().FirstAsync(x => x.Id == expired.Id).ConfigureAwait(false);

            Ensure(notExpiredAfter.EndDate.Date == oldNotExpiredEnd.AddYears(1), "未过期续费：EndDate 未按原截止日顺延 1 年。");

            Ensure(expiredAfter.StartDate.Date == today, "已过期续费：StartDate 未重置为今天。");
            Ensure(expiredAfter.EndDate.Date == today.AddYears(1), "已过期续费：EndDate 未重置为今天+1年。");
            Ensure(oldExpiredEnd < today, "测试数据构造失败：expired 原本不应 >= today。");

            var renewNotExpiredCount = await db.AnnualCardRenewRecords
                .AsNoTracking()
                .CountAsync(x => x.MemberId == notExpired.Id)
                .ConfigureAwait(false);

            var renewExpiredCount = await db.AnnualCardRenewRecords
                .AsNoTracking()
                .CountAsync(x => x.MemberId == expired.Id)
                .ConfigureAwait(false);

            Ensure(renewNotExpiredCount == 1, $"未过期续费：续费记录数期望 1，实际 {renewNotExpiredCount}");
            Ensure(renewExpiredCount == 1, $"已过期续费：续费记录数期望 1，实际 {renewExpiredCount}");

            var renewNotExpired = await db.AnnualCardRenewRecords
                .AsNoTracking()
                .FirstAsync(x => x.MemberId == notExpired.Id)
                .ConfigureAwait(false);

            Ensure(renewNotExpired.RenewedAt.Date == today, $"未过期续费：RenewedAt 期望 {today:yyyy-MM-dd}，实际 {renewNotExpired.RenewedAt:yyyy-MM-dd}");
            Ensure(renewNotExpired.EndDateBefore.Date == oldNotExpiredEnd, $"未过期续费：EndDateBefore 不正确：{renewNotExpired.EndDateBefore:yyyy-MM-dd}");
            Ensure(renewNotExpired.EndDateAfter.Date == oldNotExpiredEnd.AddYears(1), $"未过期续费：EndDateAfter 不正确：{renewNotExpired.EndDateAfter:yyyy-MM-dd}");

            var renewExpired = await db.AnnualCardRenewRecords
                .AsNoTracking()
                .FirstAsync(x => x.MemberId == expired.Id)
                .ConfigureAwait(false);

            Ensure(renewExpired.RenewedAt.Date == today, $"已过期续费：RenewedAt 期望 {today:yyyy-MM-dd}，实际 {renewExpired.RenewedAt:yyyy-MM-dd}");
            Ensure(renewExpired.StartDateAfter.Date == today, $"已过期续费：StartDateAfter 不正确：{renewExpired.StartDateAfter:yyyy-MM-dd}");
            Ensure(renewExpired.EndDateAfter.Date == today.AddYears(1), $"已过期续费：EndDateAfter 不正确：{renewExpired.EndDateAfter:yyyy-MM-dd}");

            // cleanup
                await service.DeleteAsync(notExpired.Id).ConfigureAwait(false);
                await service.DeleteAsync(expired.Id).ConfigureAwait(false);
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T042", "年卡：停卡（顺延截止 + 记录入库 + 状态=停卡中）", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            var service = new AnnualCardMemberService(provider);

            var today = DateTime.Today;
            var phone = $"QA-AN-PAUSE-{Guid.NewGuid():N}".Substring(0, 20);

            await service.CreateAsync(
                name: "QA年卡停卡",
                gender: Gender.Unknown,
                phone: phone,
                startDate: today.AddMonths(-1),
                endDate: today.AddDays(30)).ConfigureAwait(false);

            int memberId;
            DateTime oldEndDate;
            await using (var db = provider.CreateDbContext())
            {
                var member = await db.AnnualCardMembers
                    .AsNoTracking()
                    .FirstAsync(x => x.Phone == phone)
                    .ConfigureAwait(false);

                memberId = member.Id;
                oldEndDate = member.EndDate.Date;
            }

            await service.PauseAsync(memberId, pauseDays: 7).ConfigureAwait(false);

            await using (var db = provider.CreateDbContext())
            {
                var member = await db.AnnualCardMembers
                    .AsNoTracking()
                    .FirstAsync(x => x.Id == memberId)
                    .ConfigureAwait(false);

                Ensure(member.EndDate.Date == oldEndDate.AddDays(7),
                    $"停卡后 EndDate 期望 {oldEndDate.AddDays(7):yyyy-MM-dd}，实际 {member.EndDate:yyyy-MM-dd}");

                var pauses = await db.AnnualCardPauseRecords
                    .AsNoTracking()
                    .Where(x => x.MemberId == memberId)
                    .ToListAsync()
                    .ConfigureAwait(false);

                Ensure(pauses.Count == 1, $"停卡记录数期望 1，实际 {pauses.Count}");
                Ensure(pauses[0].PauseDays == 7, $"停卡天数期望 7，实际 {pauses[0].PauseDays}");
                Ensure(pauses[0].PauseStartDate.Date == today, $"停卡开始日期期望 {today:yyyy-MM-dd}，实际 {pauses[0].PauseStartDate:yyyy-MM-dd}");
                Ensure(pauses[0].ResumeDate.Date == today.AddDays(7), $"恢复日期期望 {today.AddDays(7):yyyy-MM-dd}，实际 {pauses[0].ResumeDate:yyyy-MM-dd}");
            }

            // 状态由 SearchAsync 填充停卡区间后计算得到
            var list = await service.SearchAsync(phone).ConfigureAwait(false);
            var item = list.FirstOrDefault(x => x.Id == memberId)
                       ?? throw new InvalidOperationException("SearchAsync 未返回目标会员。");

            var status = item.GetStatus(today, expiringDays: 3);
            Ensure(status == AnnualCardStatus.Paused, $"Status 期望 Paused，实际 {status}");

            await AssertThrowsAsync<DomainValidationException>(
                () => service.PauseAsync(memberId, pauseDays: 1),
                "重复停卡应被拦截").ConfigureAwait(false);

            // cleanup
            await service.DeleteAsync(memberId).ConfigureAwait(false);
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T060", "Excel：年卡导出文件/工作表/表头", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            var excel = new ExcelTransferService(provider);

            var tempDir = Path.Combine(options.RepoRoot, "qa", "tmp");
            Directory.CreateDirectory(tempDir);
            var path = Path.Combine(tempDir, $"annual_export_{Guid.NewGuid():N}.xlsx");

            try
            {
                await excel.ExportAnnualCardMembersAsync(path).ConfigureAwait(false);
                Ensure(File.Exists(path), $"导出文件未生成：{path}");

                using var wb = new XLWorkbook(path);
                var ws = wb.Worksheets.FirstOrDefault(x => x.Name == "年卡会员");
                if (ws is null)
                {
                    throw new InvalidOperationException("导出文件缺少工作表：年卡会员");
                }

                Ensure(ws.Cell(1, 1).GetString() == "Id", "表头[1,1]不是 Id");
                Ensure(ws.Cell(1, 2).GetString() == "姓名", "表头[1,2]不是 姓名");
                Ensure(ws.Cell(1, 4).GetString() == "电话", "表头[1,4]不是 电话");

                var wsPauses = wb.Worksheets.FirstOrDefault(x => x.Name == "停卡记录");
                if (wsPauses is null)
                {
                    throw new InvalidOperationException("导出文件缺少工作表：停卡记录");
                }

                Ensure(wsPauses.Cell(1, 1).GetString() == "Id", "停卡记录表头[1,1]不是 Id");
                Ensure(wsPauses.Cell(1, 2).GetString() == "会员Id", "停卡记录表头[1,2]不是 会员Id");
                Ensure(wsPauses.Cell(1, 5).GetString() == "停卡开始日期", "停卡记录表头[1,5]不是 停卡开始日期");
                Ensure(wsPauses.Cell(1, 7).GetString() == "停卡天数", "停卡记录表头[1,7]不是 停卡天数");

                var wsRenews = wb.Worksheets.FirstOrDefault(x => x.Name == "续费记录");
                if (wsRenews is null)
                {
                    throw new InvalidOperationException("导出文件缺少工作表：续费记录");
                }

                Ensure(wsRenews.Cell(1, 1).GetString() == "Id", "续费记录表头[1,1]不是 Id");
                Ensure(wsRenews.Cell(1, 2).GetString() == "会员Id", "续费记录表头[1,2]不是 会员Id");
                Ensure(wsRenews.Cell(1, 5).GetString() == "续费时间", "续费记录表头[1,5]不是 续费时间");
            }
            finally
            {
                TryDeleteFile(path);
            }
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T061", "Excel：年卡导入（新增+更新+错误收集）", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            var annualService = new AnnualCardMemberService(provider);
            var excel = new ExcelTransferService(provider);

            var tempDir = Path.Combine(options.RepoRoot, "qa", "tmp");
            Directory.CreateDirectory(tempDir);
            var path = Path.Combine(tempDir, $"annual_import_{Guid.NewGuid():N}.xlsx");

            var phoneExisting = $"139{Guid.NewGuid():N}".Substring(0, 20);
            var phoneNew = $"139{Guid.NewGuid():N}".Substring(0, 20);
            var phoneInvalid = $"139{Guid.NewGuid():N}".Substring(0, 20);

            await annualService.CreateAsync(
                name: "QA年卡待更新",
                gender: Gender.Male,
                phone: phoneExisting,
                startDate: DateTime.Today.AddDays(-10),
                endDate: DateTime.Today.AddDays(10)).ConfigureAwait(false);

            int existingId;
            await using (var db = provider.CreateDbContext())
            {
                existingId = await db.AnnualCardMembers
                    .AsNoTracking()
                    .Where(x => x.Phone == phoneExisting)
                    .Select(x => x.Id)
                    .FirstAsync()
                    .ConfigureAwait(false);
            }

            // Build an import workbook using the same header names as export.
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("年卡会员");
                ws.Cell(1, 1).Value = "Id";
                ws.Cell(1, 2).Value = "姓名";
                ws.Cell(1, 3).Value = "性别";
                ws.Cell(1, 4).Value = "电话";
                ws.Cell(1, 5).Value = "开通日期";
                ws.Cell(1, 6).Value = "截止日期";

                // update existing
                ws.Cell(2, 1).Value = existingId;
                ws.Cell(2, 2).Value = "QA年卡已更新";
                ws.Cell(2, 3).Value = "男";
                ws.Cell(2, 4).Value = phoneExisting;
                ws.Cell(2, 5).Value = DateTime.Today.AddDays(-5);
                ws.Cell(2, 6).Value = DateTime.Today.AddDays(30);

                // add new
                ws.Cell(3, 1).Value = 0;
                ws.Cell(3, 2).Value = "QA年卡新增";
                ws.Cell(3, 3).Value = "女";
                ws.Cell(3, 4).Value = phoneNew;
                ws.Cell(3, 5).Value = DateTime.Today;
                ws.Cell(3, 6).Value = DateTime.Today.AddDays(365);

                // invalid: missing name but has phone
                ws.Cell(4, 1).Value = 0;
                ws.Cell(4, 2).Value = "";
                ws.Cell(4, 3).Value = "未知";
                ws.Cell(4, 4).Value = phoneInvalid;
                ws.Cell(4, 5).Value = DateTime.Today;
                ws.Cell(4, 6).Value = DateTime.Today.AddDays(1);

                var wsPause = wb.Worksheets.Add("停卡记录");
                wsPause.Cell(1, 1).Value = "Id";
                wsPause.Cell(1, 2).Value = "会员Id";
                wsPause.Cell(1, 3).Value = "会员姓名";
                wsPause.Cell(1, 4).Value = "会员电话";
                wsPause.Cell(1, 5).Value = "停卡开始日期";
                wsPause.Cell(1, 6).Value = "恢复日期";
                wsPause.Cell(1, 7).Value = "停卡天数";
                wsPause.Cell(1, 8).Value = "停卡前截止日期";
                wsPause.Cell(1, 9).Value = "停卡后截止日期";
                wsPause.Cell(1, 10).Value = "备注";
                wsPause.Cell(1, 11).Value = "创建时间";

                wsPause.Cell(2, 1).Value = 0;
                wsPause.Cell(2, 2).Value = 0; // 用电话定位会员（测试：无会员Id也可导入）
                wsPause.Cell(2, 3).Value = "QA年卡已更新";
                wsPause.Cell(2, 4).Value = phoneExisting;
                wsPause.Cell(2, 5).Value = DateTime.Today;
                wsPause.Cell(2, 6).Value = DateTime.Today.AddDays(3);
                wsPause.Cell(2, 7).Value = 3;
                wsPause.Cell(2, 8).Value = DateTime.Today.AddDays(30);
                wsPause.Cell(2, 9).Value = DateTime.Today.AddDays(33);
                wsPause.Cell(2, 10).Value = "QA导入停卡记录";
                wsPause.Cell(2, 11).Value = DateTime.Now;

                var wsRenew = wb.Worksheets.Add("续费记录");
                wsRenew.Cell(1, 1).Value = "Id";
                wsRenew.Cell(1, 2).Value = "会员Id";
                wsRenew.Cell(1, 3).Value = "会员姓名";
                wsRenew.Cell(1, 4).Value = "会员电话";
                wsRenew.Cell(1, 5).Value = "续费时间";
                wsRenew.Cell(1, 6).Value = "续费前开通日期";
                wsRenew.Cell(1, 7).Value = "续费前截止日期";
                wsRenew.Cell(1, 8).Value = "续费后开通日期";
                wsRenew.Cell(1, 9).Value = "续费后截止日期";
                wsRenew.Cell(1, 10).Value = "备注";
                wsRenew.Cell(1, 11).Value = "创建时间";

                wsRenew.Cell(2, 1).Value = 0;
                wsRenew.Cell(2, 2).Value = 0; // 用电话定位会员
                wsRenew.Cell(2, 3).Value = "QA年卡已更新";
                wsRenew.Cell(2, 4).Value = phoneExisting;
                wsRenew.Cell(2, 5).Value = DateTime.Now;
                wsRenew.Cell(2, 6).Value = DateTime.Today.AddDays(-5);
                wsRenew.Cell(2, 7).Value = DateTime.Today.AddDays(33);
                wsRenew.Cell(2, 8).Value = DateTime.Today.AddDays(-5);
                wsRenew.Cell(2, 9).Value = DateTime.Today.AddDays(33).AddYears(1);
                wsRenew.Cell(2, 10).Value = "QA导入续费记录";
                wsRenew.Cell(2, 11).Value = DateTime.Now;

                wb.SaveAs(path);
            }

            try
            {
                var result = await excel.ImportAnnualCardMembersAsync(path).ConfigureAwait(false);

                Ensure(result.Updated == 1, $"Updated 期望 1，实际 {result.Updated}");
                Ensure(result.Added == 1, $"Added 期望 1，实际 {result.Added}");
                Ensure(result.Skipped == 1, $"Skipped 期望 1，实际 {result.Skipped}");
                Ensure(result.PauseRecordsAdded == 1, $"PauseRecordsAdded 期望 1，实际 {result.PauseRecordsAdded}");
                Ensure(result.PauseRecordsSkipped == 0, $"PauseRecordsSkipped 期望 0，实际 {result.PauseRecordsSkipped}");
                Ensure(result.RenewRecordsAdded == 1, $"RenewRecordsAdded 期望 1，实际 {result.RenewRecordsAdded}");
                Ensure(result.RenewRecordsSkipped == 0, $"RenewRecordsSkipped 期望 0，实际 {result.RenewRecordsSkipped}");
                Ensure(result.Errors.Count > 0, "期望 Errors > 0，但实际为空。");

                await using var db = provider.CreateDbContext();
                var updated = await db.AnnualCardMembers
                    .AsNoTracking()
                    .FirstAsync(x => x.Id == existingId)
                    .ConfigureAwait(false);

                Ensure(updated.Name == "QA年卡已更新", $"更新后 Name 不正确：{updated.Name}");
                Ensure(updated.EndDate.Date == DateTime.Today.AddDays(33).AddYears(1), $"更新后 EndDate 不正确：{updated.EndDate:yyyy-MM-dd}");

                var pauseCount = await db.AnnualCardPauseRecords
                    .AsNoTracking()
                    .CountAsync(x => x.MemberId == existingId)
                    .ConfigureAwait(false);
                Ensure(pauseCount == 1, $"停卡记录未写入数据库：memberId={existingId}");

                var renewCount = await db.AnnualCardRenewRecords
                    .AsNoTracking()
                    .CountAsync(x => x.MemberId == existingId)
                    .ConfigureAwait(false);
                Ensure(renewCount == 1, $"续费记录未写入数据库：memberId={existingId}");

                var added = await db.AnnualCardMembers
                    .AsNoTracking()
                    .CountAsync(x => x.Phone == phoneNew)
                    .ConfigureAwait(false);
                Ensure(added == 1, $"新增会员未写入数据库：phone={phoneNew}");
            }
            finally
            {
                // cleanup
                await using var db = provider.CreateDbContext();
                var ids = await db.AnnualCardMembers
                    .Where(x => x.Phone == phoneExisting || x.Phone == phoneNew || x.Phone == phoneInvalid)
                    .Select(x => x.Id)
                    .ToListAsync()
                    .ConfigureAwait(false);

                foreach (var id in ids)
                {
                    await annualService.DeleteAsync(id).ConfigureAwait(false);
                }

                TryDeleteFile(path);
            }
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T062", "Excel：私教导出包含 3 个工作表", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            var excel = new ExcelTransferService(provider);

            var tempDir = Path.Combine(options.RepoRoot, "qa", "tmp");
            Directory.CreateDirectory(tempDir);
            var path = Path.Combine(tempDir, $"pt_export_{Guid.NewGuid():N}.xlsx");

            try
            {
                await excel.ExportPrivateTrainingMembersAsync(path).ConfigureAwait(false);
                Ensure(File.Exists(path), $"导出文件未生成：{path}");

                using var wb = new XLWorkbook(path);
                Ensure(wb.Worksheets.Any(x => x.Name == "私教会员"), "缺少工作表：私教会员");
                Ensure(wb.Worksheets.Any(x => x.Name == "缴费记录"), "缺少工作表：缴费记录");
                Ensure(wb.Worksheets.Any(x => x.Name == "消课记录"), "缺少工作表：消课记录");
            }
            finally
            {
                TryDeleteFile(path);
            }
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T063", "Excel：私教导入追加模式（含去重）", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            var ptService = new PrivateTrainingMemberService(provider);
            var excel = new ExcelTransferService(provider);

            var tempDir = Path.Combine(options.RepoRoot, "qa", "tmp");
            Directory.CreateDirectory(tempDir);
            var path = Path.Combine(tempDir, $"pt_import_append_{Guid.NewGuid():N}.xlsx");

            var phoneExisting = $"138{Guid.NewGuid():N}".Substring(0, 20);
            var phoneNew = $"138{Guid.NewGuid():N}".Substring(0, 20);

            await ptService.CreateAsync(
                name: "QA私教待更新",
                gender: Gender.Male,
                phone: phoneExisting,
                initialPaidAmount: 100,
                totalSessions: 10).ConfigureAwait(false);

            int existingId;
            await using (var db = provider.CreateDbContext())
            {
                existingId = await db.PrivateTrainingMembers
                    .AsNoTracking()
                    .Where(x => x.Phone == phoneExisting)
                    .Select(x => x.Id)
                    .FirstAsync()
                    .ConfigureAwait(false);
            }

            using (var wb = new XLWorkbook())
            {
                var wsMembers = wb.Worksheets.Add("私教会员");
                wsMembers.Cell(1, 1).Value = "Id";
                wsMembers.Cell(1, 2).Value = "姓名";
                wsMembers.Cell(1, 3).Value = "性别";
                wsMembers.Cell(1, 4).Value = "电话";
                wsMembers.Cell(1, 5).Value = "总课程";

                // update existing: increase total sessions
                wsMembers.Cell(2, 1).Value = existingId;
                wsMembers.Cell(2, 2).Value = "QA私教已更新";
                wsMembers.Cell(2, 3).Value = "男";
                wsMembers.Cell(2, 4).Value = phoneExisting;
                wsMembers.Cell(2, 5).Value = 12;

                // add new
                wsMembers.Cell(3, 1).Value = 0;
                wsMembers.Cell(3, 2).Value = "QA私教新增";
                wsMembers.Cell(3, 3).Value = "女";
                wsMembers.Cell(3, 4).Value = phoneNew;
                wsMembers.Cell(3, 5).Value = 8;

                var wsFees = wb.Worksheets.Add("缴费记录");
                wsFees.Cell(1, 1).Value = "会员Id";
                wsFees.Cell(1, 2).Value = "会员电话";
                wsFees.Cell(1, 3).Value = "金额";
                wsFees.Cell(1, 4).Value = "缴费日期";
                wsFees.Cell(1, 5).Value = "备注";

                // one fee record for existing
                wsFees.Cell(2, 1).Value = existingId;
                wsFees.Cell(2, 2).Value = phoneExisting;
                wsFees.Cell(2, 3).Value = 50;
                wsFees.Cell(2, 4).Value = DateTime.Today;
                wsFees.Cell(2, 5).Value = "追加";

                var wsSessions = wb.Worksheets.Add("消课记录");
                wsSessions.Cell(1, 1).Value = "会员Id";
                wsSessions.Cell(1, 2).Value = "会员电话";
                wsSessions.Cell(1, 3).Value = "消耗课次";
                wsSessions.Cell(1, 4).Value = "消课日期";
                wsSessions.Cell(1, 5).Value = "备注";

                // one session record for existing
                wsSessions.Cell(2, 1).Value = existingId;
                wsSessions.Cell(2, 2).Value = phoneExisting;
                wsSessions.Cell(2, 3).Value = 1;
                wsSessions.Cell(2, 4).Value = DateTime.Today;
                wsSessions.Cell(2, 5).Value = "消课";

                wb.SaveAs(path);
            }

            try
            {
                var first = await excel.ImportPrivateTrainingMembersAsync(path, overwriteExisting: false).ConfigureAwait(false);
                Ensure(first.MembersUpdated == 1, $"第一次导入：MembersUpdated 期望 1，实际 {first.MembersUpdated}");
                Ensure(first.MembersAdded == 1, $"第一次导入：MembersAdded 期望 1，实际 {first.MembersAdded}");
                Ensure(first.FeeRecordsAdded == 1, $"第一次导入：FeeRecordsAdded 期望 1，实际 {first.FeeRecordsAdded}");
                Ensure(first.SessionRecordsAdded == 1, $"第一次导入：SessionRecordsAdded 期望 1，实际 {first.SessionRecordsAdded}");

                // import again: fee/session should be deduped
                var second = await excel.ImportPrivateTrainingMembersAsync(path, overwriteExisting: false).ConfigureAwait(false);
                Ensure(second.FeeRecordsAdded == 0, $"第二次导入：FeeRecordsAdded 期望 0（去重），实际 {second.FeeRecordsAdded}");
                Ensure(second.SessionRecordsAdded == 0, $"第二次导入：SessionRecordsAdded 期望 0（去重），实际 {second.SessionRecordsAdded}");

                await using var db = provider.CreateDbContext();
                var updated = await db.PrivateTrainingMembers
                    .AsNoTracking()
                    .FirstAsync(x => x.Id == existingId)
                    .ConfigureAwait(false);
                Ensure(updated.Name == "QA私教已更新", $"更新后 Name 不正确：{updated.Name}");
                Ensure(updated.TotalSessions == 12, $"更新后 TotalSessions 不正确：{updated.TotalSessions}");
            }
            finally
            {
                // cleanup both members by phone (cascade deletes fee/session via FK on member delete? service handles)
                await using (var db = provider.CreateDbContext())
                {
                    var memberIds = await db.PrivateTrainingMembers
                        .AsNoTracking()
                        .Where(x => x.Phone == phoneExisting || x.Phone == phoneNew)
                        .Select(x => x.Id)
                        .ToListAsync()
                        .ConfigureAwait(false);

                    foreach (var id in memberIds)
                    {
                        await ptService.DeleteAsync(id).ConfigureAwait(false);
                    }
                }

                TryDeleteFile(path);
            }
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T064", "Excel：私教导入覆盖模式（清空后导入）", async () =>
        {
            var tempDir = Path.Combine(options.RepoRoot, "qa", "tmp");
            Directory.CreateDirectory(tempDir);
            var path = Path.Combine(tempDir, $"pt_import_overwrite_{Guid.NewGuid():N}.xlsx");

            // Use an isolated DB for overwrite tests to avoid damaging the main seeded QA DB.
            var isolatedDb = Path.Combine(tempDir, $"gym_overwrite_{Guid.NewGuid():N}.db");
            await EnsureDatabaseCreatedAsync(isolatedDb).ConfigureAwait(false);

            var provider = MakeProvider(isolatedDb);
            var ptService = new PrivateTrainingMemberService(provider);
            var excel = new ExcelTransferService(provider);

            // Ensure there is at least one record to be cleared.
            var phoneOld = $"138{Guid.NewGuid():N}".Substring(0, 20);
            await ptService.CreateAsync(
                name: "QA私教待清空",
                gender: Gender.Unknown,
                phone: phoneOld,
                initialPaidAmount: 10,
                totalSessions: 1).ConfigureAwait(false);

            using (var wb = new XLWorkbook())
            {
                var wsMembers = wb.Worksheets.Add("私教会员");
                wsMembers.Cell(1, 1).Value = "Id";
                wsMembers.Cell(1, 2).Value = "姓名";
                wsMembers.Cell(1, 3).Value = "性别";
                wsMembers.Cell(1, 4).Value = "电话";
                wsMembers.Cell(1, 5).Value = "总课程";

                wsMembers.Cell(2, 1).Value = 0;
                wsMembers.Cell(2, 2).Value = "QA私教覆盖导入";
                wsMembers.Cell(2, 3).Value = "未知";
                wsMembers.Cell(2, 4).Value = $"138{Guid.NewGuid():N}".Substring(0, 20);
                wsMembers.Cell(2, 5).Value = 5;

                wb.SaveAs(path);
            }

            try
            {
                var result = await excel.ImportPrivateTrainingMembersAsync(path, overwriteExisting: true).ConfigureAwait(false);
                Ensure(result.MembersAdded == 1, $"覆盖导入：MembersAdded 期望 1，实际 {result.MembersAdded}");

                await using var db = provider.CreateDbContext();
                var total = await db.PrivateTrainingMembers.CountAsync().ConfigureAwait(false);
                Ensure(total == 1, $"覆盖导入后会员总数应为 1，实际 {total}");
            }
            finally
            {
                TryDeleteFile(path);
                TryDeleteFile(isolatedDb);
                TryDeleteFile($"{isolatedDb}-wal");
                TryDeleteFile($"{isolatedDb}-shm");
            }
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T070", "Excel：年卡导出->导入回归（数据一致性 + 幂等）", async () =>
        {
            var providerSrc = MakeProvider(options.TestDbPath);
            var annualService = new AnnualCardMemberService(providerSrc);
            var excelSrc = new ExcelTransferService(providerSrc);

            // Ensure there is at least one Pause + Renew record so the sheets are not empty.
            var phone = $"139{Guid.NewGuid():N}".Substring(0, 20);
            await annualService.CreateAsync(
                name: "QA 年卡回归",
                gender: Gender.Unknown,
                phone: phone,
                startDate: DateTime.Today.AddDays(-10),
                endDate: DateTime.Today.AddDays(10)).ConfigureAwait(false);

            var memberId = 0;
            try
            {
                await using (var db = providerSrc.CreateDbContext())
                {
                    memberId = await db.AnnualCardMembers
                        .AsNoTracking()
                        .Where(x => x.Phone == phone)
                        .Select(x => x.Id)
                        .FirstAsync()
                        .ConfigureAwait(false);
                }

                await annualService.PauseAsync(memberId, pauseDays: 7).ConfigureAwait(false);
                await annualService.RenewAsync(memberId).ConfigureAwait(false);

                int expectedMembers;
                int expectedPauses;
                int expectedRenews;
                await using (var db = providerSrc.CreateDbContext())
                {
                    expectedMembers = await db.AnnualCardMembers.CountAsync().ConfigureAwait(false);
                    expectedPauses = await db.AnnualCardPauseRecords.CountAsync().ConfigureAwait(false);
                    expectedRenews = await db.AnnualCardRenewRecords.CountAsync().ConfigureAwait(false);
                }

                var tempDir = Path.Combine(options.RepoRoot, "qa", "tmp");
                Directory.CreateDirectory(tempDir);
                var xlsx = Path.Combine(tempDir, $"annual_roundtrip_{Guid.NewGuid():N}.xlsx");

                var destDb = Path.Combine(tempDir, $"annual_roundtrip_{Guid.NewGuid():N}.db");
                await EnsureDatabaseCreatedAsync(destDb).ConfigureAwait(false);

                var providerDest = MakeProvider(destDb);
                var excelDest = new ExcelTransferService(providerDest);

                try
                {
                    await excelSrc.ExportAnnualCardMembersAsync(xlsx).ConfigureAwait(false);

                    var first = await excelDest.ImportAnnualCardMembersAsync(xlsx).ConfigureAwait(false);
                    Ensure(first.Errors.Count == 0, $"第一次导入存在错误：{string.Join(" | ", first.Errors.Take(5))}");

                    await using (var db = providerDest.CreateDbContext())
                    {
                        var members = await db.AnnualCardMembers.CountAsync().ConfigureAwait(false);
                        var pauses = await db.AnnualCardPauseRecords.CountAsync().ConfigureAwait(false);
                        var renews = await db.AnnualCardRenewRecords.CountAsync().ConfigureAwait(false);

                        Ensure(members == expectedMembers, $"导入后会员数量不一致：期望 {expectedMembers}，实际 {members}");
                        Ensure(pauses == expectedPauses, $"导入后停卡记录数量不一致：期望 {expectedPauses}，实际 {pauses}");
                        Ensure(renews == expectedRenews, $"导入后续费记录数量不一致：期望 {expectedRenews}，实际 {renews}");

                        var importedMember = await db.AnnualCardMembers
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x => x.Phone == phone)
                            .ConfigureAwait(false);
                        Ensure(importedMember is not null, "回归导入后找不到测试会员（Phone）。");

                        var pauseCount = await db.AnnualCardPauseRecords
                            .AsNoTracking()
                            .CountAsync(x => x.MemberPhone == phone)
                            .ConfigureAwait(false);
                        var renewCount = await db.AnnualCardRenewRecords
                            .AsNoTracking()
                            .CountAsync(x => x.MemberPhone == phone)
                            .ConfigureAwait(false);

                        Ensure(pauseCount >= 1, "回归导入后停卡记录未导入。");
                        Ensure(renewCount >= 1, "回归导入后续费记录未导入。");
                    }

                    var second = await excelDest.ImportAnnualCardMembersAsync(xlsx).ConfigureAwait(false);
                    Ensure(second.Errors.Count == 0, $"第二次导入存在错误：{string.Join(" | ", second.Errors.Take(5))}");

                    await using (var db = providerDest.CreateDbContext())
                    {
                        var pauses2 = await db.AnnualCardPauseRecords.CountAsync().ConfigureAwait(false);
                        var renews2 = await db.AnnualCardRenewRecords.CountAsync().ConfigureAwait(false);
                        Ensure(pauses2 == expectedPauses, "第二次导入后停卡记录不应增加。");
                        Ensure(renews2 == expectedRenews, "第二次导入后续费记录不应增加。");
                    }
                }
                finally
                {
                    TryDeleteFile(xlsx);
                    TryDeleteFile(destDb);
                    TryDeleteFile($"{destDb}-wal");
                    TryDeleteFile($"{destDb}-shm");
                }
            }
            finally
            {
                await annualService.DeleteAsync(memberId).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T071", "Excel：私教导出->导入回归（数据一致性 + 幂等）", async () =>
        {
            var providerSrc = MakeProvider(options.TestDbPath);
            var excelSrc = new ExcelTransferService(providerSrc);

            int expectedMembers;
            int expectedFees;
            int expectedSessions;
            await using (var db = providerSrc.CreateDbContext())
            {
                expectedMembers = await db.PrivateTrainingMembers.CountAsync().ConfigureAwait(false);
                expectedFees = await db.PrivateTrainingFeeRecords.CountAsync().ConfigureAwait(false);
                expectedSessions = await db.PrivateTrainingSessionRecords.CountAsync().ConfigureAwait(false);
            }

            var tempDir = Path.Combine(options.RepoRoot, "qa", "tmp");
            Directory.CreateDirectory(tempDir);
            var xlsx = Path.Combine(tempDir, $"pt_roundtrip_{Guid.NewGuid():N}.xlsx");

            var destDb = Path.Combine(tempDir, $"pt_roundtrip_{Guid.NewGuid():N}.db");
            await EnsureDatabaseCreatedAsync(destDb).ConfigureAwait(false);

            var providerDest = MakeProvider(destDb);
            var excelDest = new ExcelTransferService(providerDest);

            try
            {
                await excelSrc.ExportPrivateTrainingMembersAsync(xlsx).ConfigureAwait(false);

                var first = await excelDest.ImportPrivateTrainingMembersAsync(xlsx, overwriteExisting: false).ConfigureAwait(false);
                Ensure(first.Errors.Count == 0, $"第一次导入存在错误：{string.Join(" | ", first.Errors.Take(5))}");

                var expectedMembersAfter = Math.Max(0, expectedMembers - first.MembersSkipped);
                var expectedFeesAfter = Math.Max(0, expectedFees - first.FeeRecordsSkipped);
                var expectedSessionsAfter = Math.Max(0, expectedSessions - first.SessionRecordsSkipped);

                await using (var db = providerDest.CreateDbContext())
                {
                    var members = await db.PrivateTrainingMembers.CountAsync().ConfigureAwait(false);
                    var fees = await db.PrivateTrainingFeeRecords.CountAsync().ConfigureAwait(false);
                    var sessions = await db.PrivateTrainingSessionRecords.CountAsync().ConfigureAwait(false);

                    Ensure(members == expectedMembersAfter, $"导入后会员数量不一致：期望 {expectedMembersAfter}，实际 {members}");
                    Ensure(fees == expectedFeesAfter, $"导入后缴费记录数量不一致：期望 {expectedFeesAfter}，实际 {fees}");
                    Ensure(sessions == expectedSessionsAfter, $"导入后消课记录数量不一致：期望 {expectedSessionsAfter}，实际 {sessions}");
                }

                var second = await excelDest.ImportPrivateTrainingMembersAsync(xlsx, overwriteExisting: false).ConfigureAwait(false);
                Ensure(second.Errors.Count == 0, $"第二次导入存在错误：{string.Join(" | ", second.Errors.Take(5))}");

                await using (var db = providerDest.CreateDbContext())
                {
                    var fees2 = await db.PrivateTrainingFeeRecords.CountAsync().ConfigureAwait(false);
                    var sessions2 = await db.PrivateTrainingSessionRecords.CountAsync().ConfigureAwait(false);

                    Ensure(fees2 == expectedFeesAfter, "第二次导入后缴费记录不应增加。");
                    Ensure(sessions2 == expectedSessionsAfter, "第二次导入后消课记录不应增加。");
                }
            }
            finally
            {
                TryDeleteFile(xlsx);
                TryDeleteFile(destDb);
                TryDeleteFile($"{destDb}-wal");
                TryDeleteFile($"{destDb}-shm");
            }
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T072", "私教：聚合字段与明细记录一致（抽样校验）", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            await using var db = provider.CreateDbContext();

            var ids = await db.PrivateTrainingMembers
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .Take(50)
                .ToListAsync()
                .ConfigureAwait(false);

            foreach (var id in ids)
            {
                var member = await db.PrivateTrainingMembers
                    .AsNoTracking()
                    .FirstAsync(x => x.Id == id)
                    .ConfigureAwait(false);

                var feeSum = (await db.PrivateTrainingFeeRecords
                        .AsNoTracking()
                        .Where(x => x.MemberId == id)
                        .Select(x => x.Amount)
                        .ToListAsync()
                        .ConfigureAwait(false))
                    .Sum();

                var usedSum = await db.PrivateTrainingSessionRecords
                    .AsNoTracking()
                    .Where(x => x.MemberId == id)
                    .Select(x => (int?)x.SessionsUsed)
                    .SumAsync()
                    .ConfigureAwait(false) ?? 0;

                Ensure(member.PaidAmount == feeSum, $"MemberId={id} PaidAmount 不一致：{member.PaidAmount} vs {feeSum}");
                Ensure(member.UsedSessions == usedSum, $"MemberId={id} UsedSessions 不一致：{member.UsedSessions} vs {usedSum}");
            }
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T052", "PERF：列表查询耗时（年卡/私教/教练）", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);

            var annualService = new AnnualCardMemberService(provider);
            var ptService = new PrivateTrainingMemberService(provider);
            var coachService = new CoachService(provider);

            var swAnnual = Stopwatch.StartNew();
            var annualList = await annualService.SearchAsync(keyword: null).ConfigureAwait(false);
            swAnnual.Stop();

            var swPt = Stopwatch.StartNew();
            var ptList = await ptService.SearchAsync(keyword: null).ConfigureAwait(false);
            swPt.Stop();

            var swCoach = Stopwatch.StartNew();
            var coachList = await coachService.SearchAsync(keyword: null).ConfigureAwait(false);
            swCoach.Stop();

            Ensure(annualList.Count == options.AnnualCardMemberCount, $"Annual SearchAsync 数量不一致：{annualList.Count}");
            Ensure(ptList.Count == options.PrivateTrainingMemberCount, $"PT SearchAsync 数量不一致：{ptList.Count}");
            Ensure(coachList.Count == options.CoachCount, $"Coach SearchAsync 数量不一致：{coachList.Count}");

            results.Add(new TestResult(
                "PERF-002",
                "年卡 SearchAsync 耗时",
                Passed: true,
                Details: $"{swAnnual.ElapsedMilliseconds} ms（Annual={options.AnnualCardMemberCount}）"));

            results.Add(new TestResult(
                "PERF-003",
                "私教 SearchAsync 耗时",
                Passed: true,
                Details: $"{swPt.ElapsedMilliseconds} ms（PT={options.PrivateTrainingMemberCount}）"));

            results.Add(new TestResult(
                "PERF-004",
                "教练 SearchAsync 耗时",
                Passed: true,
                Details: $"{swCoach.ElapsedMilliseconds} ms（Coaches={options.CoachCount}）"));
        }).ConfigureAwait(false);

        await RunTestAsync(results, "T050", "Dashboard 快照查询可执行（性能/正确性基础）", async () =>
        {
            var provider = MakeProvider(options.TestDbPath);
            var service = new DashboardService(provider);

            var sw = Stopwatch.StartNew();
            var snapshot = await service.GetSnapshotAsync(
                annualCardExpiringDays: options.AnnualCardExpiringDays,
                lowRemainingThreshold: options.LowRemainingSessionsThreshold).ConfigureAwait(false);
            sw.Stop();

            Ensure(snapshot.CoachCount == options.CoachCount, $"CoachCount 不一致：{snapshot.CoachCount}");
            Ensure(snapshot.PrivateTrainingMemberCount == options.PrivateTrainingMemberCount, $"PTCount 不一致：{snapshot.PrivateTrainingMemberCount}");
            Ensure(snapshot.AnnualCardMemberCount == options.AnnualCardMemberCount, $"AnnualCount 不一致：{snapshot.AnnualCardMemberCount}");

            // Note: AnnualCardExpiringCount in current implementation equals the list size (Take(20)).
            await using var db = provider.CreateDbContext();
            var today = DateTime.Today;
            var expiringEndExclusive = today.AddDays(options.AnnualCardExpiringDays + 1);
            var totalExpiring = await db.AnnualCardMembers
                .AsNoTracking()
                .CountAsync(x => x.EndDate >= today && x.EndDate < expiringEndExclusive).ConfigureAwait(false);

            Ensure(
                snapshot.AnnualCardExpiringCount == totalExpiring,
                $"到期计数不一致：Dashboard={snapshot.AnnualCardExpiringCount}，DB={totalExpiring}");

            Ensure(
                snapshot.ExpiringAnnualCards.Count == Math.Min(totalExpiring, 20),
                $"到期列表条数不一致：List={snapshot.ExpiringAnnualCards.Count}，期望 {Math.Min(totalExpiring, 20)}");

            var expiringOrdered = snapshot.ExpiringAnnualCards
                .Select(x => x.EndDate.Date)
                .ToList();
            var expiringSorted = expiringOrdered
                .OrderBy(x => x)
                .ToList();
            Ensure(expiringOrdered.SequenceEqual(expiringSorted), "到期列表未按 EndDate 升序排序。");

            results.Add(new TestResult(
                "PERF-001",
                "Dashboard 查询耗时",
                Passed: true,
                 Details: $"{sw.ElapsedMilliseconds} ms（数据规模：Coaches={options.CoachCount}, PT={options.PrivateTrainingMemberCount}, Annual={options.AnnualCardMemberCount}）"));
        }).ConfigureAwait(false);
    }

    private static DbContextProvider MakeProvider(string dbPath)
    {
        var settings = new AppSettings
        {
            Database = new DatabaseSettings
            {
                Provider = "SQLite",
                Sqlite = new SqliteSettings { DbPath = dbPath }
            }
        };

        return new DbContextProvider(settings);
    }

    private static async Task<bool> SmokeStartExeAsync(QaRunOptions options, List<TestResult> results)
    {
        var exePath = Path.Combine(options.DistDir, "力量健身管理系统.exe");
        if (!File.Exists(exePath))
        {
            results.Add(new TestResult("SMOKE-000", "dist EXE 存在性检查", false, $"找不到文件：{exePath}"));
            return false;
        }

        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GymManager",
            "Logs",
            "app.log");

        try
        {
            var beforeLength = File.Exists(logPath) ? new FileInfo(logPath).Length : 0;

            // Start and wait a bit; then kill to avoid hanging the runner.
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = options.DistDir,
                UseShellExecute = true
            };

            var startedAt = DateTime.Now;
            var process = Process.Start(startInfo);

            await Task.Delay(TimeSpan.FromSeconds(8)).ConfigureAwait(false);

            // Best-effort close: try graceful, then force.
            if (process is not null && !process.HasExited)
            {
                try
                {
                    process.CloseMainWindow();
                }
                catch
                {
                    // ignore
                }

                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // ignore
                }
            }

            var hasLog = File.Exists(logPath);
            var logText = string.Empty;
            if (hasLog)
            {
                try
                {
                    using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (fs.Length >= beforeLength)
                    {
                        fs.Seek(beforeLength, SeekOrigin.Begin);
                    }
                    else
                    {
                        // log rotated or truncated
                        fs.Seek(0, SeekOrigin.Begin);
                    }

                    using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    logText = await reader.ReadToEndAsync().ConfigureAwait(false);
                }
                catch
                {
                    // ignore log read errors
                }
            }

            var hasUnhandled = logText.Contains("UnhandledException", StringComparison.OrdinalIgnoreCase)
                               || logText.Contains("EX(", StringComparison.OrdinalIgnoreCase)
                               || logText.Contains("DispatcherUnhandledException", StringComparison.OrdinalIgnoreCase);

            if (hasUnhandled)
            {
                results.Add(new TestResult(
                    "SMOKE-001",
                    "EXE 启动后无未处理异常日志",
                    false,
                    $"启动时间：{startedAt:HH:mm:ss}；发现 app.log 可能包含异常内容。路径：{logPath}\n\n{TrimLog(logText)}"));
                return false;
            }

            results.Add(new TestResult(
                "SMOKE-001",
                "EXE 启动后无未处理异常日志",
                true,
                $"启动时间：{startedAt:HH:mm:ss}；新增日志片段未发现明显未处理异常（如有 UI 异常仍建议人工回归）。路径：{logPath}"));
            return true;
        }
        finally { }
    }

    private static string TrimLog(string log)
    {
        const int max = 4000;
        if (string.IsNullOrWhiteSpace(log))
        {
            return "(空)";
        }

        if (log.Length <= max)
        {
            return log;
        }

        return log.Substring(log.Length - max, max);
    }

    private static async Task RunTestAsync(List<TestResult> results, string id, string name, Func<Task> test)
    {
        try
        {
            await test().ConfigureAwait(false);
            results.Add(new TestResult(id, name, true, "PASS"));
        }
        catch (Exception ex)
        {
            results.Add(new TestResult(id, name, false, ex.Message));
        }
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action, string message) where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
            throw new InvalidOperationException($"期望抛出 {typeof(TException).Name}：{message}");
        }
        catch (TException)
        {
            // expected
        }
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // ignore cleanup errors
        }
    }

    private static async Task WriteReportAsync(QaRunOptions options, List<TestResult> results, string reportPath)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# 力量健身管理系统 QA 测试报告");
        sb.AppendLine();
        sb.AppendLine($"- 生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- 环境：{Environment.OSVersion} / .NET {Environment.Version}");
        sb.AppendLine($"- EXE：{Path.Combine(options.DistDir, "力量健身管理系统.exe")}");
        sb.AppendLine($"- dist 配置：{Path.Combine(options.DistDir, "appsettings.json")}");
        sb.AppendLine($"- 测试 DB：{options.TestDbPath}");
        sb.AppendLine($"- 数据规模：Coaches={options.CoachCount}, 私教会员={options.PrivateTrainingMemberCount}, 年卡会员={options.AnnualCardMemberCount}");
        sb.AppendLine($"- 提醒阈值：年卡到期={options.AnnualCardExpiringDays} 天，私教课时不足={options.LowRemainingSessionsThreshold} 节");
        sb.AppendLine($"- 数据生成 Seed：{options.Seed}");
        sb.AppendLine();

        var passed = results.Count(r => r.Passed);
        var failed = results.Count - passed;
        sb.AppendLine($"## 汇总");
        sb.AppendLine();
        sb.AppendLine($"- 用例数：{results.Count}（通过 {passed} / 失败 {failed}）");
        sb.AppendLine();

        sb.AppendLine("## 明细");
        sb.AppendLine();
        foreach (var r in results)
        {
            sb.AppendLine($"- [{(r.Passed ? "PASS" : "FAIL")}] {r.Id} {r.Name}");
            if (!string.IsNullOrWhiteSpace(r.Details) && r.Details != "PASS")
            {
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine(r.Details);
                sb.AppendLine("```");
                sb.AppendLine();
            }
        }

        sb.AppendLine();
        sb.AppendLine("## 人工 UI 回归建议（大数据量）");
        sb.AppendLine();
        sb.AppendLine("1) 首页 Dashboard：计数是否正确、到期提醒横幅是否出现、点击横幅是否跳转到年卡模块且自动筛选“即将到期”。");
        sb.AppendLine("2) 教练管理：搜索/新增/编辑/删除，重复工号是否提示。");
        sb.AppendLine("3) 私教课会员：搜索/新增/编辑/删除；新增缴费记录/消课记录；消课超额是否提示；右侧缴费/消课明细是否刷新。");
        sb.AppendLine("4) 年卡会员：筛选（正常/即将到期/已过期）是否正确；续费规则（未过期顺延 1 年 / 已过期从今天重开 1 年）是否正确。");
        sb.AppendLine("5) 性能体验：列表滚动、搜索响应、进入页面加载速度；观察是否出现明显卡顿或 UI 卡死。");

        await File.WriteAllTextAsync(reportPath, sb.ToString(), Encoding.UTF8).ConfigureAwait(false);
    }

    private static void WriteSectionHeader(string title)
    {
        Console.WriteLine(new string('=', 80));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', 80));
    }
}
