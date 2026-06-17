using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UniFlowHub.Api.Data;

#nullable disable

namespace UniFlowHub.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260616120000_AddEquipamentoTIInventario")]
    public partial class AddEquipamentoTIInventario : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FilialCompraId",
                table: "EquipamentoTI",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilialCompra",
                table: "EquipamentoTI",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NotaFiscalCompra",
                table: "EquipamentoTI",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UsuarioResponsavelId",
                table: "EquipamentoTI",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioResponsavelNome",
                table: "EquipamentoTI",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UsuarioResponsavelEmail",
                table: "EquipamentoTI",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UsuarioResponsavelDepartamento",
                table: "EquipamentoTI",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UsuarioResponsavelUnidade",
                table: "EquipamentoTI",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "FilialCompraId", table: "EquipamentoTI");
            migrationBuilder.DropColumn(name: "FilialCompra", table: "EquipamentoTI");
            migrationBuilder.DropColumn(name: "NotaFiscalCompra", table: "EquipamentoTI");
            migrationBuilder.DropColumn(name: "UsuarioResponsavelId", table: "EquipamentoTI");
            migrationBuilder.DropColumn(name: "UsuarioResponsavelNome", table: "EquipamentoTI");
            migrationBuilder.DropColumn(name: "UsuarioResponsavelEmail", table: "EquipamentoTI");
            migrationBuilder.DropColumn(name: "UsuarioResponsavelDepartamento", table: "EquipamentoTI");
            migrationBuilder.DropColumn(name: "UsuarioResponsavelUnidade", table: "EquipamentoTI");
        }
    }
}
