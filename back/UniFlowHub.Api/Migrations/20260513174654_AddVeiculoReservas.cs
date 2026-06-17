using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UniFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddVeiculoReservas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VeiculoReserva",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Chassi = table.Column<string>(type: "text", nullable: false),
                    Reservado = table.Column<bool>(type: "boolean", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoPorUserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VeiculoReserva", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VeiculoReserva_User_AtualizadoPorUserId",
                        column: x => x.AtualizadoPorUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VeiculoReserva_AtualizadoPorUserId",
                table: "VeiculoReserva",
                column: "AtualizadoPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VeiculoReserva_Chassi",
                table: "VeiculoReserva",
                column: "Chassi",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VeiculoReserva");
        }
    }
}
