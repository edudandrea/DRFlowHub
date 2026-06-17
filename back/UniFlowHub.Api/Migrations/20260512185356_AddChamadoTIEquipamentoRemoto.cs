using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddChamadoTIEquipamentoRemoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EquipamentoIp",
                table: "ChamadoTI",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EquipamentoNome",
                table: "ChamadoTI",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EquipamentoSistemaOperacional",
                table: "ChamadoTI",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EquipamentoIp",
                table: "ChamadoTI");

            migrationBuilder.DropColumn(
                name: "EquipamentoNome",
                table: "ChamadoTI");

            migrationBuilder.DropColumn(
                name: "EquipamentoSistemaOperacional",
                table: "ChamadoTI");
        }
    }
}
