using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryCheckAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPayments_purchase_orders_PurchaseOrderId",
                table: "SupplierPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPayments_suppliers_SupplierId",
                table: "SupplierPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPayments_users_RecordedBy",
                table: "SupplierPayments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SupplierPayments",
                table: "SupplierPayments");

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "inventory_check_tickets",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by_user_id",
                table: "inventory_check_tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "assignee_user_id",
                table: "inventory_check_tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                table: "inventory_check_tickets",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by_user_id",
                table: "inventory_check_tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancelled_reason",
                table: "inventory_check_tickets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "filled_at",
                table: "inventory_check_tickets",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "filled_by_user_id",
                table: "inventory_check_tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "linked_stock_ticket_id",
                table: "inventory_check_tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "counted_at",
                table: "inventory_check_details",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "counted_by_user_id",
                table: "inventory_check_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_counted",
                table: "inventory_check_details",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "reason_note",
                table: "inventory_check_details",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "supplier_payments_pkey",
                table: "SupplierPayments",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_check_tickets_linked_stock_ticket_id",
                table: "inventory_check_tickets",
                column: "linked_stock_ticket_id");

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_check_tickets_stock_tickets_linked_stock_ticket_id",
                table: "inventory_check_tickets",
                column: "linked_stock_ticket_id",
                principalTable: "stock_tickets",
                principalColumn: "stock_ticket_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPayments_PurchaseOrders",
                table: "SupplierPayments",
                column: "PurchaseOrderId",
                principalTable: "purchase_orders",
                principalColumn: "purchase_order_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPayments_Suppliers",
                table: "SupplierPayments",
                column: "SupplierId",
                principalTable: "suppliers",
                principalColumn: "supplier_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPayments_Users_RecordedBy",
                table: "SupplierPayments",
                column: "RecordedBy",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_inventory_check_tickets_stock_tickets_linked_stock_ticket_id",
                table: "inventory_check_tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPayments_PurchaseOrders",
                table: "SupplierPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPayments_Suppliers",
                table: "SupplierPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPayments_Users_RecordedBy",
                table: "SupplierPayments");

            migrationBuilder.DropPrimaryKey(
                name: "supplier_payments_pkey",
                table: "SupplierPayments");

            migrationBuilder.DropIndex(
                name: "IX_inventory_check_tickets_linked_stock_ticket_id",
                table: "inventory_check_tickets");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "inventory_check_tickets");

            migrationBuilder.DropColumn(
                name: "approved_by_user_id",
                table: "inventory_check_tickets");

            migrationBuilder.DropColumn(
                name: "assignee_user_id",
                table: "inventory_check_tickets");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "inventory_check_tickets");

            migrationBuilder.DropColumn(
                name: "cancelled_by_user_id",
                table: "inventory_check_tickets");

            migrationBuilder.DropColumn(
                name: "cancelled_reason",
                table: "inventory_check_tickets");

            migrationBuilder.DropColumn(
                name: "filled_at",
                table: "inventory_check_tickets");

            migrationBuilder.DropColumn(
                name: "filled_by_user_id",
                table: "inventory_check_tickets");

            migrationBuilder.DropColumn(
                name: "linked_stock_ticket_id",
                table: "inventory_check_tickets");

            migrationBuilder.DropColumn(
                name: "counted_at",
                table: "inventory_check_details");

            migrationBuilder.DropColumn(
                name: "counted_by_user_id",
                table: "inventory_check_details");

            migrationBuilder.DropColumn(
                name: "is_counted",
                table: "inventory_check_details");

            migrationBuilder.DropColumn(
                name: "reason_note",
                table: "inventory_check_details");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SupplierPayments",
                table: "SupplierPayments",
                column: "PaymentId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPayments_purchase_orders_PurchaseOrderId",
                table: "SupplierPayments",
                column: "PurchaseOrderId",
                principalTable: "purchase_orders",
                principalColumn: "purchase_order_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPayments_suppliers_SupplierId",
                table: "SupplierPayments",
                column: "SupplierId",
                principalTable: "suppliers",
                principalColumn: "supplier_id");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPayments_users_RecordedBy",
                table: "SupplierPayments",
                column: "RecordedBy",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
