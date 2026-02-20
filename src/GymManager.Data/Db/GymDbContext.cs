using GymManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymManager.Data.Db;

/// <summary>
/// EF Core 数据库上下文（支持 SQLite / SQL Server）。
/// </summary>
public sealed class GymDbContext : DbContext
{
    public GymDbContext(DbContextOptions<GymDbContext> options) : base(options)
    {
    }

    public DbSet<Coach> Coaches => Set<Coach>();
    public DbSet<PrivateTrainingMember> PrivateTrainingMembers => Set<PrivateTrainingMember>();
    public DbSet<PrivateTrainingFeeRecord> PrivateTrainingFeeRecords => Set<PrivateTrainingFeeRecord>();
    public DbSet<PrivateTrainingSessionRecord> PrivateTrainingSessionRecords => Set<PrivateTrainingSessionRecord>();
    public DbSet<AnnualCardMember> AnnualCardMembers => Set<AnnualCardMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Coach>(entity =>
        {
            entity.ToTable("Coaches");
            entity.HasKey(x => x.EmployeeNo);
            entity.Property(x => x.EmployeeNo).HasMaxLength(32);
            entity.Property(x => x.Name).HasMaxLength(50).IsRequired();

            entity.HasIndex(x => x.Name).HasDatabaseName("IX_Coaches_Name");
        });

        modelBuilder.Entity<PrivateTrainingMember>(entity =>
        {
            entity.ToTable("PrivateTrainingMembers", table =>
            {
                table.HasCheckConstraint(
                    "CK_PrivateTrainingMembers_Sessions",
                    "TotalSessions >= 0 AND UsedSessions >= 0 AND UsedSessions <= TotalSessions");
                table.HasCheckConstraint(
                    "CK_PrivateTrainingMembers_PaidAmount",
                    "PaidAmount >= 0");
            });
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(20).IsRequired();

            entity.Property(x => x.PaidAmount).HasPrecision(18, 2);

            entity.HasIndex(x => x.Phone).HasDatabaseName("IX_PrivateTrainingMembers_Phone");
            entity.HasIndex(x => x.Name).HasDatabaseName("IX_PrivateTrainingMembers_Name");
        });

        modelBuilder.Entity<PrivateTrainingFeeRecord>(entity =>
        {
            entity.ToTable("PrivateTrainingFeeRecords", table =>
            {
                table.HasCheckConstraint("CK_PrivateTrainingFeeRecords_Amount", "Amount > 0");
            });
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Note).HasMaxLength(200);

            entity.HasOne(x => x.Member)
                .WithMany(x => x.FeeRecords)
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.MemberId, x.PaidAt })
                .HasDatabaseName("IX_PrivateTrainingFeeRecords_MemberId_PaidAt");
        });

        modelBuilder.Entity<PrivateTrainingSessionRecord>(entity =>
        {
            entity.ToTable("PrivateTrainingSessionRecords", table =>
            {
                table.HasCheckConstraint("CK_PrivateTrainingSessionRecords_SessionsUsed", "SessionsUsed >= 1");
            });
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Note).HasMaxLength(200);

            entity.HasOne(x => x.Member)
                .WithMany(x => x.SessionRecords)
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.MemberId, x.UsedAt })
                .HasDatabaseName("IX_PrivateTrainingSessionRecords_MemberId_UsedAt");
        });

        modelBuilder.Entity<AnnualCardMember>(entity =>
        {
            entity.ToTable("AnnualCardMembers", table =>
            {
                table.HasCheckConstraint("CK_AnnualCardMembers_DateRange", "EndDate >= StartDate");
            });
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(20).IsRequired();

            entity.HasIndex(x => x.EndDate).HasDatabaseName("IX_AnnualCardMembers_EndDate");
            entity.HasIndex(x => x.Phone).HasDatabaseName("IX_AnnualCardMembers_Phone");
            entity.HasIndex(x => x.Name).HasDatabaseName("IX_AnnualCardMembers_Name");
        });
    }

    public override int SaveChanges()
    {
        UpdateAuditFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateAuditFields()
    {
        var now = DateTime.Now;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default)
                {
                    entry.Entity.CreatedAt = now;
                }

                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<PrivateTrainingFeeRecord>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
            {
                entry.Entity.CreatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<PrivateTrainingSessionRecord>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
            {
                entry.Entity.CreatedAt = now;
            }
        }
    }
}
