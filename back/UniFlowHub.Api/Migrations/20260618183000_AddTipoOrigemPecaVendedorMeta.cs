using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using UniFlowHub.Api.Data;

#nullable disable

namespace UniFlowHub.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260618183000_AddTipoOrigemPecaVendedorMeta")]
    public partial class AddTipoOrigemPecaVendedorMeta : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Origem",
                table: "PecaVendedorMeta",
                type: "text",
                nullable: false,
                defaultValue: "pecas");

            migrationBuilder.AddColumn<string>(
                name: "TipoMeta",
                table: "PecaVendedorMeta",
                type: "text",
                nullable: false,
                defaultValue: "valor");

            migrationBuilder.CreateIndex(
                name: "IX_PecaVendedorMeta_CpfVendedor_Origem_TipoMeta_DataInicio_DataFim",
                table: "PecaVendedorMeta",
                columns: new[] { "CpfVendedor", "Origem", "TipoMeta", "DataInicio", "DataFim" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PecaVendedorMeta_CpfVendedor_Origem_TipoMeta_DataInicio_DataFim",
                table: "PecaVendedorMeta");

            migrationBuilder.DropColumn(
                name: "Origem",
                table: "PecaVendedorMeta");

            migrationBuilder.DropColumn(
                name: "TipoMeta",
                table: "PecaVendedorMeta");
        }
    }
}
