using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class ChangeShiftIndexToUserBased : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clean up duplicate OPEN shifts per user before creating the unique index.
            // Keep the most recent OPEN shift per user, close the rest.
            migrationBuilder.Sql(@"
                UPDATE shifts
                SET status = 'CLOSED',
                    ended_at = started_at,
                    note = COALESCE(note, '') || ' [Hệ thống tự động chốt do phát hiện trùng ca mở trên cùng user]'
                WHERE shift_id IN (
                    SELECT shift_id
                    FROM (
                        SELECT shift_id,
                            ROW_NUMBER() OVER (PARTITION BY user_id ORDER BY started_at DESC) AS rn
                        FROM shifts
                        WHERE status = 'OPEN'
                    ) AS ranked
                    WHERE rn > 1
                );
            ");

            migrationBuilder.DropIndex(
                name: "ix_shifts_one_open_per_branch",
                table: "shifts");

            migrationBuilder.DropIndex(
                name: "IX_shifts_user_id",
                table: "shifts");

            migrationBuilder.CreateIndex(
                name: "IX_shifts_branch_id",
                table: "shifts",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_shifts_one_open_per_user",
                table: "shifts",
                column: "user_id",
                unique: true,
                filter: "status = 'OPEN'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_shifts_branch_id",
                table: "shifts");

            migrationBuilder.DropIndex(
                name: "ix_shifts_one_open_per_user",
                table: "shifts");

            migrationBuilder.CreateIndex(
                name: "ix_shifts_one_open_per_branch",
                table: "shifts",
                column: "branch_id",
                unique: true,
                filter: "status = 'OPEN'");

            migrationBuilder.CreateIndex(
                name: "IX_shifts_user_id",
                table: "shifts",
                column: "user_id");
        }
    }
}
