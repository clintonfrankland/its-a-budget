using System.Security.Cryptography;
using System.Text;
using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public enum BudgetInviteAcceptStatus
{
    Accepted,
    AlreadyAccepted,
    Expired,
    Revoked,
    Invalid,
    AuthenticationRequired,
    WrongUser
}

public sealed record BudgetInviteCreateResult(BudgetInvite Invite, string PlainToken);

public sealed record BudgetInvitePreview(
    BudgetInviteAcceptStatus Status,
    int? SharedBudgetId = null,
    string? SharedBudgetName = null,
    string? InvitedByDisplayName = null,
    BudgetMemberRole? Role = null,
    string? Target = null);

public sealed record BudgetInviteAcceptResult(BudgetInviteAcceptStatus Status, int? BudgetMemberId = null);

public sealed record SharedBudgetInviteSummary(
    int BudgetInviteId,
    int SharedBudgetId,
    string SharedBudgetName,
    string InvitedByDisplayName,
    string Target,
    BudgetMemberRole Role,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? AcceptedAtUtc,
    DateTime? RevokedAtUtc,
    int? AcceptedByUserId);

public class BudgetInviteService
{
    public const int DefaultExpirationDays = 14;

    private readonly ClintonFranklandDbContext _db;
    private readonly SharedBudgetDataService _sharedBudgets;
    private readonly TimeProvider _timeProvider;

    public BudgetInviteService(
        ClintonFranklandDbContext db,
        SharedBudgetDataService sharedBudgets,
        TimeProvider timeProvider)
    {
        _db = db;
        _sharedBudgets = sharedBudgets;
        _timeProvider = timeProvider;
    }

    public async Task<List<SharedBudget>> GetManageableSharedBudgetsAsync(int userId)
    {
        if (userId <= 0)
            return [];

        return await _db.BudgetMembers
            .AsNoTracking()
            .Where(m =>
                m.UserId == userId &&
                m.Status == BudgetMemberStatus.Active &&
                (m.Role == BudgetMemberRole.Owner || m.Role == BudgetMemberRole.Admin))
            .Select(m => m.SharedBudget!)
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    public async Task<List<BudgetMember>> GetMembersAsync(int requesterUserId, int sharedBudgetId)
    {
        if (!await _sharedBudgets.CanManageMembersAsync(requesterUserId, sharedBudgetId))
            return [];

        return await _db.BudgetMembers
            .AsNoTracking()
            .Include(m => m.User)
            .Where(m => m.SharedBudgetId == sharedBudgetId)
            .OrderBy(m => m.Status)
            .ThenBy(m => m.User!.DisplayName)
            .ToListAsync();
    }

    public async Task<List<SharedBudgetInviteSummary>> GetInvitesAsync(int requesterUserId, int sharedBudgetId)
    {
        if (!await _sharedBudgets.CanManageMembersAsync(requesterUserId, sharedBudgetId))
            return [];

        return await _db.BudgetInvites
            .AsNoTracking()
            .Include(i => i.SharedBudget)
            .Include(i => i.InvitedByUser)
            .Where(i => i.SharedBudgetId == sharedBudgetId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new SharedBudgetInviteSummary(
                i.BudgetInviteId,
                i.SharedBudgetId,
                i.SharedBudget!.Name,
                i.InvitedByUser == null ? "Unknown user" : i.InvitedByUser.DisplayName,
                i.InviteeUserName ?? i.InviteeEmail ?? "Open invite",
                i.Role,
                i.CreatedAtUtc,
                i.ExpiresAtUtc,
                i.AcceptedAtUtc,
                i.RevokedAtUtc,
                i.AcceptedByUserId))
            .ToListAsync();
    }

    public async Task<BudgetInviteCreateResult> CreateInviteAsync(
        int requesterUserId,
        int sharedBudgetId,
        string target,
        BudgetMemberRole role,
        int expirationDays = DefaultExpirationDays)
    {
        if (!await _sharedBudgets.CanManageMembersAsync(requesterUserId, sharedBudgetId))
            throw new UnauthorizedAccessException("Only budget owners and admins can invite members.");

        if (role is not (BudgetMemberRole.Admin or BudgetMemberRole.Editor or BudgetMemberRole.Viewer))
            throw new InvalidOperationException("Invites may grant Viewer, Editor, or Admin access.");

        var normalizedTarget = (target ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedTarget))
            throw new InvalidOperationException("Enter an email address or existing username.");

        var now = GetUtcNow();
        var token = GenerateToken();
        var existingUser = await FindUserByUserNameAsync(normalizedTarget);
        var invite = new BudgetInvite
        {
            SharedBudgetId = sharedBudgetId,
            InvitedByUserId = requesterUserId,
            InviteTokenHash = HashToken(token),
            Role = role,
            InviteeEmail = existingUser?.EmailAddress ?? (LooksLikeEmail(normalizedTarget) ? normalizedTarget : null),
            InviteeUserName = existingUser?.UserName ?? (LooksLikeEmail(normalizedTarget) ? null : normalizedTarget),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(Math.Clamp(expirationDays, 1, 90))
        };

        _db.BudgetInvites.Add(invite);
        await _db.SaveChangesAsync();
        return new BudgetInviteCreateResult(invite, token);
    }

