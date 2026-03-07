using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Components.Pages;

public partial class Users
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private ClintonFrankland.Data.ClintonFranklandDbContext DbContext { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private List<User> UsersList { get; set; } = [];
    private string? ErrorMessage { get; set; }

    private User? EditingUser { get; set; }
    private bool IsNewUser { get; set; }
    private string FormDisplayName { get; set; } = "";
    private string FormUserName { get; set; } = "";
    private string? FormEmail { get; set; }
    private bool FormIsAdmin { get; set; }

    private User? PasswordUser { get; set; }
    private string PasswordValue { get; set; } = "";

    // Screen size tracking for responsive column visibility
    private ScreenSize currentScreenSize = ScreenSize.Large;

    // Screen size enum matching Bootstrap breakpoints
    public enum ScreenSize
    {
        ExtraSmall,  // < 576px
        Small,       // >= 576px
        Medium,      // >= 768px
        Large,       // >= 992px
        ExtraLarge,  // >= 1200px
        ExtraExtraLarge // >= 1400px
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await AuthService.InitializeAsync();
        if (!AuthService.IsAuthenticated)
        {
            Navigation.NavigateTo($"/login?Return={Uri.EscapeDataString("/users")}");
            return;
        }

        await LoadUsers();
        StateHasChanged();
    }

    private async Task LoadUsers()
    {
        var current = AuthService.CurrentUser;

        if (current.IsAdmin)
        {
            UsersList = await DbContext.Users.Where(u => !u.IsDeleted).OrderBy(u => u.UserId).ToListAsync();
        }
        else
        {
            UsersList = await DbContext.Users.Where(u => !u.IsDeleted && u.UserId == current.UserId).ToListAsync();
        }
    }

    private void ShowNewUser()
    {
        if (!AuthService.CurrentUser.IsAdmin)
        {
            ErrorMessage = "Only admins can create users.";
            return;
        }

        IsNewUser = true;
        EditingUser = new User();
        FormDisplayName = "";
        FormUserName = "";
        FormEmail = "";
        FormIsAdmin = false;
    }

    private void EditUser(User user)
    {
        var current = AuthService.CurrentUser;
        if (!current.IsAdmin && current.UserId != user.UserId)
        {
            ErrorMessage = "You can only edit your own user profile.";
            return;
        }

        IsNewUser = false;
        EditingUser = user;
        FormDisplayName = user.DisplayName;
        FormUserName = user.UserName;
        FormEmail = user.EmailAddress;
        FormIsAdmin = user.IsAdmin;
    }

    private async Task SaveUser()
    {
        if (EditingUser is null) return;
        var current = AuthService.CurrentUser;

        if (!current.IsAdmin && !IsNewUser && current.UserId != EditingUser.UserId)
        {
            ErrorMessage = "You can only edit your own user profile.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormDisplayName) || string.IsNullOrWhiteSpace(FormUserName))
        {
            ErrorMessage = "Full name and username are required.";
            return;
        }

        if (IsNewUser)
        {
            var nextId = await DbContext.Users.Select(u => (int?)u.UserId).MaxAsync() ?? 0;
            var salt = PasswordUtility.CreateSalt();
            var tempPassword = PasswordUtility.GenerateStrongPassword();

            var user = new User
            {
                UserId = nextId + 1,
                SiteId = current.SiteId == 0 ? 1 : current.SiteId,
                DisplayName = FormDisplayName.Trim(),
                UserName = FormUserName.Trim(),
                EmailAddress = string.IsNullOrWhiteSpace(FormEmail) ? null : FormEmail.Trim(),
                IsAdmin = current.IsAdmin && FormIsAdmin,
                IsDeleted = false,
                Salt = salt,
                PasswordHash = PasswordUtility.HashPassword(tempPassword, salt),
                FirstLogin = DateTime.UtcNow,
                LastLogin = DateTime.UtcNow
            };

            DbContext.Users.Add(user);
        }
        else
        {
            var dbUser = await DbContext.Users.FirstAsync(u => u.UserId == EditingUser.UserId);
            dbUser.DisplayName = FormDisplayName.Trim();
            dbUser.UserName = FormUserName.Trim();
            dbUser.EmailAddress = string.IsNullOrWhiteSpace(FormEmail) ? null : FormEmail.Trim();

            if (current.IsAdmin)
            {
                dbUser.IsAdmin = FormIsAdmin;
            }
        }

        await DbContext.SaveChangesAsync();
        CloseEditModal();
        await LoadUsers();
        ErrorMessage = null;
    }

    private void OpenPasswordUtility(User user)
    {
        var current = AuthService.CurrentUser;
        if (!current.IsAdmin && current.UserId != user.UserId)
        {
            ErrorMessage = "You can only reset your own password.";
            return;
        }

        PasswordUser = user;
        PasswordValue = "";
    }

    private void GeneratePassword()
    {
        PasswordValue = PasswordUtility.GenerateStrongPassword();
    }

    private async Task ResetPassword()
    {
        if (PasswordUser is null || string.IsNullOrWhiteSpace(PasswordValue))
        {
            ErrorMessage = "Enter or generate a password first.";
            return;
        }

        var current = AuthService.CurrentUser;
        if (!current.IsAdmin && current.UserId != PasswordUser.UserId)
        {
            ErrorMessage = "You can only reset your own password.";
            return;
        }

        var dbUser = await DbContext.Users.FirstAsync(u => u.UserId == PasswordUser.UserId);
        dbUser.Salt = PasswordUtility.CreateSalt();
        dbUser.PasswordHash = PasswordUtility.HashPassword(PasswordValue, dbUser.Salt);
        dbUser.PasswordResetRequestOn = DateTime.UtcNow;

        await DbContext.SaveChangesAsync();
        ClosePasswordModal();
        ErrorMessage = null;
    }

    private void CloseEditModal()
    {
        EditingUser = null;
        IsNewUser = false;
    }

    private void ClosePasswordModal()
    {
        PasswordUser = null;
        PasswordValue = "";
    }

    // Called by RadzenMediaQuery components when breakpoints change
    void OnScreenSizeChange(ScreenSize size, bool matches)
    {
        if (matches)
        {
            currentScreenSize = size;
            StateHasChanged();
        }
    }

    // Helper methods to check screen size for column visibility
    bool IsAtLeast(ScreenSize minimumSize) => currentScreenSize >= minimumSize;
    bool IsAtMost(ScreenSize maximumSize) => currentScreenSize <= maximumSize;
    bool IsBetween(ScreenSize minSize, ScreenSize maxSize) => currentScreenSize >= minSize && currentScreenSize <= maxSize;
    bool IsExactly(ScreenSize size) => currentScreenSize == size;
}
