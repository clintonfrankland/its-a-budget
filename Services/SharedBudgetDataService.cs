using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public class SharedBudgetDataService
{
    private readonly ClintonFranklandDbContext _db;

    public SharedBudgetDataService(ClintonFranklandDbContext db)
    {
        _db = db;
    }

    public async Task<int?> GetDefaultSharedBudgetIdAsync(int userId)
    {
        if (userId <= 0)
            return null;

        var existing = await _db.BudgetMembers
            .AsNoTracking()
            .Where(m =>
                m.UserId == userId &&
                m.Status == BudgetMemberStatus.Active &&
                m.Role == BudgetMemberRole.Owner)
            .OrderBy(m => m.SharedBudgetId)
            .Select(m => (int?)m.SharedBudgetId)
            .FirstOrDefaultAsync();

        if (existing.HasValue)
            return existing.Value;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId && !u.IsDeleted);
        if (user is null)
            return null;

        var now = DateTime.UtcNow;
        var sharedBudget = new SharedBudget
        {
            Name = BuildDefaultName(user),
            OwnerUserId = user.UserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _db.SharedBudgets.Add(sharedBudget);
        await _db.SaveChangesAsync();

        _db.BudgetMembers.Add(new BudgetMember
        {
            SharedBudgetId = sharedBudget.SharedBudgetId,
            UserId = user.UserId,
            Role = BudgetMemberRole.Owner,
            Status = BudgetMemberStatus.Active,
            CreatedAtUtc = now
        });

        await _db.SaveChangesAsync();
        return sharedBudget.SharedBudgetId;
    }

    private static string BuildDefaultName(User user)
    {
        var baseName = !string.IsNullOrWhiteSpace(user.DisplayName)
            ? user.DisplayName.Trim()
            : user.UserName.Trim();

        return string.IsNullOrWhiteSpace(baseName)
            ? $"Budget {user.UserId}"
            : $"{baseName} Budget";
    }
}
