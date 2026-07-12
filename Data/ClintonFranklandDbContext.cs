using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Data;

/// <summary>
/// Entity Framework Core DbContext for the ClintonFrankland database
/// </summary>
public class ClintonFranklandDbContext : DbContext
{
    public ClintonFranklandDbContext(DbContextOptions<ClintonFranklandDbContext> options)
        : base(options)
    {
    }

    // DbSets for each entity
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountType> AccountTypes => Set<AccountType>();
    public DbSet<BudgetInvite> BudgetInvites => Set<BudgetInvite>();
    public DbSet<BudgetMember> BudgetMembers => Set<BudgetMember>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<CategoryBudgetTarget> CategoryBudgetTargets => Set<CategoryBudgetTarget>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Frequency> Frequencies => Set<Frequency>();
    public DbSet<Payee> Payees => Set<Payee>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionRule> TransactionRules => Set<TransactionRule>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuthLoginAudit> AuthLoginAudits => Set<AuthLoginAudit>();
    public DbSet<SmtpSetting> SmtpSettings => Set<SmtpSetting>();
    public DbSet<BillDueNotificationSetting> BillDueNotificationSettings => Set<BillDueNotificationSetting>();
    public DbSet<NotificationSendLog> NotificationSendLogs => Set<NotificationSendLog>();
    public DbSet<MigrationError> MigrationErrors => Set<MigrationError>();
    public DbSet<SharedBudget> SharedBudgets => Set<SharedBudget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Account entity
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasOne(a => a.AccountType)
                  .WithMany(at => at.Accounts)
                  .HasForeignKey(a => a.AccountTypeId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.User)
                  .WithMany(u => u.Accounts)
                  .HasForeignKey(a => a.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.SharedBudget)
                  .WithMany(b => b.Accounts)
                  .HasForeignKey(a => a.SharedBudgetId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SharedBudget>(entity =>
        {
            entity.HasKey(b => b.SharedBudgetId);
            entity.Property(b => b.Name).HasMaxLength(128).IsRequired();
            entity.Property(b => b.CreatedAtUtc).IsRequired();
            entity.Property(b => b.UpdatedAtUtc).IsRequired();
            entity.HasIndex(b => b.OwnerUserId);

            entity.HasOne(b => b.OwnerUser)
                  .WithMany(u => u.OwnedSharedBudgets)
                  .HasForeignKey(b => b.OwnerUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BudgetMember>(entity =>
        {
            entity.HasKey(m => m.BudgetMemberId);
            entity.Property(m => m.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(m => m.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(m => m.CreatedAtUtc).IsRequired();
            entity.HasIndex(m => new { m.SharedBudgetId, m.UserId }).IsUnique();
            entity.HasIndex(m => m.UserId);

            entity.HasOne(m => m.SharedBudget)
                  .WithMany(b => b.Members)
                  .HasForeignKey(m => m.SharedBudgetId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.User)
                  .WithMany(u => u.BudgetMemberships)
                  .HasForeignKey(m => m.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BudgetInvite>(entity =>
        {
            entity.HasKey(i => i.BudgetInviteId);
            entity.Property(i => i.InviteTokenHash).HasMaxLength(128).IsRequired();
            entity.Property(i => i.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(i => i.InviteeEmail).HasMaxLength(256);
            entity.Property(i => i.InviteeUserName).HasMaxLength(64);
            entity.Property(i => i.CreatedAtUtc).IsRequired();
            entity.Property(i => i.ExpiresAtUtc).IsRequired();
            entity.HasIndex(i => i.InviteTokenHash).IsUnique();
            entity.HasIndex(i => new { i.SharedBudgetId, i.ExpiresAtUtc });

            entity.HasOne(i => i.SharedBudget)
                  .WithMany(b => b.Invites)
                  .HasForeignKey(i => i.SharedBudgetId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.InvitedByUser)
                  .WithMany(u => u.SentBudgetInvites)
                  .HasForeignKey(i => i.InvitedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Budget entity
        modelBuilder.Entity<Budget>(entity =>
        {
            entity.HasOne(b => b.Category)
                  .WithMany(c => c.Budgets)
                  .HasForeignKey(b => b.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Frequency)
                  .WithMany(f => f.Budgets)
                  .HasForeignKey(b => b.FrequencyId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Payee)
                  .WithMany(p => p.Budgets)
                  .HasForeignKey(b => b.PayeeId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.User)
                  .WithMany(u => u.Budgets)
                  .HasForeignKey(b => b.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.SharedBudget)
                  .WithMany(sb => sb.Budgets)
                  .HasForeignKey(b => b.SharedBudgetId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Category entity
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasOne(c => c.User)
                  .WithMany(u => u.Categories)
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.SharedBudget)
                  .WithMany(b => b.Categories)
                  .HasForeignKey(c => c.SharedBudgetId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CategoryBudgetTarget>(entity =>
        {
            entity.HasKey(t => t.CategoryBudgetTargetId);
            entity.Property(t => t.UpdatedAtUtc).IsRequired();

            entity.HasIndex(t => new { t.UserId, t.CategoryId, t.BudgetMonth }).IsUnique();
            entity.HasIndex(t => new { t.SharedBudgetId, t.CategoryId, t.BudgetMonth })
                  .IsUnique()
                  .HasFilter("[SharedBudgetId] IS NOT NULL");

            entity.HasOne(t => t.User)
                  .WithMany(u => u.CategoryBudgetTargets)
                  .HasForeignKey(t => t.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Category)
                  .WithMany(c => c.CategoryBudgetTargets)
                  .HasForeignKey(t => t.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.SharedBudget)
                  .WithMany(b => b.CategoryBudgetTargets)
                  .HasForeignKey(t => t.SharedBudgetId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Payee entity
        modelBuilder.Entity<Payee>(entity =>
        {
            entity.HasOne(p => p.User)
                  .WithMany(u => u.Payees)
                  .HasForeignKey(p => p.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Transaction entity
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasOne(t => t.Account)
                  .WithMany(a => a.Transactions)
                  .HasForeignKey(t => t.AccountId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Category)
                  .WithMany(c => c.Transactions)
                  .HasForeignKey(t => t.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Payee)
                  .WithMany(p => p.Transactions)
                  .HasForeignKey(t => t.PayeeId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.User)
                  .WithMany(u => u.Transactions)
                  .HasForeignKey(t => t.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.SharedBudget)
                  .WithMany(b => b.Transactions)
                  .HasForeignKey(t => t.SharedBudgetId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Frequency entity - order by Sort column
        modelBuilder.Entity<Frequency>(entity =>
        {
            entity.Property(f => f.Sort).HasDefaultValue(0);
        });

        modelBuilder.Entity<AuthLoginAudit>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.AttemptedAtUtc).IsRequired();
            entity.Property(a => a.Succeeded).IsRequired();
            entity.HasIndex(a => a.AttemptedAtUtc);
            entity.HasIndex(a => a.UserName);
        });

        modelBuilder.Entity<SmtpSetting>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.UpdatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<BillDueNotificationSetting>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.UpdatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<NotificationSendLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => new { x.UserId, x.BudgetId, x.NoticeType, x.NoticeLocalDate }).IsUnique();
            entity.HasIndex(x => x.SharedBudgetId);
        });

        modelBuilder.Entity<MigrationError>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MigrationName).HasMaxLength(256);
            entity.Property(x => x.ErrorMessage).IsRequired();
            entity.Property(x => x.StackTrace).IsRequired();
            entity.Property(x => x.OccurredAt)
                .IsRequired()
                .HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(x => x.OccurredAt);
        });

        modelBuilder.Entity<TransactionRule>(entity =>
        {
            entity.HasKey(r => r.TransactionRuleId);
            entity.Property(r => r.ContainsText).HasMaxLength(256).IsRequired();
            entity.Property(r => r.CategoryName).HasMaxLength(128);
            entity.Property(r => r.PayeeName).HasMaxLength(255);
            entity.Property(r => r.Notes).HasMaxLength(500);
            entity.Property(r => r.UpdatedAtUtc).IsRequired();
            entity.HasIndex(r => new { r.UserId, r.Priority });
        });

        // Configure User notification preferences with defaults
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => new { u.ExternalProvider, u.ExternalSubject })
                  .IsUnique()
                  .HasFilter("[IsDeleted] = 0 AND [ExternalProvider] IS NOT NULL AND [ExternalSubject] IS NOT NULL");

            entity.Property(u => u.ReceiveBillDueNotices).HasDefaultValue(false);
            entity.Property(u => u.ReceiveWeeklyUpcomingBillDigest).HasDefaultValue(false);
            entity.Property(u => u.NotificationTimezone).HasDefaultValue("America/New_York");
            entity.Property(u => u.NotificationDeliveryTime).HasDefaultValue(new TimeOnly(8, 0));
        });
    }
}
