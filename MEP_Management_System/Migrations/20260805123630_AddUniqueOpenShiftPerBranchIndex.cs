using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueOpenShiftPerBranchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clean up duplicate OPEN shifts before creating the unique index.
            // Keep the most recent OPEN shift per branch, close the rest.
            migrationBuilder.Sql(@"
                UPDATE shifts
                SET status = 'CLOSED',
                    ended_at = started_at,
                    note = COALESCE(note, '') || ' [Hệ thống tự động chốt do phát hiện trùng ca mở]'
                WHERE shift_id IN (
                    SELECT shift_id
                    FROM (
                        SELECT shift_id,
                            ROW_NUMBER() OVER (PARTITION BY branch_id ORDER BY started_at DESC) AS rn
                        FROM shifts
                        WHERE status = 'OPEN'
                    ) AS ranked
                    WHERE rn > 1
                );
            ");

            migrationBuilder.DropIndex(
                name: "IX_shifts_branch_id",
                table: "shifts");

            migrationBuilder.CreateIndex(
                name: "ix_shifts_one_open_per_branch",
                table: "shifts",
                column: "branch_id",
                unique: true,
                filter: "status = 'OPEN'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_shifts_one_open_per_branch",
                table: "shifts");

            migrationBuilder.CreateIndex(
                name: "IX_shifts_branch_id",
                table: "shifts",
                column: "branch_id");
        }
    }
}
