using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRustDeskDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RustDeskHostname",
                table: "User",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RustDeskId",
                table: "User",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RustDeskSenha",
                table: "User",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RustDeskSistemaOperacional",
                table: "User",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RustDeskHostname",
                table: "User");

            migrationBuilder.DropColumn(
                name: "RustDeskId",
                table: "User");

            migrationBuilder.DropColumn(
                name: "RustDeskSenha",
                table: "User");

            migrationBuilder.DropColumn(
                name: "RustDeskSistemaOperacional",
                table: "User");
        }
    }
}
