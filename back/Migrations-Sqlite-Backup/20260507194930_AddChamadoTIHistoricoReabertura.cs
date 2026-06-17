using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DRFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddChamadoTIHistoricoReabertura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataPrimeiroEncerramento",
                table: "ChamadoTI",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataReabertura",
                table: "ChamadoTI",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Reaberto",
                table: "ChamadoTI",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataPrimeiroEncerramento",
                table: "ChamadoTI");

            migrationBuilder.DropColumn(
                name: "DataReabertura",
                table: "ChamadoTI");

            migrationBuilder.DropColumn(
                name: "Reaberto",
                table: "ChamadoTI");
        }
    }
}
