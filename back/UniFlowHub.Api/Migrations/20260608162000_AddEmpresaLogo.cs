using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowHub.Api.Migrations
{
    public partial class AddEmpresaLogo : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Empresa"
                ADD COLUMN IF NOT EXISTS "LogoUrl" text NOT NULL DEFAULT '';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Empresa"
                DROP COLUMN IF EXISTS "LogoUrl";
                """);
        }
    }
}
