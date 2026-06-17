using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UniFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Unidade",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Cnpj = table.Column<string>(type: "text", nullable: false),
                    Endereco = table.Column<string>(type: "text", nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Unidade", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Cpf = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Senha = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Departamento = table.Column<string>(type: "text", nullable: false),
                    Cargo = table.Column<string>(type: "text", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    UnidadeId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DataNascimento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                    table.ForeignKey(
                        name: "FK_User_Unidade_UnidadeId",
                        column: x => x.UnidadeId,
                        principalTable: "Unidade",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_User_User_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChamadoTI",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Categoria = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Solicitante = table.Column<string>(type: "text", nullable: false),
                    Unidade = table.Column<string>(type: "text", nullable: false),
                    Departamento = table.Column<string>(type: "text", nullable: false),
                    Prioridade = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Responsavel = table.Column<string>(type: "text", nullable: false),
                    AcessoRemotoUrl = table.Column<string>(type: "text", nullable: false),
                    AnexoImagemUrl = table.Column<string>(type: "text", nullable: false),
                    Observacoes = table.Column<string>(type: "text", nullable: false),
                    ObservacoesEncerramento = table.Column<string>(type: "text", nullable: false),
                    SatisfacaoNota = table.Column<int>(type: "integer", nullable: true),
                    SatisfacaoComentario = table.Column<string>(type: "text", nullable: false),
                    DataAvaliacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataAbertura = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataPrimeiroEncerramento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataReabertura = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataEncerramento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reaberto = table.Column<bool>(type: "boolean", nullable: false),
                    Userid = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "EquipamentoTI",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Patrimonio = table.Column<string>(type: "text", nullable: false),
                    Modelo = table.Column<string>(type: "text", nullable: false),
                    Serial = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Origem = table.Column<string>(type: "text", nullable: false),
                    Destino = table.Column<string>(type: "text", nullable: false),
                    Responsavel = table.Column<string>(type: "text", nullable: false),
                    DataMovimentacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataPrevistaRetorno = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Observacoes = table.Column<string>(type: "text", nullable: false),
                    DocumentoUrl = table.Column<string>(type: "text", nullable: false),
                    Userid = table.Column<int>(type: "integer", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Categoria = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Solicitante = table.Column<string>(type: "text", nullable: false),
                    Unidade = table.Column<string>(type: "text", nullable: false),
                    Departamento = table.Column<string>(type: "text", nullable: false),
                    ValorEstimado = table.Column<decimal>(type: "numeric", nullable: false),
                    FornecedorSugerido = table.Column<string>(type: "text", nullable: false),
                    Prioridade = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Justificativa = table.Column<string>(type: "text", nullable: false),
                    Observacoes = table.Column<string>(type: "text", nullable: false),
                    DocumentoUrl = table.Column<string>(type: "text", nullable: false),
                    DataSolicitacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAprovacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataEnvioCompras = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataConclusao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Aprovador = table.Column<string>(type: "text", nullable: false),
                    Comprador = table.Column<string>(type: "text", nullable: false),
                    ObservacoesAprovacao = table.Column<string>(type: "text", nullable: false),
                    ObservacoesCompras = table.Column<string>(type: "text", nullable: false),
                    Userid = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "SolicitacaoRH",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Unidade = table.Column<string>(type: "text", nullable: false),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    TipoSolicitacao = table.Column<string>(type: "text", nullable: false),
                    Solicitante = table.Column<string>(type: "text", nullable: false),
                    Departamento = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    AnexossUrl = table.Column<string>(type: "text", nullable: false),
                    Prioridade = table.Column<string>(type: "text", nullable: false),
                    Responsavel = table.Column<string>(type: "text", nullable: false),
                    DataSolicitacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataEncerramento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Observacoes = table.Column<string>(type: "text", nullable: false),
                    ObservacoesEncerramento = table.Column<string>(type: "text", nullable: false),
                    SatisfacaoNota = table.Column<int>(type: "integer", nullable: true),
                    SatisfacaoComentario = table.Column<string>(type: "text", nullable: false),
                    DataAvaliacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Userid = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitacaoRH", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitacaoRH_User_Userid",
                        column: x => x.Userid,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChamadoTIComunicacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChamadoTIId = table.Column<int>(type: "integer", nullable: false),
                    Mensagem = table.Column<string>(type: "text", nullable: false),
                    AutorNome = table.Column<string>(type: "text", nullable: false),
                    AutorRole = table.Column<string>(type: "text", nullable: false),
                    AutorUserId = table.Column<int>(type: "integer", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "SolicitacaoCompraComunicacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SolicitacaoCompraId = table.Column<int>(type: "integer", nullable: false),
                    Mensagem = table.Column<string>(type: "text", nullable: false),
                    AutorNome = table.Column<string>(type: "text", nullable: false),
                    AutorRole = table.Column<string>(type: "text", nullable: false),
                    AutorUserId = table.Column<int>(type: "integer", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitacaoCompraComunicacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitacaoCompraComunicacao_SolicitacaoCompra_SolicitacaoC~",
                        column: x => x.SolicitacaoCompraId,
                        principalTable: "SolicitacaoCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SolicitacaoRHComunicacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SolicitacaoRHId = table.Column<int>(type: "integer", nullable: false),
                    Mensagem = table.Column<string>(type: "text", nullable: false),
                    AutorNome = table.Column<string>(type: "text", nullable: false),
                    AutorRole = table.Column<string>(type: "text", nullable: false),
                    AutorUserId = table.Column<int>(type: "integer", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "IX_ChamadoTI_Userid",
                table: "ChamadoTI",
                column: "Userid");

            migrationBuilder.CreateIndex(
                name: "IX_ChamadoTIComunicacao_ChamadoTIId",
                table: "ChamadoTIComunicacao",
                column: "ChamadoTIId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipamentoTI_Userid",
                table: "EquipamentoTI",
                column: "Userid");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitacaoCompra_Userid",
                table: "SolicitacaoCompra",
                column: "Userid");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitacaoCompraComunicacao_SolicitacaoCompraId",
                table: "SolicitacaoCompraComunicacao",
                column: "SolicitacaoCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitacaoRH_Userid",
                table: "SolicitacaoRH",
                column: "Userid");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitacaoRHComunicacao_SolicitacaoRHId",
                table: "SolicitacaoRHComunicacao",
                column: "SolicitacaoRHId");

            migrationBuilder.CreateIndex(
                name: "IX_User_CreatedByUserId",
                table: "User",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_User_UnidadeId",
                table: "User",
                column: "UnidadeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChamadoTIComunicacao");

            migrationBuilder.DropTable(
                name: "EquipamentoTI");

            migrationBuilder.DropTable(
                name: "SolicitacaoCompraComunicacao");

            migrationBuilder.DropTable(
                name: "SolicitacaoRHComunicacao");

            migrationBuilder.DropTable(
                name: "ChamadoTI");

            migrationBuilder.DropTable(
                name: "SolicitacaoCompra");

            migrationBuilder.DropTable(
                name: "SolicitacaoRH");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropTable(
                name: "Unidade");
        }
    }
}
