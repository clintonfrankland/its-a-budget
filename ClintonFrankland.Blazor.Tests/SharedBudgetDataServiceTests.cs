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

    [Fact]
    public async Task SharedBudgetMembers_CanReadSharedFinancialDataButNonMembersCannot()
    {
        await using var db = CreateDbContext();
        SeedAccountTypes(db);
        SeedSharedBudget(db, BudgetMemberRole.Viewer);
        await db.SaveChangesAsync();

        var sharedBudgets = new SharedBudgetDataService(db);
        var viewerAccounts = await new AccountsDataService(db, sharedBudgets).GetAccountsForUserAsync(2);
        var outsiderAccounts = await new AccountsDataService(db, sharedBudgets).GetAccountsForUserAsync(3);
        var viewerTransactions = await new CheckbookDataService(db, sharedBudgets).GetTransactionsForUserAsync(2);
        var outsiderTransactions = await new CheckbookDataService(db, sharedBudgets).GetTransactionsForUserAsync(3);
        var viewerBudgets = await new BudgetItemsDataService(db, sharedBudgets).GetBudgetsForUserAsync(2);
        var outsiderBudgets = await new BudgetItemsDataService(db, sharedBudgets).GetBudgetsForUserAsync(3);

        Assert.Contains(viewerAccounts, a => a.AccountName == "House Checking");
        Assert.DoesNotContain(viewerAccounts, a => a.AccountName == "Owner Legacy");
        Assert.Empty(outsiderAccounts);
        Assert.Contains(viewerTransactions, t => t.Amount == -25m);
        Assert.Empty(outsiderTransactions);
        Assert.Contains(viewerBudgets, b => b.BudgetName == "Shared Bill");
        Assert.Empty(outsiderBudgets);
        Assert.Null(await new AccountsDataService(db, sharedBudgets).GetAccountByIdAsync(3, 1));
        Assert.Null(await new CheckbookDataService(db, sharedBudgets).GetTransactionByIdAsync(3, 1));
        Assert.Null(await new BudgetItemsDataService(db, sharedBudgets).GetBudgetByIdAsync(3, 1));
    }

    [Fact]
    public async Task SharedBudgetViewer_CannotMutateSharedFinancialData()
    {
        await using var db = CreateDbContext();
        SeedAccountTypes(db);
        SeedSharedBudget(db, BudgetMemberRole.Viewer);
        await db.SaveChangesAsync();

        var sharedBudgets = new SharedBudgetDataService(db);
        await new AccountsDataService(db, sharedBudgets)
            .SaveAccountAsync(2, 1, "Viewer Edit", "", 1, 1m, 0m, 0m, 1, 0m, 0m, "", DateTime.UtcNow);
        await new CheckbookDataService(db, sharedBudgets)
            .SaveTransactionAsync(2, 1, new DateOnly(2026, 7, 6), "Viewer", "Utilities", -1m, true, null, null);
        await new BudgetItemsDataService(db, sharedBudgets)
            .DeleteBudgetAsync(2, 1);
        await new BudgetScheduleService(db, sharedBudgets)
            .MarkBudgetPaidAsync(2, 1);

        Assert.Equal("House Checking", (await db.Accounts.FindAsync(1))!.AccountName);
        Assert.Equal(-25m, (await db.Transactions.FindAsync(1))!.Amount);
        Assert.NotNull(await db.Budgets.FindAsync(1));
        Assert.Equal(new DateTime(2026, 7, 5), (await db.Budgets.FindAsync(1))!.NextDueDate);
    }

    [Fact]
    public async Task SharedBudgetEditor_CanMutateSharedFinancialDataButNotMemberAdminActions()
    {
        await using var db = CreateDbContext();
        SeedAccountTypes(db);
        SeedSharedBudget(db, BudgetMemberRole.Editor);
        await db.SaveChangesAsync();

        var sharedBudgets = new SharedBudgetDataService(db);
        await new AccountsDataService(db, sharedBudgets)
            .SaveAccountAsync(2, 1, "Editor Edit", "", 1, 10m, 0m, 0m, 1, 0m, 0m, "", DateTime.UtcNow);
        await new CheckbookDataService(db, sharedBudgets)
            .SetTransactionClearedAsync(2, 1, true);

        Assert.Equal("Editor Edit", (await db.Accounts.FindAsync(1))!.AccountName);
        Assert.True((await db.Transactions.FindAsync(1))!.Cleared);
        Assert.True(await sharedBudgets.CanManageFinancialDataAsync(2, 1, 1));
        Assert.False(await sharedBudgets.CanManageMembersAsync(2, 1));
        Assert.False(await sharedBudgets.CanPerformOwnerActionAsync(2, 1));
    }

    [Fact]
    public async Task BudgetInviteOwner_CreatesHighEntropyHashOnlyInviteForUsername()
    {
        await using var db = CreateDbContext();
        SeedSharedBudget(db, BudgetMemberRole.Viewer);
        await db.SaveChangesAsync();

        var service = CreateInviteService(db);

        var result = await service.CreateInviteAsync(1, 1, "member", BudgetMemberRole.Editor);

        Assert.True(result.PlainToken.Length >= 40);
        Assert.DoesNotContain(result.PlainToken, result.Invite.InviteTokenHash, StringComparison.Ordinal);
        Assert.Equal(64, result.Invite.InviteTokenHash.Length);
        Assert.Equal("member", result.Invite.InviteeUserName);
        Assert.Equal(BudgetMemberRole.Editor, result.Invite.Role);
        Assert.DoesNotContain(
            typeof(BudgetInvite).GetProperties().Select(p => p.Name),
            name => name.Contains("Plain", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BudgetInviteAccept_CreatesMemberExactlyOnce()
    {
        await using var db = CreateDbContext();
        SeedSharedBudget(db, BudgetMemberRole.Viewer, includeMember: false);
        await db.SaveChangesAsync();
        var service = CreateInviteService(db);
        var invite = await service.CreateInviteAsync(1, 1, "member", BudgetMemberRole.Admin);

        var first = await service.AcceptInviteAsync(invite.PlainToken, 2);
        var second = await service.AcceptInviteAsync(invite.PlainToken, 2);

        Assert.Equal(BudgetInviteAcceptStatus.Accepted, first.Status);
        Assert.Equal(BudgetInviteAcceptStatus.AlreadyAccepted, second.Status);
        var member = Assert.Single(await db.BudgetMembers.Where(m => m.UserId == 2).ToListAsync());
        Assert.Equal(BudgetMemberRole.Admin, member.Role);
        Assert.Equal(BudgetMemberStatus.Active, member.Status);
        Assert.Equal(2, await db.BudgetMembers.CountAsync());
        Assert.Equal(2, (await db.BudgetInvites.SingleAsync()).AcceptedByUserId);
    }

    [Fact]
    public async Task BudgetInviteAccept_ReturnsExpiredRevokedAndWrongUserStates()
    {
        await using var db = CreateDbContext();
        SeedSharedBudget(db, BudgetMemberRole.Viewer, includeMember: false);
        await db.SaveChangesAsync();
        var service = CreateInviteService(db);

        var expired = await service.CreateInviteAsync(1, 1, "member", BudgetMemberRole.Viewer, expirationDays: 1);
        var expiredInvite = await db.BudgetInvites.SingleAsync(i => i.BudgetInviteId == expired.Invite.BudgetInviteId);
        expiredInvite.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);

        var revoked = await service.CreateInviteAsync(1, 1, "member", BudgetMemberRole.Viewer);
        var revokedInvite = await db.BudgetInvites.SingleAsync(i => i.BudgetInviteId == revoked.Invite.BudgetInviteId);
        revokedInvite.RevokedAtUtc = DateTime.UtcNow;
        revokedInvite.RevokedByUserId = 1;

        var wrongUser = await service.CreateInviteAsync(1, 1, "member", BudgetMemberRole.Viewer);
        await db.SaveChangesAsync();

        Assert.Equal(BudgetInviteAcceptStatus.Expired, (await service.AcceptInviteAsync(expired.PlainToken, 2)).Status);
        Assert.Equal(BudgetInviteAcceptStatus.Revoked, (await service.AcceptInviteAsync(revoked.PlainToken, 2)).Status);
        Assert.Equal(BudgetInviteAcceptStatus.WrongUser, (await service.AcceptInviteAsync(wrongUser.PlainToken, 3)).Status);
        Assert.DoesNotContain(await db.BudgetMembers.ToListAsync(), m => m.UserId == 2);
    }

    [Fact]
    public async Task BudgetInviteAccept_RequiresAuthenticatedBudgetUser()
    {
        await using var db = CreateDbContext();
        SeedSharedBudget(db, BudgetMemberRole.Viewer, includeMember: false);
        await db.SaveChangesAsync();
        var service = CreateInviteService(db);
        var invite = await service.CreateInviteAsync(1, 1, "member", BudgetMemberRole.Viewer);

        Assert.Equal(
            BudgetInviteAcceptStatus.AuthenticationRequired,
            (await service.AcceptInviteAsync(invite.PlainToken, 0)).Status);

        Assert.Equal(
            BudgetInviteAcceptStatus.AuthenticationRequired,
            (await service.AcceptInviteAsync(invite.PlainToken, 999)).Status);
    }

    [Fact]
    public async Task BudgetInviteManageActions_AreLimitedToOwnerAndAdmin()
    {
        await using var db = CreateDbContext();
        SeedSharedBudget(db, BudgetMemberRole.Editor);
        db.Users.Add(User(4, "admin", "Admin"));
        db.BudgetMembers.Add(new BudgetMember
        {
            BudgetMemberId = 3,
            SharedBudgetId = 1,
            UserId = 4,
            Role = BudgetMemberRole.Admin,
            Status = BudgetMemberStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = CreateInviteService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateInviteAsync(2, 1, "member@example.com", BudgetMemberRole.Viewer));

        var createdByAdmin = await service.CreateInviteAsync(4, 1, "member@example.com", BudgetMemberRole.Viewer);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RevokeInviteAsync(2, createdByAdmin.Invite.BudgetInviteId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ResendInviteAsync(2, createdByAdmin.Invite.BudgetInviteId));

        Assert.True(await service.RevokeInviteAsync(1, createdByAdmin.Invite.BudgetInviteId));
    }

    [Fact]
    public async Task BudgetInviteResend_RotatesTokenAndReopensInvite()
    {
        await using var db = CreateDbContext();
        SeedSharedBudget(db, BudgetMemberRole.Viewer, includeMember: false);
        await db.SaveChangesAsync();
        var service = CreateInviteService(db);
        var created = await service.CreateInviteAsync(1, 1, "member", BudgetMemberRole.Viewer);
        await service.RevokeInviteAsync(1, created.Invite.BudgetInviteId);

        var resent = await service.ResendInviteAsync(1, created.Invite.BudgetInviteId);

        Assert.NotEqual(created.PlainToken, resent.PlainToken);
        Assert.Equal(BudgetInviteAcceptStatus.Invalid, (await service.AcceptInviteAsync(created.PlainToken, 2)).Status);
        Assert.Equal(BudgetInviteAcceptStatus.Accepted, (await service.AcceptInviteAsync(resent.PlainToken, 2)).Status);
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

    private static BudgetInviteService CreateInviteService(ClintonFranklandDbContext db) =>
        new(db, new SharedBudgetDataService(db), TimeProvider.System);

    private static void SeedSharedBudget(
        ClintonFranklandDbContext db,
        BudgetMemberRole memberRole,
        bool includeMember = true)
    {
        var now = DateTime.UtcNow;
        db.Users.AddRange(
            User(1, "owner", "Owner", "owner@example.com"),
            User(2, "member", "Member", "member@example.com"),
            User(3, "outsider", "Outsider", "outsider@example.com"));
        db.SharedBudgets.Add(new SharedBudget
        {
            SharedBudgetId = 1,
            Name = "Household",
            OwnerUserId = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        db.BudgetMembers.Add(new BudgetMember
            {
                BudgetMemberId = 1,
                SharedBudgetId = 1,
                UserId = 1,
                Role = BudgetMemberRole.Owner,
                Status = BudgetMemberStatus.Active,
                CreatedAtUtc = now
            });

        if (includeMember)
        {
            db.BudgetMembers.Add(new BudgetMember
            {
                BudgetMemberId = 2,
                SharedBudgetId = 1,
                UserId = 2,
                Role = memberRole,
                Status = BudgetMemberStatus.Active,
                CreatedAtUtc = now
            });
        }

        db.Accounts.AddRange(
            new Account
            {
                AccountId = 1,
                AccountName = "House Checking",
                AccountTypeId = 1,
                BeginningBalance = 100m,
                Balance = 100m,
                ClearedBalance = 100m,
                IsDefault = true,
                UserId = 1,
                SharedBudgetId = 1
            },
            new Account
            {
                AccountId = 2,
                AccountName = "Owner Legacy",
                AccountTypeId = 1,
                BeginningBalance = 200m,
                Balance = 200m,
                ClearedBalance = 200m,
                IsDefault = false,
                UserId = 1,
                SharedBudgetId = null
            });
        db.Categories.Add(new Category { CategoryId = 1, CategoryName = "Utilities", UserId = 1, SharedBudgetId = 1 });
        db.Payees.Add(new Payee { PayeeId = 1, PayeeName = "Power", UserId = 1, IsDeleted = false });
        db.Frequencies.Add(new Frequency { FrequencyId = 4, FrequencyName = "Monthly", Sort = 1 });
        db.Transactions.Add(new Transaction
        {
            TransactionId = 1,
            TransactionDate = new DateOnly(2026, 7, 4),
            Amount = -25m,
            PayeeId = 1,
            CategoryId = 1,
            AccountId = 1,
            Cleared = false,
            UserId = 1,
            SharedBudgetId = 1
        });
        db.Budgets.Add(new Budget
        {
            BudgetId = 1,
            BudgetName = "Shared Bill",
            BudgetTypeId = 1,
            FrequencyId = 4,
            NextDueDate = new DateTime(2026, 7, 5),
            EndDate = new DateTime(1970, 1, 1),
            Amount = 30m,
            CategoryId = 1,
            UserId = 1,
            SharedBudgetId = 1,
            IsBill = true,
            PayeeId = 1
        });
    }

    private static User User(int id, string userName, string displayName, string? email = null) => new()
    {
        UserId = id,
        SiteId = 1,
        UserName = userName,
        DisplayName = displayName,
        EmailAddress = email,
        IsAdmin = false,
        Salt = "salt",
        PasswordHash = "hash",
        IsDeleted = false,
        FirstLogin = DateTime.UtcNow,
        LastLogin = DateTime.UtcNow
    };
}
