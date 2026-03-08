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
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Frequency> Frequencies => Set<Frequency>();
    public DbSet<Payee> Payees => Set<Payee>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuthLoginAudit> AuthLoginAudits => Set<AuthLoginAudit>();
    public DbSet<SmtpSetting> SmtpSettings => Set<SmtpSetting>();

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
        });

        // Configure Category entity
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasOne(c => c.User)
                  .WithMany(u => u.Categories)
                  .HasForeignKey(c => c.UserId)
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
    }
}
