using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowHub.Migrations
{
    /// <inheritdoc />
    [Migration("001_AddMontadoraAndLogoToUnidade")]
    public partial class AddMontadoraAndLogoToUnidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Montadora",
                table: "Unidade",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LogoMontadoraUrl",
                table: "Unidade",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Montadora",
                table: "Unidade");

            migrationBuilder.DropColumn(
                name: "LogoMontadoraUrl",
                table: "Unidade");
        }
    }
}
