using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedBudgetMembershipModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.cfSharedBudgets', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.cfSharedBudgets (
        SharedBudgetId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_cfSharedBudgets PRIMARY KEY,
        Name NVARCHAR(128) NOT NULL,
        OwnerUserId INT NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_cfSharedBudgets_CreatedAtUtc DEFAULT(SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_cfSharedBudgets_UpdatedAtUtc DEFAULT(SYSUTCDATETIME())
    );
END

IF OBJECT_ID('dbo.cfBudgetMembers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.cfBudgetMembers (
        BudgetMemberId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_cfBudgetMembers PRIMARY KEY,
        SharedBudgetId INT NOT NULL,
        UserId INT NOT NULL,
        Role NVARCHAR(16) NOT NULL,
        Status NVARCHAR(16) NOT NULL CONSTRAINT DF_cfBudgetMembers_Status DEFAULT('Active'),
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_cfBudgetMembers_CreatedAtUtc DEFAULT(SYSUTCDATETIME()),
        RemovedAtUtc DATETIME2 NULL,
        RemovedByUserId INT NULL
    );
END

IF OBJECT_ID('dbo.cfBudgetInvites', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.cfBudgetInvites (
        BudgetInviteId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_cfBudgetInvites PRIMARY KEY,
        SharedBudgetId INT NOT NULL,
        InvitedByUserId INT NOT NULL,
        InviteTokenHash NVARCHAR(128) NOT NULL,
        Role NVARCHAR(16) NOT NULL,
        InviteeEmail NVARCHAR(256) NULL,
        ExpiresAtUtc DATETIME2 NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_cfBudgetInvites_CreatedAtUtc DEFAULT(SYSUTCDATETIME()),
        AcceptedAtUtc DATETIME2 NULL,
        AcceptedByUserId INT NULL,
        RevokedAtUtc DATETIME2 NULL,
        RevokedByUserId INT NULL
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.cfAccounts', 'U') IS NOT NULL AND COL_LENGTH('dbo.cfAccounts', 'SharedBudgetId') IS NULL
    ALTER TABLE dbo.cfAccounts ADD SharedBudgetId INT NULL;

IF OBJECT_ID('dbo.cfTransactions', 'U') IS NOT NULL AND COL_LENGTH('dbo.cfTransactions', 'SharedBudgetId') IS NULL
    ALTER TABLE dbo.cfTransactions ADD SharedBudgetId INT NULL;

IF OBJECT_ID('dbo.cfCategories', 'U') IS NOT NULL AND COL_LENGTH('dbo.cfCategories', 'SharedBudgetId') IS NULL
    ALTER TABLE dbo.cfCategories ADD SharedBudgetId INT NULL;

IF OBJECT_ID('dbo.cfBudgets', 'U') IS NOT NULL AND COL_LENGTH('dbo.cfBudgets', 'SharedBudgetId') IS NULL
    ALTER TABLE dbo.cfBudgets ADD SharedBudgetId INT NULL;

IF OBJECT_ID('dbo.cfCategoryBudgetTargets', 'U') IS NOT NULL AND COL_LENGTH('dbo.cfCategoryBudgetTargets', 'SharedBudgetId') IS NULL
    ALTER TABLE dbo.cfCategoryBudgetTargets ADD SharedBudgetId INT NULL;

IF OBJECT_ID('dbo.cfNotificationSendLog', 'U') IS NOT NULL AND COL_LENGTH('dbo.cfNotificationSendLog', 'SharedBudgetId') IS NULL
    ALTER TABLE dbo.cfNotificationSendLog ADD SharedBudgetId INT NULL;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.cfUsers', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.cfSharedBudgets (Name, OwnerUserId, CreatedAtUtc, UpdatedAtUtc)
    SELECT
        LEFT(COALESCE(NULLIF(LTRIM(RTRIM(u.DisplayName)), ''), NULLIF(LTRIM(RTRIM(u.UserName)), ''), CONCAT('Budget ', u.UserId)) + ' Budget', 128),
        u.UserId,
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    FROM dbo.cfUsers u
    WHERE ISNULL(u.IsDeleted, 0) = 0
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.cfBudgetMembers bm
          WHERE bm.UserId = u.UserId
            AND bm.Role = 'Owner'
            AND bm.Status = 'Active'
      );

    INSERT INTO dbo.cfBudgetMembers (SharedBudgetId, UserId, Role, Status, CreatedAtUtc)
    SELECT sb.SharedBudgetId, sb.OwnerUserId, 'Owner', 'Active', SYSUTCDATETIME()
    FROM dbo.cfSharedBudgets sb
    WHERE NOT EXISTS (
        SELECT 1
        FROM dbo.cfBudgetMembers bm
        WHERE bm.SharedBudgetId = sb.SharedBudgetId
          AND bm.UserId = sb.OwnerUserId
    );

    IF OBJECT_ID('dbo.cfAccounts', 'U') IS NOT NULL
    BEGIN
        UPDATE a
        SET SharedBudgetId = sb.SharedBudgetId
        FROM dbo.cfAccounts a
        INNER JOIN dbo.cfBudgetMembers bm ON bm.UserId = a.UserId AND bm.Role = 'Owner' AND bm.Status = 'Active'
        INNER JOIN dbo.cfSharedBudgets sb ON sb.SharedBudgetId = bm.SharedBudgetId
        WHERE a.SharedBudgetId IS NULL AND a.UserId IS NOT NULL;
    END

    IF OBJECT_ID('dbo.cfTransactions', 'U') IS NOT NULL
    BEGIN
        UPDATE t
        SET SharedBudgetId = sb.SharedBudgetId
        FROM dbo.cfTransactions t
        INNER JOIN dbo.cfBudgetMembers bm ON bm.UserId = t.UserId AND bm.Role = 'Owner' AND bm.Status = 'Active'
        INNER JOIN dbo.cfSharedBudgets sb ON sb.SharedBudgetId = bm.SharedBudgetId
        WHERE t.SharedBudgetId IS NULL AND t.UserId IS NOT NULL;
    END

    IF OBJECT_ID('dbo.cfCategories', 'U') IS NOT NULL
    BEGIN
        UPDATE c
        SET SharedBudgetId = sb.SharedBudgetId
        FROM dbo.cfCategories c
        INNER JOIN dbo.cfBudgetMembers bm ON bm.UserId = c.UserId AND bm.Role = 'Owner' AND bm.Status = 'Active'
        INNER JOIN dbo.cfSharedBudgets sb ON sb.SharedBudgetId = bm.SharedBudgetId
        WHERE c.SharedBudgetId IS NULL AND c.UserId IS NOT NULL;
    END

    IF OBJECT_ID('dbo.cfBudgets', 'U') IS NOT NULL
    BEGIN
        UPDATE b
        SET SharedBudgetId = sb.SharedBudgetId
        FROM dbo.cfBudgets b
        INNER JOIN dbo.cfBudgetMembers bm ON bm.UserId = b.UserId AND bm.Role = 'Owner' AND bm.Status = 'Active'
        INNER JOIN dbo.cfSharedBudgets sb ON sb.SharedBudgetId = bm.SharedBudgetId
        WHERE b.SharedBudgetId IS NULL AND b.UserId IS NOT NULL;
    END

    IF OBJECT_ID('dbo.cfCategoryBudgetTargets', 'U') IS NOT NULL
    BEGIN
        UPDATE t
        SET SharedBudgetId = sb.SharedBudgetId
        FROM dbo.cfCategoryBudgetTargets t
        INNER JOIN dbo.cfBudgetMembers bm ON bm.UserId = t.UserId AND bm.Role = 'Owner' AND bm.Status = 'Active'
        INNER JOIN dbo.cfSharedBudgets sb ON sb.SharedBudgetId = bm.SharedBudgetId
        WHERE t.SharedBudgetId IS NULL;
    END

    IF OBJECT_ID('dbo.cfNotificationSendLog', 'U') IS NOT NULL
    BEGIN
        UPDATE l
        SET SharedBudgetId = sb.SharedBudgetId
        FROM dbo.cfNotificationSendLog l
        INNER JOIN dbo.cfBudgetMembers bm ON bm.UserId = l.UserId AND bm.Role = 'Owner' AND bm.Status = 'Active'
        INNER JOIN dbo.cfSharedBudgets sb ON sb.SharedBudgetId = bm.SharedBudgetId
        WHERE l.SharedBudgetId IS NULL;
    END
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_cfBudgetMembers_Role')
    ALTER TABLE dbo.cfBudgetMembers ADD CONSTRAINT CK_cfBudgetMembers_Role CHECK (Role IN ('Owner', 'Admin', 'Editor', 'Viewer'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_cfBudgetMembers_Status')
    ALTER TABLE dbo.cfBudgetMembers ADD CONSTRAINT CK_cfBudgetMembers_Status CHECK (Status IN ('Active', 'Removed'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_cfBudgetInvites_Role')
    ALTER TABLE dbo.cfBudgetInvites ADD CONSTRAINT CK_cfBudgetInvites_Role CHECK (Role IN ('Owner', 'Admin', 'Editor', 'Viewer'));

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfSharedBudgets_OwnerUserId' AND object_id = OBJECT_ID('dbo.cfSharedBudgets'))
    CREATE INDEX IX_cfSharedBudgets_OwnerUserId ON dbo.cfSharedBudgets(OwnerUserId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfBudgetMembers_UserId' AND object_id = OBJECT_ID('dbo.cfBudgetMembers'))
    CREATE INDEX IX_cfBudgetMembers_UserId ON dbo.cfBudgetMembers(UserId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfBudgetMembers_SharedBudgetId_UserId' AND object_id = OBJECT_ID('dbo.cfBudgetMembers'))
    CREATE UNIQUE INDEX IX_cfBudgetMembers_SharedBudgetId_UserId ON dbo.cfBudgetMembers(SharedBudgetId, UserId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfBudgetInvites_InvitedByUserId' AND object_id = OBJECT_ID('dbo.cfBudgetInvites'))
    CREATE INDEX IX_cfBudgetInvites_InvitedByUserId ON dbo.cfBudgetInvites(InvitedByUserId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfBudgetInvites_InviteTokenHash' AND object_id = OBJECT_ID('dbo.cfBudgetInvites'))
    CREATE UNIQUE INDEX IX_cfBudgetInvites_InviteTokenHash ON dbo.cfBudgetInvites(InviteTokenHash);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfBudgetInvites_SharedBudgetId_ExpiresAtUtc' AND object_id = OBJECT_ID('dbo.cfBudgetInvites'))
    CREATE INDEX IX_cfBudgetInvites_SharedBudgetId_ExpiresAtUtc ON dbo.cfBudgetInvites(SharedBudgetId, ExpiresAtUtc);

IF OBJECT_ID('dbo.cfAccounts', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfAccounts_SharedBudgetId' AND object_id = OBJECT_ID('dbo.cfAccounts'))
    CREATE INDEX IX_cfAccounts_SharedBudgetId ON dbo.cfAccounts(SharedBudgetId);

IF OBJECT_ID('dbo.cfTransactions', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfTransactions_SharedBudgetId' AND object_id = OBJECT_ID('dbo.cfTransactions'))
    CREATE INDEX IX_cfTransactions_SharedBudgetId ON dbo.cfTransactions(SharedBudgetId);

IF OBJECT_ID('dbo.cfCategories', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfCategories_SharedBudgetId' AND object_id = OBJECT_ID('dbo.cfCategories'))
    CREATE INDEX IX_cfCategories_SharedBudgetId ON dbo.cfCategories(SharedBudgetId);

IF OBJECT_ID('dbo.cfBudgets', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfBudgets_SharedBudgetId' AND object_id = OBJECT_ID('dbo.cfBudgets'))
    CREATE INDEX IX_cfBudgets_SharedBudgetId ON dbo.cfBudgets(SharedBudgetId);

IF OBJECT_ID('dbo.cfCategoryBudgetTargets', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfCategoryBudgetTargets_SharedBudgetId_CategoryId_BudgetMonth' AND object_id = OBJECT_ID('dbo.cfCategoryBudgetTargets'))
    CREATE UNIQUE INDEX IX_cfCategoryBudgetTargets_SharedBudgetId_CategoryId_BudgetMonth ON dbo.cfCategoryBudgetTargets(SharedBudgetId, CategoryId, BudgetMonth) WHERE SharedBudgetId IS NOT NULL;

IF OBJECT_ID('dbo.cfNotificationSendLog', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfNotificationSendLog_SharedBudgetId' AND object_id = OBJECT_ID('dbo.cfNotificationSendLog'))
    CREATE INDEX IX_cfNotificationSendLog_SharedBudgetId ON dbo.cfNotificationSendLog(SharedBudgetId);

IF OBJECT_ID('dbo.cfUsers', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_cfSharedBudgets_cfUsers_OwnerUserId')
    ALTER TABLE dbo.cfSharedBudgets ADD CONSTRAINT FK_cfSharedBudgets_cfUsers_OwnerUserId FOREIGN KEY (OwnerUserId) REFERENCES dbo.cfUsers(UserId);

IF OBJECT_ID('dbo.cfUsers', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_cfBudgetMembers_cfUsers_UserId')
    ALTER TABLE dbo.cfBudgetMembers ADD CONSTRAINT FK_cfBudgetMembers_cfUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.cfUsers(UserId);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_cfBudgetMembers_cfSharedBudgets_SharedBudgetId')
    ALTER TABLE dbo.cfBudgetMembers ADD CONSTRAINT FK_cfBudgetMembers_cfSharedBudgets_SharedBudgetId FOREIGN KEY (SharedBudgetId) REFERENCES dbo.cfSharedBudgets(SharedBudgetId);

IF OBJECT_ID('dbo.cfUsers', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_cfBudgetInvites_cfUsers_InvitedByUserId')
    ALTER TABLE dbo.cfBudgetInvites ADD CONSTRAINT FK_cfBudgetInvites_cfUsers_InvitedByUserId FOREIGN KEY (InvitedByUserId) REFERENCES dbo.cfUsers(UserId);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_cfBudgetInvites_cfSharedBudgets_SharedBudgetId')
    ALTER TABLE dbo.cfBudgetInvites ADD CONSTRAINT FK_cfBudgetInvites_cfSharedBudgets_SharedBudgetId FOREIGN KEY (SharedBudgetId) REFERENCES dbo.cfSharedBudgets(SharedBudgetId);

IF OBJECT_ID('dbo.cfAccounts', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_cfAccounts_cfSharedBudgets_SharedBudgetId')
    ALTER TABLE dbo.cfAccounts ADD CONSTRAINT FK_cfAccounts_cfSharedBudgets_SharedBudgetId FOREIGN KEY (SharedBudgetId) REFERENCES dbo.cfSharedBudgets(SharedBudgetId);

IF OBJECT_ID('dbo.cfTransactions', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_cfTransactions_cfSharedBudgets_SharedBudgetId')
    ALTER TABLE dbo.cfTransactions ADD CONSTRAINT FK_cfTransactions_cfSharedBudgets_SharedBudgetId FOREIGN KEY (SharedBudgetId) REFERENCES dbo.cfSharedBudgets(SharedBudgetId);

IF OBJECT_ID('dbo.cfCategories', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_cfCategories_cfSharedBudgets_SharedBudgetId')
    ALTER TABLE dbo.cfCategories ADD CONSTRAINT FK_cfCategories_cfSharedBudgets_SharedBudgetId FOREIGN KEY (SharedBudgetId) REFERENCES dbo.cfSharedBudgets(SharedBudgetId);

IF OBJECT_ID('dbo.cfBudgets', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_cfBudgets_cfSharedBudgets_SharedBudgetId')
    ALTER TABLE dbo.cfBudgets ADD CONSTRAINT FK_cfBudgets_cfSharedBudgets_SharedBudgetId FOREIGN KEY (SharedBudgetId) REFERENCES dbo.cfSharedBudgets(SharedBudgetId);

IF OBJECT_ID('dbo.cfCategoryBudgetTargets', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_cfCategoryBudgetTargets_cfSharedBudgets_SharedBudgetId')
    ALTER TABLE dbo.cfCategoryBudgetTargets ADD CONSTRAINT FK_cfCategoryBudgetTargets_cfSharedBudgets_SharedBudgetId FOREIGN KEY (SharedBudgetId) REFERENCES dbo.cfSharedBudgets(SharedBudgetId);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.cfCategoryBudgetTargets', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_cfCategoryBudgetTargets_cfSharedBudgets_SharedBudgetId')
    ALTER TABLE dbo.cfCategoryBudgetTargets DROP CONSTRAINT FK_cfCategoryBudgetTargets_cfSharedBudgets_SharedBudgetId;

IF OBJECT_ID('dbo.cfBudgets', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_cfBudgets_cfSharedBudgets_SharedBudgetId')
    ALTER TABLE dbo.cfBudgets DROP CONSTRAINT FK_cfBudgets_cfSharedBudgets_SharedBudgetId;

IF OBJECT_ID('dbo.cfCategories', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_cfCategories_cfSharedBudgets_SharedBudgetId')
    ALTER TABLE dbo.cfCategories DROP CONSTRAINT FK_cfCategories_cfSharedBudgets_SharedBudgetId;

IF OBJECT_ID('dbo.cfTransactions', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_cfTransactions_cfSharedBudgets_SharedBudgetId')
    ALTER TABLE dbo.cfTransactions DROP CONSTRAINT FK_cfTransactions_cfSharedBudgets_SharedBudgetId;

IF OBJECT_ID('dbo.cfAccounts', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_cfAccounts_cfSharedBudgets_SharedBudgetId')
    ALTER TABLE dbo.cfAccounts DROP CONSTRAINT FK_cfAccounts_cfSharedBudgets_SharedBudgetId;

IF OBJECT_ID('dbo.cfBudgetInvites', 'U') IS NOT NULL
    DROP TABLE dbo.cfBudgetInvites;

IF OBJECT_ID('dbo.cfBudgetMembers', 'U') IS NOT NULL
    DROP TABLE dbo.cfBudgetMembers;

IF OBJECT_ID('dbo.cfSharedBudgets', 'U') IS NOT NULL
    DROP TABLE dbo.cfSharedBudgets;

IF OBJECT_ID('dbo.cfNotificationSendLog', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfNotificationSendLog_SharedBudgetId' AND object_id = OBJECT_ID('dbo.cfNotificationSendLog'))
    DROP INDEX IX_cfNotificationSendLog_SharedBudgetId ON dbo.cfNotificationSendLog;

IF OBJECT_ID('dbo.cfNotificationSendLog', 'U') IS NOT NULL AND COL_LENGTH('dbo.cfNotificationSendLog', 'SharedBudgetId') IS NOT NULL
    ALTER TABLE dbo.cfNotificationSendLog DROP COLUMN SharedBudgetId;

IF OBJECT_ID('dbo.cfCategoryBudgetTargets', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfCategoryBudgetTargets_SharedBudgetId_CategoryId_BudgetMonth' AND object_id = OBJECT_ID('dbo.cfCategoryBudgetTargets'))
    DROP INDEX IX_cfCategoryBudgetTargets_SharedBudgetId_CategoryId_BudgetMonth ON dbo.cfCategoryBudgetTargets;

IF OBJECT_ID('dbo.cfCategoryBudgetTargets', 'U') IS NOT NULL AND COL_LENGTH('dbo.cfCategoryBudgetTargets', 'SharedBudgetId') IS NOT NULL
    ALTER TABLE dbo.cfCategoryBudgetTargets DROP COLUMN SharedBudgetId;

IF OBJECT_ID('dbo.cfBudgets', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfBudgets_SharedBudgetId' AND object_id = OBJECT_ID('dbo.cfBudgets'))
    DROP INDEX IX_cfBudgets_SharedBudgetId ON dbo.cfBudgets;

IF OBJECT_ID('dbo.cfBudgets', 'U') IS NOT NULL AND COL_LENGTH('dbo.cfBudgets', 'SharedBudgetId') IS NOT NULL
    ALTER TABLE dbo.cfBudgets DROP COLUMN SharedBudgetId;

IF OBJECT_ID('dbo.cfCategories', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfCategories_SharedBudgetId' AND object_id = OBJECT_ID('dbo.cfCategories'))
    DROP INDEX IX_cfCategories_SharedBudgetId ON dbo.cfCategories;

IF OBJECT_ID('dbo.cfCategories', 'U') IS NOT NULL AND COL_LENGTH('dbo.cfCategories', 'SharedBudgetId') IS NOT NULL
    ALTER TABLE dbo.cfCategories DROP COLUMN SharedBudgetId;

IF OBJECT_ID('dbo.cfTransactions', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfTransactions_SharedBudgetId' AND object_id = OBJECT_ID('dbo.cfTransactions'))
    DROP INDEX IX_cfTransactions_SharedBudgetId ON dbo.cfTransactions;

IF OBJECT_ID('dbo.cfTransactions', 'U') IS NOT NULL AND COL_LENGTH('dbo.cfTransactions', 'SharedBudgetId') IS NOT NULL
    ALTER TABLE dbo.cfTransactions DROP COLUMN SharedBudgetId;

IF OBJECT_ID('dbo.cfAccounts', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cfAccounts_SharedBudgetId' AND object_id = OBJECT_ID('dbo.cfAccounts'))
    DROP INDEX IX_cfAccounts_SharedBudgetId ON dbo.cfAccounts;

IF OBJECT_ID('dbo.cfAccounts', 'U') IS NOT NULL AND COL_LENGTH('dbo.cfAccounts', 'SharedBudgetId') IS NOT NULL
    ALTER TABLE dbo.cfAccounts DROP COLUMN SharedBudgetId;
");
        }
    }
}
