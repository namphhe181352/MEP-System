using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddUomFieldsToInvoiceItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "selected_unit",
                table: "invoice_items",
                newName: "SelectedUnit");

            migrationBuilder.RenameColumn(
                name: "convert_value",
                table: "invoice_items",
                newName: "ConvertValue");

            migrationBuilder.AlterColumn<string>(
                name: "SelectedUnit",
                table: "invoice_items",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ConvertValue",
                table: "invoice_items",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SelectedUnit",
                table: "invoice_items",
                newName: "selected_unit");

            migrationBuilder.RenameColumn(
                name: "ConvertValue",
                table: "invoice_items",
                newName: "convert_value");

            migrationBuilder.AlterColumn<string>(
                name: "selected_unit",
                table: "invoice_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "convert_value",
                table: "invoice_items",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);
        }
    }
}
