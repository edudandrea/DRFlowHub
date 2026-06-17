using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class NewTables1605 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Aprovado",
                table: "ChamadoTI",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataAprovacao",
                table: "ChamadoTI",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacoesAprovacao",
                table: "ChamadoTI",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Aprovado",
                table: "ChamadoTI");

            migrationBuilder.DropColumn(
                name: "DataAprovacao",
                table: "ChamadoTI");

            migrationBuilder.DropColumn(
                name: "ObservacoesAprovacao",
                table: "ChamadoTI");
        }
    }
}
