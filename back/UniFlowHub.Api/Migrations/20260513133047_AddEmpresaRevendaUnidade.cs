using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddEmpresaRevendaUnidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Empresa",
                table: "Unidade",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Revenda",
                table: "Unidade",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Empresa",
                table: "Unidade");

            migrationBuilder.DropColumn(
                name: "Revenda",
                table: "Unidade");
        }
    }
}
