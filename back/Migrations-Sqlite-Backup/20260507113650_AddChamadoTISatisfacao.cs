using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DRFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddChamadoTISatisfacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataAvaliacao",
                table: "ChamadoTI",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Observacoes",
                table: "ChamadoTI",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ObservacoesEncerramento",
                table: "ChamadoTI",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SatisfacaoComentario",
                table: "ChamadoTI",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SatisfacaoNota",
                table: "ChamadoTI",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataAvaliacao",
                table: "ChamadoTI");

            migrationBuilder.DropColumn(
                name: "Observacoes",
                table: "ChamadoTI");

            migrationBuilder.DropColumn(
                name: "ObservacoesEncerramento",
                table: "ChamadoTI");

            migrationBuilder.DropColumn(
                name: "SatisfacaoComentario",
                table: "ChamadoTI");

            migrationBuilder.DropColumn(
                name: "SatisfacaoNota",
                table: "ChamadoTI");
        }
    }
}
