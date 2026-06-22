using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UniFlowHub.Migrations
{
    [Migration("20260622165000_AddEmpresaRevendaStatusConfig")]
    public partial class AddEmpresaRevendaStatusConfig : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Ativa",
                table: "MontadoraRevendaConfig",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "EmpresaRevendaStatusConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaNumero = table.Column<int>(type: "integer", nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmpresaRevendaStatusConfig", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmpresaRevendaStatusConfig_EmpresaNumero",
                table: "EmpresaRevendaStatusConfig",
                column: "EmpresaNumero",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EmpresaRevendaStatusConfig");

            migrationBuilder.DropColumn(
                name: "Ativa",
                table: "MontadoraRevendaConfig");
        }
    }
}
