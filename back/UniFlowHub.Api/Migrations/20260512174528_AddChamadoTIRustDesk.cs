using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddChamadoTIRustDesk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RustDeskId",
                table: "ChamadoTI",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RustDeskKey",
                table: "ChamadoTI",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RustDeskSenha",
                table: "ChamadoTI",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RustDeskServidor",
                table: "ChamadoTI",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RustDeskId",
                table: "ChamadoTI");

            migrationBuilder.DropColumn(
                name: "RustDeskKey",
                table: "ChamadoTI");

            migrationBuilder.DropColumn(
                name: "RustDeskSenha",
                table: "ChamadoTI");

            migrationBuilder.DropColumn(
                name: "RustDeskServidor",
                table: "ChamadoTI");
        }
    }
}
