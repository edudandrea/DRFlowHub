using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UniFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddGuiaIcmsPagamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuiaIcmsPagamento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuiaId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DataPagamento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoPorUserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuiaIcmsPagamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuiaIcmsPagamento_User_AtualizadoPorUserId",
                        column: x => x.AtualizadoPorUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuiaIcmsPagamento_AtualizadoPorUserId",
                table: "GuiaIcmsPagamento",
                column: "AtualizadoPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuiaIcmsPagamento_GuiaId",
                table: "GuiaIcmsPagamento",
                column: "GuiaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuiaIcmsPagamento");
        }
    }
}
