using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClintonFrankland.Migrations
{
    /// <inheritdoc />
    public partial class AddGuidedOnboardingState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('cfUsers', 'OnboardingCompleted') IS NULL
                BEGIN
                    ALTER TABLE cfUsers ADD OnboardingCompleted BIT NOT NULL
                        CONSTRAINT DF_cfUsers_OnboardingCompleted DEFAULT(1);
                END
                """);
            migrationBuilder.Sql("""
                IF COL_LENGTH('cfUsers', 'OnboardingStep') IS NULL
                BEGIN
                    ALTER TABLE cfUsers ADD OnboardingStep INT NOT NULL
                        CONSTRAINT DF_cfUsers_OnboardingStep DEFAULT(0);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('cfUsers', 'OnboardingCompleted') IS NOT NULL
                    ALTER TABLE cfUsers DROP CONSTRAINT DF_cfUsers_OnboardingCompleted, COLUMN OnboardingCompleted;
                """);
            migrationBuilder.Sql("""
                IF COL_LENGTH('cfUsers', 'OnboardingStep') IS NOT NULL
                    ALTER TABLE cfUsers DROP CONSTRAINT DF_cfUsers_OnboardingStep, COLUMN OnboardingStep;
                """);
        }
    }
}
