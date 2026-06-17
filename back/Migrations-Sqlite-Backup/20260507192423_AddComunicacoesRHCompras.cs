using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DRFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddComunicacoesRHCompras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SolicitacaoCompraComunicacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SolicitacaoCompraId = table.Column<int>(type: "INTEGER", nullable: false),
                    Mensagem = table.Column<string>(type: "TEXT", nullable: false),
                    AutorNome = table.Column<string>(type: "TEXT", nullable: false),
                    AutorRole = table.Column<string>(type: "TEXT", nullable: false),
                    AutorUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitacaoCompraComunicacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitacaoCompraComunicacao_SolicitacaoCompra_SolicitacaoCompraId",
                        column: x => x.SolicitacaoCompraId,
                        principalTable: "SolicitacaoCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SolicitacaoRHComunicacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SolicitacaoRHId = table.Column<int>(type: "INTEGER", nullable: false),
                    Mensagem = table.Column<string>(type: "TEXT", nullable: false),
                    AutorNome = table.Column<string>(type: "TEXT", nullable: false),
                    AutorRole = table.Column<string>(type: "TEXT", nullable: false),
                    AutorUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitacaoRHComunicacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitacaoRHComunicacao_SolicitacaoRH_SolicitacaoRHId",
                        column: x => x.SolicitacaoRHId,
                        principalTable: "SolicitacaoRH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitacaoCompraComunicacao_SolicitacaoCompraId",
                table: "SolicitacaoCompraComunicacao",
                column: "SolicitacaoCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitacaoRHComunicacao_SolicitacaoRHId",
                table: "SolicitacaoRHComunicacao",
                column: "SolicitacaoRHId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SolicitacaoCompraComunicacao");

            migrationBuilder.DropTable(
                name: "SolicitacaoRHComunicacao");
        }
    }
}
