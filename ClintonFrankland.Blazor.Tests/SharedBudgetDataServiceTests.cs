using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Blazor.Tests;

public class SharedBudgetDataServiceTests
{
    [Fact]
    public async Task GetDefaultSharedBudgetIdAsync_CreatesOneOwnerMembershipPerUser()
    {
        await using var db = CreateDbContext();
        db.Users.Add(User(42, "clinton", "Clinton"));
        await db.SaveChangesAsync();

        var service = new SharedBudgetDataService(db);

        var first = await service.GetDefaultSharedBudgetIdAsync(42);
        var second = await service.GetDefaultSharedBudgetIdAsync(42);

        Assert.Equal(first, second);
        var sharedBudget = Assert.Single(await db.SharedBudgets.ToListAsync());
        Assert.Equal(first, sharedBudget.SharedBudgetId);
        Assert.Equal(42, sharedBudget.OwnerUserId);

        var member = Assert.Single(await db.BudgetMembers.ToListAsync());
        Assert.Equal(first, member.SharedBudgetId);
        Assert.Equal(42, member.UserId);
        Assert.Equal(BudgetMemberRole.Owner, member.Role);
        Assert.Equal(BudgetMemberStatus.Active, member.Status);
    }

    [Fact]
    public async Task SharedBudgetModel_SupportsInviteAndMemberRolesWithoutPlaintextToken()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;
        db.Users.Add(User(42, "owner", "Owner"));
        db.SharedBudgets.Add(new SharedBudget
        {
            SharedBudgetId = 1,
            Name = "Household",
            OwnerUserId = 42,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        db.BudgetMembers.AddRange(
            Member(1, BudgetMemberRole.Owner),
            Member(2, BudgetMemberRole.Admin),
            Member(3, BudgetMemberRole.Editor),
            Member(4, BudgetMemberRole.Viewer));
        db.BudgetInvites.Add(new BudgetInvite
        {
            BudgetInviteId = 1,
            SharedBudgetId = 1,
            InvitedByUserId = 42,
            InviteTokenHash = new string('a', 64),
            Role = BudgetMemberRole.Editor,
            ExpiresAtUtc = now.AddDays(7),
            CreatedAtUtc = now
        });
        await db.SaveChangesAsync();

        var roles = await db.BudgetMembers
            .OrderBy(m => m.BudgetMemberId)
            .Select(m => m.Role)
            .ToListAsync();
        var invite = await db.BudgetInvites.SingleAsync();

        Assert.Equal(
            [BudgetMemberRole.Owner, BudgetMemberRole.Admin, BudgetMemberRole.Editor, BudgetMemberRole.Viewer],
            roles);
        Assert.Equal(new string('a', 64), invite.InviteTokenHash);
        Assert.DoesNotContain(
            typeof(BudgetInvite).GetProperties().Select(p => p.Name),
            name => name.Contains("Plain", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Token", StringComparison.OrdinalIgnoreCase));

        BudgetMember Member(int id, BudgetMemberRole role) => new()
        {
            BudgetMemberId = id,
            SharedBudgetId = 1,
            UserId = 42,
            Role = role,
            Status = BudgetMemberStatus.Active,
            CreatedAtUtc = now
        };
    }

    [Fact]
    public async Task ExistingUserRowsWithoutSharedBudgetId_RemainVisibleAndNewRowsAreStamped()
    {
        await using var db = CreateDbContext();
        SeedAccountTypes(db);
        db.Users.Add(User(42, "clinton", "Clinton"));
        db.Accounts.Add(new Account
        {
            AccountId = 1,
            AccountName = "Legacy Checking",
            AccountTypeId = 1,
            BeginningBalance = 100m,
            Balance = 100m,
            ClearedBalance = 100m,
            IsDefault = true,
            UserId = 42,
            SharedBudgetId = null
        });
        db.Categories.Add(new Category { CategoryId = 1, CategoryName = "Legacy", UserId = 42, SharedBudgetId = null });
        await db.SaveChangesAsync();

        var sharedBudgets = new SharedBudgetDataService(db);
        var accounts = await new AccountsDataService(db, sharedBudgets).GetAccountsForUserAsync(42);
        var categories = await new CheckbookDataService(db, sharedBudgets).GetCategoriesForUserAsync(42);

        await new AccountsDataService(db, sharedBudgets)
            .SaveAccountAsync(42, -1, "New Checking", "", 1, 50m, 0m, 0m, 1, 0m, 0m, "", DateTime.UtcNow);

        var created = await db.Accounts.SingleAsync(a => a.AccountName == "New Checking");

        Assert.Contains(accounts, a => a.AccountName == "Legacy Checking" && a.SharedBudgetId is null);
        Assert.Contains(categories, c => c.CategoryName == "Legacy" && c.SharedBudgetId is null);
        Assert.NotNull(created.SharedBudgetId);
        Assert.Single(await db.SharedBudgets.ToListAsync());
        Assert.Single(await db.BudgetMembers.ToListAsync());
    }

    private static ClintonFranklandDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ClintonFranklandDbContext(options);
    }

    private static void SeedAccountTypes(ClintonFranklandDbContext db)
    {
        db.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "Checking" });
    }

    private static User User(int id, string userName, string displayName) => new()
    {
        UserId = id,
        SiteId = 1,
        UserName = userName,
        DisplayName = displayName,
        IsAdmin = false,
        Salt = "salt",
        PasswordHash = "hash",
        IsDeleted = false,
        FirstLogin = DateTime.UtcNow,
        LastLogin = DateTime.UtcNow
    };
}
