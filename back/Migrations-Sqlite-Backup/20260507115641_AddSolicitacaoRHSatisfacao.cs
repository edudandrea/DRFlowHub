using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DRFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddSolicitacaoRHSatisfacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataAvaliacao",
                table: "SolicitacaoRH",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataEncerramento",
                table: "SolicitacaoRH",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Observacoes",
                table: "SolicitacaoRH",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ObservacoesEncerramento",
                table: "SolicitacaoRH",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SatisfacaoComentario",
                table: "SolicitacaoRH",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SatisfacaoNota",
                table: "SolicitacaoRH",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataAvaliacao",
                table: "SolicitacaoRH");

            migrationBuilder.DropColumn(
                name: "DataEncerramento",
                table: "SolicitacaoRH");

            migrationBuilder.DropColumn(
                name: "Observacoes",
                table: "SolicitacaoRH");

            migrationBuilder.DropColumn(
                name: "ObservacoesEncerramento",
                table: "SolicitacaoRH");

            migrationBuilder.DropColumn(
                name: "SatisfacaoComentario",
                table: "SolicitacaoRH");

            migrationBuilder.DropColumn(
                name: "SatisfacaoNota",
                table: "SolicitacaoRH");
        }
    }
}
