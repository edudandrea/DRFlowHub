using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DRFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddUnidadesUsuariosSolicitacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UnidadeId",
                table: "User",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unidade",
                table: "SolicitacaoCompra",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Unidade",
                table: "ChamadoTI",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Unidade",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    Cnpj = table.Column<string>(type: "TEXT", nullable: false),
                    Endereco = table.Column<string>(type: "TEXT", nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Unidade", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_User_UnidadeId",
                table: "User",
                column: "UnidadeId");

            migrationBuilder.AddForeignKey(
                name: "FK_User_Unidade_UnidadeId",
                table: "User",
                column: "UnidadeId",
                principalTable: "Unidade",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_User_Unidade_UnidadeId",
                table: "User");

            migrationBuilder.DropTable(
                name: "Unidade");

            migrationBuilder.DropIndex(
                name: "IX_User_UnidadeId",
                table: "User");

            migrationBuilder.DropColumn(
                name: "UnidadeId",
                table: "User");

            migrationBuilder.DropColumn(
                name: "Unidade",
                table: "SolicitacaoCompra");

            migrationBuilder.DropColumn(
                name: "Unidade",
                table: "ChamadoTI");
        }
    }
}
