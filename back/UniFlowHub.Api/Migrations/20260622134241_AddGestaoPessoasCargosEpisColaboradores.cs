using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UniFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddGestaoPessoasCargosEpisColaboradores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GestaoPessoasCargo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Departamento = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GestaoPessoasCargo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GestaoPessoasItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Tamanho = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GestaoPessoasItem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GestaoPessoasColaborador",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Cpf = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Telefone = table.Column<string>(type: "text", nullable: false),
                    Departamento = table.Column<string>(type: "text", nullable: false),
                    CargoId = table.Column<int>(type: "integer", nullable: true),
                    UnidadeId = table.Column<int>(type: "integer", nullable: true),
                    DataNascimento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataAdmissao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Observacoes = table.Column<string>(type: "text", nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GestaoPessoasColaborador", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GestaoPessoasColaborador_GestaoPessoasCargo_CargoId",
                        column: x => x.CargoId,
                        principalTable: "GestaoPessoasCargo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GestaoPessoasColaborador_Unidade_UnidadeId",
                        column: x => x.UnidadeId,
                        principalTable: "Unidade",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GestaoPessoasCargoItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CargoId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    Obrigatorio = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GestaoPessoasCargoItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GestaoPessoasCargoItem_GestaoPessoasCargo_CargoId",
                        column: x => x.CargoId,
                        principalTable: "GestaoPessoasCargo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GestaoPessoasCargoItem_GestaoPessoasItem_ItemId",
                        column: x => x.ItemId,
                        principalTable: "GestaoPessoasItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GestaoPessoasColaboradorRetirada",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ColaboradorId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    DataRetirada = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataDevolucao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Observacoes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GestaoPessoasColaboradorRetirada", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GestaoPessoasColaboradorRetirada_GestaoPessoasColaborador_C~",
                        column: x => x.ColaboradorId,
                        principalTable: "GestaoPessoasColaborador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GestaoPessoasColaboradorRetirada_GestaoPessoasItem_ItemId",
                        column: x => x.ItemId,
                        principalTable: "GestaoPessoasItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GestaoPessoasCargo_Nome",
                table: "GestaoPessoasCargo",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GestaoPessoasCargoItem_CargoId_ItemId",
                table: "GestaoPessoasCargoItem",
                columns: new[] { "CargoId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GestaoPessoasCargoItem_ItemId",
                table: "GestaoPessoasCargoItem",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_GestaoPessoasColaborador_CargoId",
                table: "GestaoPessoasColaborador",
                column: "CargoId");

            migrationBuilder.CreateIndex(
                name: "IX_GestaoPessoasColaborador_Cpf",
                table: "GestaoPessoasColaborador",
                column: "Cpf");

            migrationBuilder.CreateIndex(
                name: "IX_GestaoPessoasColaborador_UnidadeId",
                table: "GestaoPessoasColaborador",
                column: "UnidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_GestaoPessoasColaboradorRetirada_ColaboradorId",
                table: "GestaoPessoasColaboradorRetirada",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_GestaoPessoasColaboradorRetirada_ItemId",
                table: "GestaoPessoasColaboradorRetirada",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_GestaoPessoasItem_Tipo_Nome_Tamanho",
                table: "GestaoPessoasItem",
                columns: new[] { "Tipo", "Nome", "Tamanho" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GestaoPessoasCargoItem");

            migrationBuilder.DropTable(
                name: "GestaoPessoasColaboradorRetirada");

            migrationBuilder.DropTable(
                name: "GestaoPessoasColaborador");

            migrationBuilder.DropTable(
                name: "GestaoPessoasItem");

            migrationBuilder.DropTable(
                name: "GestaoPessoasCargo");
        }
    }
}
