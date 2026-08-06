using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class RefactorShiftToCashSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clean up duplicate OPEN shifts per branch before creating the unique index.
            migrationBuilder.Sql(@"
                UPDATE shifts
                SET status = 'CLOSED',
                    ended_at = started_at,
                    note = COALESCE(note, '') || ' [Hệ thống tự động chốt do phát hiện trùng ca mở trên cùng chi nhánh]'
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

            migrationBuilder.DropIndex(
                name: "ix_shifts_one_open_per_user",
                table: "shifts");

            migrationBuilder.AddColumn<Guid>(
                name: "closed_by_user_id",
                table: "shifts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "force_close_reason",
                table: "shifts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "shift_id",
                table: "invoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_shifts_closed_by_user_id",
                table: "shifts",
                column: "closed_by_user_id");

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

            migrationBuilder.CreateIndex(
                name: "IX_invoices_shift_id",
                table: "invoices",
                column: "shift_id");

            // Backfill shift_id on existing invoices BEFORE adding FK constraint.
            // Links each invoice to the most recent shift that was open at the branch when created.
            migrationBuilder.Sql(@"
                UPDATE invoices i
                SET shift_id = sub.shift_id
                FROM (
                    SELECT DISTINCT ON (inv.invoice_id)
                        inv.invoice_id,
                        s.shift_id
                    FROM invoices inv
                    JOIN orders o ON inv.order_id = o.order_id
                    JOIN shifts s ON s.branch_id = o.branch_id
                        AND s.started_at <= inv.created_at
                        AND (s.ended_at IS NULL OR s.ended_at >= inv.created_at)
                    WHERE inv.shift_id = '00000000-0000-0000-0000-000000000000'
                    ORDER BY inv.invoice_id, s.started_at DESC
                ) sub
                WHERE i.invoice_id = sub.invoice_id;
            ");

            migrationBuilder.AddForeignKey(
                name: "invoices_shift_id_fkey",
                table: "invoices",
                column: "shift_id",
                principalTable: "shifts",
                principalColumn: "shift_id");

            migrationBuilder.AddForeignKey(
                name: "shifts_closed_by_user_id_fkey",
                table: "shifts",
                column: "closed_by_user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "invoices_shift_id_fkey",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "shifts_closed_by_user_id_fkey",
                table: "shifts");

            migrationBuilder.DropIndex(
                name: "IX_shifts_closed_by_user_id",
                table: "shifts");

            migrationBuilder.DropIndex(
                name: "ix_shifts_one_open_per_branch",
                table: "shifts");

            migrationBuilder.DropIndex(
                name: "IX_shifts_user_id",
                table: "shifts");

            migrationBuilder.DropIndex(
                name: "IX_invoices_shift_id",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "closed_by_user_id",
                table: "shifts");

            migrationBuilder.DropColumn(
                name: "force_close_reason",
                table: "shifts");

            migrationBuilder.DropColumn(
                name: "shift_id",
                table: "invoices");

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
    }
}
