using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UniFlowHub.Api.Data;

#nullable disable

namespace UniFlowHub.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260616193000_AddEquipamentoTIExclusaoLogica")]
    public partial class AddEquipamentoTIExclusaoLogica : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Excluido",
                table: "EquipamentoTI",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MotivoExclusao",
                table: "EquipamentoTI",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DataExclusao",
                table: "EquipamentoTI",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExcluidoPorUserId",
                table: "EquipamentoTI",
                type: "integer",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Excluido", table: "EquipamentoTI");
            migrationBuilder.DropColumn(name: "MotivoExclusao", table: "EquipamentoTI");
            migrationBuilder.DropColumn(name: "DataExclusao", table: "EquipamentoTI");
            migrationBuilder.DropColumn(name: "ExcluidoPorUserId", table: "EquipamentoTI");
        }
    }
}
