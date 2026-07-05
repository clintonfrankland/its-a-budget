using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public class SharedBudgetDataService
{
    private readonly ClintonFranklandDbContext _db;
    private static readonly BudgetMemberRole[] FinancialManagerRoles =
    [
        BudgetMemberRole.Owner,
        BudgetMemberRole.Admin,
        BudgetMemberRole.Editor
    ];

    public SharedBudgetDataService(ClintonFranklandDbContext db)
    {
        _db = db;
    }

    public Task<List<int>> GetReadableSharedBudgetIdsAsync(int userId) =>
        GetActiveMemberships(userId)
            .Select(m => m.SharedBudgetId)
            .ToListAsync();

    public Task<List<int>> GetFinancialManagerSharedBudgetIdsAsync(int userId) =>
        GetActiveMemberships(userId)
            .Where(m => FinancialManagerRoles.Contains(m.Role))
            .Select(m => m.SharedBudgetId)
            .ToListAsync();

    public Task<BudgetMemberRole?> GetMemberRoleAsync(int userId, int sharedBudgetId) =>
        GetActiveMemberships(userId)
            .Where(m => m.SharedBudgetId == sharedBudgetId)
            .Select(m => (BudgetMemberRole?)m.Role)
            .FirstOrDefaultAsync();

    public async Task<bool> CanReadFinancialDataAsync(int userId, int? sharedBudgetId, int? ownerUserId)
    {
        if (userId <= 0)
            return false;

        if (sharedBudgetId.HasValue)
            return await GetMemberRoleAsync(userId, sharedBudgetId.Value) is not null;

        return ownerUserId == userId;
    }

    public async Task<bool> CanManageFinancialDataAsync(int userId, int? sharedBudgetId, int? ownerUserId)
    {
        if (userId <= 0)
            return false;

        if (sharedBudgetId.HasValue)
        {
            var role = await GetMemberRoleAsync(userId, sharedBudgetId.Value);
            return role is BudgetMemberRole.Owner or BudgetMemberRole.Admin or BudgetMemberRole.Editor;
        }

        return ownerUserId == userId;
    }

    public async Task<bool> CanManageMembersAsync(int userId, int sharedBudgetId)
    {
        var role = await GetMemberRoleAsync(userId, sharedBudgetId);
        return role is BudgetMemberRole.Owner or BudgetMemberRole.Admin;
    }

    public async Task<bool> CanPerformOwnerActionAsync(int userId, int sharedBudgetId)
    {
        var role = await GetMemberRoleAsync(userId, sharedBudgetId);
        return role is BudgetMemberRole.Owner;
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

    private IQueryable<BudgetMember> GetActiveMemberships(int userId) =>
        _db.BudgetMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.Status == BudgetMemberStatus.Active);

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
