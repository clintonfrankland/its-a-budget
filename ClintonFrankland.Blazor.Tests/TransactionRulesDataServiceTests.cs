using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Models.ViewModels;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Blazor.Tests;

public class TransactionRulesDataServiceTests
{
    [Fact]
    public void Matches_UsesFirstRuleFiltersAndSignedAmount()
    {
        var debit = new TransactionRule { IsEnabled = true, ContainsText = "market", MinimumAmount = -100m, MaximumAmount = -1m, AccountId = 2 };
        Assert.True(TransactionRulesDataService.Matches(debit, 2, -25m, "Market", null));
        Assert.False(TransactionRulesDataService.Matches(debit, 2, 25m, "Market", null));
        Assert.False(TransactionRulesDataService.Matches(debit, 3, -25m, "Market", null));
    }

    [Fact]
    public async Task SuggestAsync_UsesPriorityAndNeverReadsAnotherUsersRules()
    {
        await using var db = Db();
        db.TransactionRules.AddRange(
            new TransactionRule { UserId = 1, ContainsText = "store", PayeeName = "First", Priority = 0, IsEnabled = true },
            new TransactionRule { UserId = 1, ContainsText = "store", PayeeName = "Second", Priority = 1, IsEnabled = true },
            new TransactionRule { UserId = 2, ContainsText = "store", PayeeName = "Other", Priority = 0, IsEnabled = true });
        await db.SaveChangesAsync();
        var service = new TransactionRulesDataService(db);
        Assert.Equal("First", (await service.SuggestAsync(1, null, -1m, "Store", null))!.PayeeName);
        Assert.Null(await service.SuggestAsync(3, null, -1m, "Store", null));
    }

    [Fact]
    public async Task PreviewAndApply_AreUserScoped()
    {
        await using var db = Db();
        db.Categories.AddRange(new Category { CategoryId = 1, CategoryName = "Old", UserId = 1 }, new Category { CategoryId = 2, CategoryName = "Food", UserId = 1 });
        db.Payees.AddRange(new Payee { PayeeId = 1, PayeeName = "Shop", UserId = 1 }, new Payee { PayeeId = 2, PayeeName = "Shop", UserId = 2 });
        db.TransactionRules.Add(new TransactionRule { UserId = 1, ContainsText = "shop", CategoryName = "Food", Priority = 0, IsEnabled = true });
        db.Transactions.AddRange(Tx(1, 1, 1), Tx(2, 2, 2)); await db.SaveChangesAsync();
        var service = new TransactionRulesDataService(db); var preview = await service.PreviewAsync(1);
        Assert.Single(preview); await service.ApplyAsync(1, preview.Select(x => x.TransactionId).ToArray());
        Assert.Equal(2, (await db.Transactions.FindAsync(1))!.CategoryId); Assert.Equal(1, (await db.Transactions.FindAsync(2))!.CategoryId);
    }
    private static ClintonFranklandDbContext Db() => new(new DbContextOptionsBuilder<ClintonFranklandDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Transaction Tx(int id, int user, int payee) => new() { TransactionId=id, UserId=user, PayeeId=payee, CategoryId=1, AccountId=1, Amount=-10m, TransactionDate=new DateOnly(2026,1,1) };
}
