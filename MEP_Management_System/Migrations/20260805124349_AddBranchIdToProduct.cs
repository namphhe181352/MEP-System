using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchIdToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "products",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "products");
        }
    }
}
