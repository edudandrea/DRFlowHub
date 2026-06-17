using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UniFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCartaoPonto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CartaoPontoArquivo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NomeArquivo = table.Column<string>(type: "text", nullable: false),
                    DataImportacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ImportadoPorUserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartaoPontoArquivo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CartaoPontoArquivo_User_ImportadoPorUserId",
                        column: x => x.ImportadoPorUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CartaoPontoRegistro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CartaoPontoArquivoId = table.Column<int>(type: "integer", nullable: false),
                    FuncionarioNome = table.Column<string>(type: "text", nullable: false),
                    Cpf = table.Column<string>(type: "text", nullable: false),
                    Data = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HorarioOriginal = table.Column<string>(type: "text", nullable: false),
                    HorarioEditado = table.Column<string>(type: "text", nullable: false),
                    Sequencia = table.Column<int>(type: "integer", nullable: false),
                    LinhaOriginal = table.Column<string>(type: "text", nullable: false),
                    ConfirmadoPeloUsuario = table.Column<bool>(type: "boolean", nullable: false),
                    PrecisaAjuste = table.Column<bool>(type: "boolean", nullable: false),
                    DataRespostaUsuario = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataEdicao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditadoPorUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartaoPontoRegistro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CartaoPontoRegistro_CartaoPontoArquivo_CartaoPontoArquivoId",
                        column: x => x.CartaoPontoArquivoId,
                        principalTable: "CartaoPontoArquivo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CartaoPontoRegistro_User_EditadoPorUserId",
                        column: x => x.EditadoPorUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CartaoPontoArquivo_ImportadoPorUserId",
                table: "CartaoPontoArquivo",
                column: "ImportadoPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CartaoPontoRegistro_CartaoPontoArquivoId",
                table: "CartaoPontoRegistro",
                column: "CartaoPontoArquivoId");

            migrationBuilder.CreateIndex(
                name: "IX_CartaoPontoRegistro_EditadoPorUserId",
                table: "CartaoPontoRegistro",
                column: "EditadoPorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CartaoPontoRegistro");

            migrationBuilder.DropTable(
                name: "CartaoPontoArquivo");
        }
    }
}