    public async Task<BudgetInviteCreateResult> ResendInviteAsync(int requesterUserId, int inviteId)
    {
        var invite = await _db.BudgetInvites.FirstOrDefaultAsync(i => i.BudgetInviteId == inviteId);
        if (invite is null)
            throw new InvalidOperationException("Invite was not found.");

        if (!await _sharedBudgets.CanManageMembersAsync(requesterUserId, invite.SharedBudgetId))
            throw new UnauthorizedAccessException("Only budget owners and admins can resend invites.");

        if (invite.AcceptedAtUtc.HasValue)
            throw new InvalidOperationException("Accepted invites cannot be resent.");

        var now = GetUtcNow();
        var token = GenerateToken();
        invite.InviteTokenHash = HashToken(token);
        invite.CreatedAtUtc = now;
        invite.ExpiresAtUtc = now.AddDays(DefaultExpirationDays);
        invite.AcceptedAtUtc = null;
        invite.AcceptedByUserId = null;
        invite.RevokedAtUtc = null;
        invite.RevokedByUserId = null;

        await _db.SaveChangesAsync();
        return new BudgetInviteCreateResult(invite, token);
    }

    public async Task<bool> RevokeInviteAsync(int requesterUserId, int inviteId)
    {
        var invite = await _db.BudgetInvites.FirstOrDefaultAsync(i => i.BudgetInviteId == inviteId);
        if (invite is null)
            return false;

        if (!await _sharedBudgets.CanManageMembersAsync(requesterUserId, invite.SharedBudgetId))
            throw new UnauthorizedAccessException("Only budget owners and admins can revoke invites.");

        if (invite.AcceptedAtUtc.HasValue)
            return false;

        invite.RevokedAtUtc ??= GetUtcNow();
        invite.RevokedByUserId ??= requesterUserId;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<BudgetInvitePreview> PreviewInviteAsync(string token)
    {
        var invite = await FindInviteByTokenAsync(token);
        if (invite is null)
            return new BudgetInvitePreview(BudgetInviteAcceptStatus.Invalid);

        var state = GetCurrentInviteState(invite);
        if (state != BudgetInviteAcceptStatus.Accepted)
            return new BudgetInvitePreview(state);

        return new BudgetInvitePreview(
            BudgetInviteAcceptStatus.Accepted,
            invite.SharedBudgetId,
            invite.SharedBudget?.Name,
            invite.InvitedByUser?.DisplayName,
            invite.Role,
            invite.InviteeUserName ?? invite.InviteeEmail);
    }

    public async Task<BudgetInviteAcceptResult> AcceptInviteAsync(string token, int currentUserId)
    {
        if (currentUserId <= 0)
            return new BudgetInviteAcceptResult(BudgetInviteAcceptStatus.AuthenticationRequired);

        var invite = await FindInviteByTokenAsync(token, tracking: true);
        if (invite is null)
            return new BudgetInviteAcceptResult(BudgetInviteAcceptStatus.Invalid);

        var state = GetCurrentInviteState(invite);
        if (state != BudgetInviteAcceptStatus.Accepted)
            return new BudgetInviteAcceptResult(state);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == currentUserId && !u.IsDeleted);
        if (user is null)
            return new BudgetInviteAcceptResult(BudgetInviteAcceptStatus.AuthenticationRequired);

        if (!InviteMatchesUser(invite, user))
            return new BudgetInviteAcceptResult(BudgetInviteAcceptStatus.WrongUser);

        var now = GetUtcNow();
        var member = await _db.BudgetMembers.FirstOrDefaultAsync(m =>
            m.SharedBudgetId == invite.SharedBudgetId &&
            m.UserId == currentUserId);

        if (member is null)
        {
            member = new BudgetMember
            {
                SharedBudgetId = invite.SharedBudgetId,
                UserId = currentUserId,
                Role = invite.Role,
                Status = BudgetMemberStatus.Active,
                CreatedAtUtc = now
            };
            _db.BudgetMembers.Add(member);
        }
        else if (member.Status == BudgetMemberStatus.Active)
        {
            invite.AcceptedAtUtc ??= now;
            invite.AcceptedByUserId ??= currentUserId;
            await _db.SaveChangesAsync();
            return new BudgetInviteAcceptResult(BudgetInviteAcceptStatus.AlreadyAccepted, member.BudgetMemberId);
        }
        else
        {
            member.Role = invite.Role;
            member.Status = BudgetMemberStatus.Active;
            member.RemovedAtUtc = null;
            member.RemovedByUserId = null;
        }

        invite.AcceptedAtUtc = now;
        invite.AcceptedByUserId = currentUserId;
        await _db.SaveChangesAsync();
        return new BudgetInviteAcceptResult(BudgetInviteAcceptStatus.Accepted, member.BudgetMemberId);
    }

