using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddProductAttributeTypesTableManual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if table already exists before creating
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS product_attribute_types (
                    type_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    type_name character varying(100) NOT NULL,
                    type_code character varying(50),
                    is_active smallint NOT NULL DEFAULT 1,
                    created_at timestamp without time zone NOT NULL,
                    created_by uuid,
                    updated_at timestamp without time zone,
                    updated_by uuid,
                    CONSTRAINT product_attribute_types_pkey PRIMARY KEY (type_id)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_product_attribute_types_type_name
                ON product_attribute_types (type_name);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
