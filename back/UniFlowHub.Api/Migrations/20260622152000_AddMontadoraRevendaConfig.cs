using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UniFlowHub.Migrations
{
    [Migration("20260622152000_AddMontadoraRevendaConfig")]
    public partial class AddMontadoraRevendaConfig : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MontadoraRevendaConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaNumero = table.Column<int>(type: "integer", nullable: false),
                    RevendaNumero = table.Column<int>(type: "integer", nullable: false),
                    Montadora = table.Column<string>(type: "text", nullable: false),
                    LogoMontadoraUrl = table.Column<string>(type: "text", nullable: true),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MontadoraRevendaConfig", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MontadoraRevendaConfig_EmpresaNumero_RevendaNumero",
                table: "MontadoraRevendaConfig",
                columns: new[] { "EmpresaNumero", "RevendaNumero" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MontadoraRevendaConfig");
        }
    }
}
