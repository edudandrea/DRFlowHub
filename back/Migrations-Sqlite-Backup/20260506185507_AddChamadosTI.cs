using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DRFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddChamadosTI : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChamadoTI",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Titulo = table.Column<string>(type: "TEXT", nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", nullable: false),
                    Solicitante = table.Column<string>(type: "TEXT", nullable: false),
                    Departamento = table.Column<string>(type: "TEXT", nullable: false),
                    Prioridade = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Responsavel = table.Column<string>(type: "TEXT", nullable: false),
                    AcessoRemotoUrl = table.Column<string>(type: "TEXT", nullable: false),
                    AnexoImagemUrl = table.Column<string>(type: "TEXT", nullable: false),
                    DataAbertura = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Userid = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChamadoTI", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChamadoTI_User_Userid",
                        column: x => x.Userid,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChamadoTI_Userid",
                table: "ChamadoTI",
                column: "Userid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChamadoTI");
        }
    }
}
