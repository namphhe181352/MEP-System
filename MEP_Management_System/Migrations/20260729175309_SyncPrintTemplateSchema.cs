using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class SyncPrintTemplateSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "show_tax_info",
                table: "invoice_print_templates");

            migrationBuilder.DropColumn(
                name: "tax_rate",
                table: "invoice_print_templates");

            migrationBuilder.AlterColumn<bool>(
                name: "show_logo",
                table: "stock_print_templates",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "branch_address",
                table: "stock_print_templates",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "branch_name",
                table: "stock_print_templates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "show_logo",
                table: "invoice_print_templates",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "branch_address",
                table: "stock_print_templates");

            migrationBuilder.DropColumn(
                name: "branch_name",
                table: "stock_print_templates");

            migrationBuilder.AlterColumn<bool>(
                name: "show_logo",
                table: "stock_print_templates",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "show_logo",
                table: "invoice_print_templates",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "show_tax_info",
                table: "invoice_print_templates",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tax_rate",
                table: "invoice_print_templates",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 8.00m);
        }
    }
}
