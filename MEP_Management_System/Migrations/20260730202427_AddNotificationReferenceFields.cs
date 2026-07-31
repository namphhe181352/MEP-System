using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationReferenceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.AddColumn<Guid?>(
            //     name: "reference_id",
            //     table: "notifications",
            //     type: "uuid",
            //     nullable: true);

            // migrationBuilder.AddColumn<string>(
            //     name: "reference_type",
            //     table: "notifications",
            //     type: "character varying(100)",
            //     maxLength: 100,
            //     nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reference_type",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "reference_id",
                table: "notifications");
        }
    }
}
