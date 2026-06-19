using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddSolicitacaoRHAprovacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Aprovada",
                table: "SolicitacaoRH",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aprovador",
                table: "SolicitacaoRH",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DataAprovacao",
                table: "SolicitacaoRH",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacoesAprovacao",
                table: "SolicitacaoRH",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Aprovada",
                table: "SolicitacaoRH");

            migrationBuilder.DropColumn(
                name: "Aprovador",
                table: "SolicitacaoRH");

            migrationBuilder.DropColumn(
                name: "DataAprovacao",
                table: "SolicitacaoRH");

            migrationBuilder.DropColumn(
                name: "ObservacoesAprovacao",
                table: "SolicitacaoRH");
        }
    }
}
