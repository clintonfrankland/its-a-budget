using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public sealed record SharedBudgetMembershipSummary(
    int SharedBudgetId,
    string Name,
    BudgetMemberRole Role,
    int ActiveMemberCount,
    bool IsSoleOwner);

internal sealed record SharedBudgetMembershipSummaryRow(
    int SharedBudgetId,
    string? Name,
    BudgetMemberRole Role,
    int ActiveMemberCount);

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

    public async Task<List<int>> GetReadableSharedBudgetIdsAsync(int userId)
    {
        var activeSharedBudgetId = await GetActiveSharedBudgetIdAsync(userId);
        return activeSharedBudgetId.HasValue ? [activeSharedBudgetId.Value] : [];
    }

    public async Task<List<SharedBudgetMembershipSummary>> GetReadableSharedBudgetSummariesAsync(int userId)
    {
        if (userId <= 0)
            return [];

        var rows = await _db.BudgetMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.Status == BudgetMemberStatus.Active)
            .OrderBy(m => m.SharedBudget == null ? null : m.SharedBudget.Name)
            .ThenBy(m => m.SharedBudgetId)
            .Select(m => new SharedBudgetMembershipSummaryRow(
                m.SharedBudgetId,
                m.SharedBudget == null ? null : m.SharedBudget.Name,
                m.Role,
                _db.BudgetMembers.Count(active =>
                    active.SharedBudgetId == m.SharedBudgetId &&
                    active.Status == BudgetMemberStatus.Active)))
            .ToListAsync();

        return rows
            .Select(row => new SharedBudgetMembershipSummary(
                row.SharedBudgetId,
                string.IsNullOrWhiteSpace(row.Name) ? $"Budget {row.SharedBudgetId}" : row.Name,
                row.Role,
                row.ActiveMemberCount,
                row.Role == BudgetMemberRole.Owner && row.ActiveMemberCount == 1))
            .ToList();
    }

    public async Task<List<int>> GetFinancialManagerSharedBudgetIdsAsync(int userId)
    {
        var activeSharedBudgetId = await GetActiveSharedBudgetIdAsync(userId);
        if (!activeSharedBudgetId.HasValue)
            return [];

        return await GetActiveMemberships(userId)
            .Where(m =>
                m.SharedBudgetId == activeSharedBudgetId.Value &&
                FinancialManagerRoles.Contains(m.Role))
            .Select(m => m.SharedBudgetId)
            .ToListAsync();
    }

    public async Task<int?> GetActiveSharedBudgetIdAsync(int userId)
    {
        if (userId <= 0)
            return null;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId && !u.IsDeleted);
        if (user is null)
            return await GetActiveMemberships(userId)
                .OrderBy(m => m.SharedBudgetId)
                .Select(m => (int?)m.SharedBudgetId)
                .FirstOrDefaultAsync();

        var selectedIsReadable = user.ActiveSharedBudgetId.HasValue &&
            await GetActiveMemberships(userId)
                .AnyAsync(m => m.SharedBudgetId == user.ActiveSharedBudgetId.Value);
        if (selectedIsReadable)
            return user.ActiveSharedBudgetId;

        var fallback = await GetActiveMemberships(userId)
            .OrderBy(m => m.SharedBudgetId)
            .Select(m => (int?)m.SharedBudgetId)
            .FirstOrDefaultAsync();
        if (fallback.HasValue && user.ActiveSharedBudgetId != fallback)
        {
            user.ActiveSharedBudgetId = fallback;
            await _db.SaveChangesAsync();
        }

        return fallback;
    }

    public async Task<bool> SetActiveSharedBudgetAsync(int userId, int sharedBudgetId)
    {
        if (!await GetActiveMemberships(userId).AnyAsync(m => m.SharedBudgetId == sharedBudgetId))
            return false;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId && !u.IsDeleted);
        if (user is null)
            return false;

        if (user.ActiveSharedBudgetId == sharedBudgetId)
            return true;

        user.ActiveSharedBudgetId = sharedBudgetId;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<SharedBudgetMembershipSummary?> GetActiveSharedBudgetSummaryAsync(int userId)
    {
        var activeId = await GetActiveSharedBudgetIdAsync(userId);
        if (!activeId.HasValue)
            return null;

        return (await GetReadableSharedBudgetSummariesAsync(userId))
            .FirstOrDefault(b => b.SharedBudgetId == activeId.Value);
    }

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

    public async Task<bool> ChangeMemberRoleAsync(int requesterUserId, int budgetMemberId, BudgetMemberRole newRole)
    {
        if (newRole == BudgetMemberRole.Owner)
            throw new InvalidOperationException("Use ownership transfer to assign the Owner role.");

        if (newRole is not (BudgetMemberRole.Admin or BudgetMemberRole.Editor or BudgetMemberRole.Viewer))
            throw new InvalidOperationException("Members may be changed to Admin, Editor, or Viewer.");

        var member = await _db.BudgetMembers.FirstOrDefaultAsync(m => m.BudgetMemberId == budgetMemberId);
        if (member is null || member.Status != BudgetMemberStatus.Active)
            return false;

        if (member.UserId == requesterUserId)
            throw new InvalidOperationException("Use leave budget or ownership transfer for your own membership.");

        if (!await CanManageMembersAsync(requesterUserId, member.SharedBudgetId))
            throw new UnauthorizedAccessException("Only budget owners and admins can change member roles.");

        if (member.Role == BudgetMemberRole.Owner)
            throw new InvalidOperationException("Transfer ownership before changing the current owner.");

        member.Role = newRole;
        await TouchSharedBudgetAsync(member.SharedBudgetId);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveMemberAsync(int requesterUserId, int budgetMemberId)
    {
        var member = await _db.BudgetMembers.FirstOrDefaultAsync(m => m.BudgetMemberId == budgetMemberId);
        if (member is null || member.Status != BudgetMemberStatus.Active)
            return false;

        if (member.UserId == requesterUserId)
            return await LeaveSharedBudgetAsync(requesterUserId, member.SharedBudgetId);

        if (!await CanManageMembersAsync(requesterUserId, member.SharedBudgetId))
            throw new UnauthorizedAccessException("Only budget owners and admins can remove members.");

        if (member.Role == BudgetMemberRole.Owner)
            throw new InvalidOperationException("Transfer ownership before removing the current owner.");

        MarkRemoved(member, requesterUserId);
        await TouchSharedBudgetAsync(member.SharedBudgetId);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> LeaveSharedBudgetAsync(int requesterUserId, int sharedBudgetId)
    {
        var member = await _db.BudgetMembers.FirstOrDefaultAsync(m =>
            m.UserId == requesterUserId &&
            m.SharedBudgetId == sharedBudgetId &&
            m.Status == BudgetMemberStatus.Active);

        if (member is null)
            return false;

        if (member.Role == BudgetMemberRole.Owner)
            throw new InvalidOperationException("Transfer ownership before leaving this budget.");

        MarkRemoved(member, requesterUserId);
        await TouchSharedBudgetAsync(sharedBudgetId);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> TransferOwnershipAsync(int requesterUserId, int sharedBudgetId, int newOwnerMemberId)
    {
        if (!await CanPerformOwnerActionAsync(requesterUserId, sharedBudgetId))
            throw new UnauthorizedAccessException("Only the current owner can transfer ownership.");

        var currentOwner = await _db.BudgetMembers.FirstOrDefaultAsync(m =>
            m.SharedBudgetId == sharedBudgetId &&
            m.UserId == requesterUserId &&
            m.Status == BudgetMemberStatus.Active &&
            m.Role == BudgetMemberRole.Owner);
        var newOwner = await _db.BudgetMembers.FirstOrDefaultAsync(m =>
            m.BudgetMemberId == newOwnerMemberId &&
            m.SharedBudgetId == sharedBudgetId &&
            m.Status == BudgetMemberStatus.Active);
        var sharedBudget = await _db.SharedBudgets.FirstOrDefaultAsync(b => b.SharedBudgetId == sharedBudgetId);

        if (currentOwner is null || newOwner is null || sharedBudget is null)
            return false;

        if (newOwner.UserId == requesterUserId)
            throw new InvalidOperationException("Select another active member to receive ownership.");

        currentOwner.Role = BudgetMemberRole.Admin;
        newOwner.Role = BudgetMemberRole.Owner;
        sharedBudget.OwnerUserId = newOwner.UserId;
        sharedBudget.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int?> GetDefaultSharedBudgetIdAsync(int userId)
    {
        if (userId <= 0)
            return null;

        var existing = await GetActiveSharedBudgetIdAsync(userId);

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
        user.ActiveSharedBudgetId = sharedBudget.SharedBudgetId;
        await _db.SaveChangesAsync();
        return sharedBudget.SharedBudgetId;
    }

    private IQueryable<BudgetMember> GetActiveMemberships(int userId) =>
        _db.BudgetMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.Status == BudgetMemberStatus.Active);

    private async Task TouchSharedBudgetAsync(int sharedBudgetId)
    {
        var sharedBudget = await _db.SharedBudgets.FirstOrDefaultAsync(b => b.SharedBudgetId == sharedBudgetId);
        if (sharedBudget is not null)
            sharedBudget.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void MarkRemoved(BudgetMember member, int removedByUserId)
    {
        member.Status = BudgetMemberStatus.Removed;
        member.RemovedAtUtc = DateTime.UtcNow;
        member.RemovedByUserId = removedByUserId;
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
