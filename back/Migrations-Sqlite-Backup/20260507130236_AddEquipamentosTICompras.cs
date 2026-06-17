using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DRFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipamentosTICompras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EquipamentoTI",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    Patrimonio = table.Column<string>(type: "TEXT", nullable: false),
                    Modelo = table.Column<string>(type: "TEXT", nullable: false),
                    Serial = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Origem = table.Column<string>(type: "TEXT", nullable: false),
                    Destino = table.Column<string>(type: "TEXT", nullable: false),
                    Responsavel = table.Column<string>(type: "TEXT", nullable: false),
                    DataMovimentacao = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataPrevistaRetorno = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Observacoes = table.Column<string>(type: "TEXT", nullable: false),
                    DocumentoUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Userid = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipamentoTI", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipamentoTI_User_Userid",
                        column: x => x.Userid,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SolicitacaoCompra",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Titulo = table.Column<string>(type: "TEXT", nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", nullable: false),
                    Solicitante = table.Column<string>(type: "TEXT", nullable: false),
                    Departamento = table.Column<string>(type: "TEXT", nullable: false),
                    ValorEstimado = table.Column<decimal>(type: "TEXT", nullable: false),
                    FornecedorSugerido = table.Column<string>(type: "TEXT", nullable: false),
                    Prioridade = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Justificativa = table.Column<string>(type: "TEXT", nullable: false),
                    Observacoes = table.Column<string>(type: "TEXT", nullable: false),
                    DocumentoUrl = table.Column<string>(type: "TEXT", nullable: false),
                    DataSolicitacao = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataAprovacao = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DataEnvioCompras = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DataConclusao = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Aprovador = table.Column<string>(type: "TEXT", nullable: false),
                    Comprador = table.Column<string>(type: "TEXT", nullable: false),
                    ObservacoesAprovacao = table.Column<string>(type: "TEXT", nullable: false),
                    ObservacoesCompras = table.Column<string>(type: "TEXT", nullable: false),
                    Userid = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitacaoCompra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitacaoCompra_User_Userid",
                        column: x => x.Userid,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EquipamentoTI_Userid",
                table: "EquipamentoTI",
                column: "Userid");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitacaoCompra_Userid",
                table: "SolicitacaoCompra",
                column: "Userid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EquipamentoTI");

            migrationBuilder.DropTable(
                name: "SolicitacaoCompra");
        }
    }
}