    private async Task<BudgetInvite?> FindInviteByTokenAsync(string token, bool tracking = false)
    {
        var hash = HashToken(token);
        var query = _db.BudgetInvites
            .Include(i => i.SharedBudget)
            .Include(i => i.InvitedByUser)
            .Where(i => i.InviteTokenHash == hash);

        if (!tracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync();
    }

    private BudgetInviteAcceptStatus GetCurrentInviteState(BudgetInvite invite)
    {
        if (invite.AcceptedAtUtc.HasValue)
            return BudgetInviteAcceptStatus.AlreadyAccepted;

        if (invite.RevokedAtUtc.HasValue)
            return BudgetInviteAcceptStatus.Revoked;

        if (invite.ExpiresAtUtc <= GetUtcNow())
            return BudgetInviteAcceptStatus.Expired;

        return BudgetInviteAcceptStatus.Accepted;
    }

    private async Task<User?> FindUserByUserNameAsync(string userName) =>
        await _db.Users.FirstOrDefaultAsync(u =>
            !u.IsDeleted &&
            u.UserName.ToLower() == userName.Trim().ToLower());

    private static bool InviteMatchesUser(BudgetInvite invite, User user)
    {
        var hasUserNameTarget = !string.IsNullOrWhiteSpace(invite.InviteeUserName);
        var hasEmailTarget = !string.IsNullOrWhiteSpace(invite.InviteeEmail);

        if (!hasUserNameTarget && !hasEmailTarget)
            return true;

        if (hasUserNameTarget &&
            string.Equals(invite.InviteeUserName!.Trim(), user.UserName, StringComparison.OrdinalIgnoreCase))
            return true;

        return hasEmailTarget &&
            !string.IsNullOrWhiteSpace(user.EmailAddress) &&
            string.Equals(invite.InviteeEmail!.Trim(), user.EmailAddress.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private DateTime GetUtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static bool LooksLikeEmail(string value) => value.Contains('@') && value.Contains('.');

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string HashToken(string token)
    {
        var normalized = (token ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
