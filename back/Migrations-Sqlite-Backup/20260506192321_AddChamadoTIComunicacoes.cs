using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DRFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddChamadoTIComunicacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataEncerramento",
                table: "ChamadoTI",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChamadoTIComunicacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChamadoTIId = table.Column<int>(type: "INTEGER", nullable: false),
                    Mensagem = table.Column<string>(type: "TEXT", nullable: false),
                    AutorNome = table.Column<string>(type: "TEXT", nullable: false),
                    AutorRole = table.Column<string>(type: "TEXT", nullable: false),
                    AutorUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChamadoTIComunicacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChamadoTIComunicacao_ChamadoTI_ChamadoTIId",
                        column: x => x.ChamadoTIId,
                        principalTable: "ChamadoTI",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChamadoTIComunicacao_ChamadoTIId",
                table: "ChamadoTIComunicacao",
                column: "ChamadoTIId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChamadoTIComunicacao");

            migrationBuilder.DropColumn(
                name: "DataEncerramento",
                table: "ChamadoTI");
        }
    }
}
