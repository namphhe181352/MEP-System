using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationReferenceIdRaw : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE notifications ADD COLUMN IF NOT EXISTS reference_id uniqueidentifier NULL;
                ALTER TABLE notifications ADD COLUMN IF NOT EXISTS reference_type nvarchar(100) NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE notifications DROP COLUMN IF EXISTS reference_type;
                ALTER TABLE notifications DROP COLUMN IF EXISTS reference_id;
            ");
        }
    }
}
