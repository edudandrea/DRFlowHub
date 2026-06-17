using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UniFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddEmpresasRevendas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                table: "Unidade",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumeroRevenda",
                table: "Unidade",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Empresa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresa", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Unidade_EmpresaId",
                table: "Unidade",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Empresa_Numero",
                table: "Empresa",
                column: "Numero",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Unidade_Empresa_EmpresaId",
                table: "Unidade",
                column: "EmpresaId",
                principalTable: "Empresa",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Unidade_Empresa_EmpresaId",
                table: "Unidade");

            migrationBuilder.DropTable(
                name: "Empresa");

            migrationBuilder.DropIndex(
                name: "IX_Unidade_EmpresaId",
                table: "Unidade");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "Unidade");

            migrationBuilder.DropColumn(
                name: "NumeroRevenda",
                table: "Unidade");
        }
    }
}
