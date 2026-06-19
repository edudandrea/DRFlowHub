using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UniFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddGestaoPessoas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GestaoPessoasEtapa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    TipoProcesso = table.Column<string>(type: "text", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GestaoPessoasEtapa", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GestaoPessoasProcesso",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TipoProcesso = table.Column<string>(type: "text", nullable: false),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Solicitante = table.Column<string>(type: "text", nullable: false),
                    Unidade = table.Column<string>(type: "text", nullable: false),
                    Departamento = table.Column<string>(type: "text", nullable: false),
                    ColaboradorNome = table.Column<string>(type: "text", nullable: false),
                    Cargo = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Prioridade = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Observacoes = table.Column<string>(type: "text", nullable: false),
                    DataSolicitacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAprovacaoGestor = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AprovadorGestor = table.Column<string>(type: "text", nullable: false),
                    ObservacoesAprovacao = table.Column<string>(type: "text", nullable: false),
                    DataCancelamento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "text", nullable: false),
                    DataConclusao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EtapaAtualId = table.Column<int>(type: "integer", nullable: true),
                    Userid = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GestaoPessoasProcesso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GestaoPessoasProcesso_GestaoPessoasEtapa_EtapaAtualId",
                        column: x => x.EtapaAtualId,
                        principalTable: "GestaoPessoasEtapa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GestaoPessoasProcesso_User_Userid",
                        column: x => x.Userid,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GestaoPessoasProcessoHistorico",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProcessoId = table.Column<int>(type: "integer", nullable: false),
                    EtapaId = table.Column<int>(type: "integer", nullable: false),
                    Acao = table.Column<string>(type: "text", nullable: false),
                    Observacoes = table.Column<string>(type: "text", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioNome = table.Column<string>(type: "text", nullable: false),
                    DataMovimentacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GestaoPessoasProcessoHistorico", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GestaoPessoasProcessoHistorico_GestaoPessoasEtapa_EtapaId",
                        column: x => x.EtapaId,
                        principalTable: "GestaoPessoasEtapa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GestaoPessoasProcessoHistorico_GestaoPessoasProcesso_Proces~",
                        column: x => x.ProcessoId,
                        principalTable: "GestaoPessoasProcesso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GestaoPessoasEtapa_TipoProcesso_Ordem",
                table: "GestaoPessoasEtapa",
                columns: new[] { "TipoProcesso", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_GestaoPessoasProcesso_EtapaAtualId",
                table: "GestaoPessoasProcesso",
                column: "EtapaAtualId");

            migrationBuilder.CreateIndex(
                name: "IX_GestaoPessoasProcesso_Userid",
                table: "GestaoPessoasProcesso",
                column: "Userid");

            migrationBuilder.CreateIndex(
                name: "IX_GestaoPessoasProcessoHistorico_EtapaId",
                table: "GestaoPessoasProcessoHistorico",
                column: "EtapaId");

            migrationBuilder.CreateIndex(
                name: "IX_GestaoPessoasProcessoHistorico_ProcessoId",
                table: "GestaoPessoasProcessoHistorico",
                column: "ProcessoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GestaoPessoasProcessoHistorico");

            migrationBuilder.DropTable(
                name: "GestaoPessoasProcesso");

            migrationBuilder.DropTable(
                name: "GestaoPessoasEtapa");
        }
    }
}
