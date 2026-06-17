using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DRFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class SystemUpdate1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "User",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataNascimento",
                table: "User",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "SolicitacaoRH",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Unidade = table.Column<string>(type: "TEXT", nullable: false),
                    Titulo = table.Column<string>(type: "TEXT", nullable: false),
                    TipoSolicitacao = table.Column<string>(type: "TEXT", nullable: false),
                    Solicitante = table.Column<string>(type: "TEXT", nullable: false),
                    Departamento = table.Column<string>(type: "TEXT", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", nullable: false),
                    AnexossUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Prioridade = table.Column<string>(type: "TEXT", nullable: false),
                    Responsavel = table.Column<string>(type: "TEXT", nullable: false),
                    DataSolicitacao = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Userid = table.Column<int>(type: "INTEGER", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_User_CreatedByUserId",
                table: "User",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitacaoRH_Userid",
                table: "SolicitacaoRH",
                column: "Userid");

            migrationBuilder.AddForeignKey(
                name: "FK_User_User_CreatedByUserId",
                table: "User",
                column: "CreatedByUserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_User_User_CreatedByUserId",
                table: "User");

            migrationBuilder.DropTable(
                name: "SolicitacaoRH");

            migrationBuilder.DropIndex(
                name: "IX_User_CreatedByUserId",
                table: "User");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "User");

            migrationBuilder.DropColumn(
                name: "DataNascimento",
                table: "User");
        }
    }
}
