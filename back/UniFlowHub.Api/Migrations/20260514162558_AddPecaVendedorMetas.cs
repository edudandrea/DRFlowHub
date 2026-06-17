using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UniFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddPecaVendedorMetas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PecaVendedorMeta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CpfVendedor = table.Column<string>(type: "text", nullable: false),
                    NomeVendedor = table.Column<string>(type: "text", nullable: false),
                    ValorMeta = table.Column<decimal>(type: "numeric", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoPorUserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PecaVendedorMeta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PecaVendedorMeta_User_AtualizadoPorUserId",
                        column: x => x.AtualizadoPorUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PecaVendedorMeta_AtualizadoPorUserId",
                table: "PecaVendedorMeta",
                column: "AtualizadoPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PecaVendedorMeta_CpfVendedor",
                table: "PecaVendedorMeta",
                column: "CpfVendedor",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PecaVendedorMeta");
        }
    }
}
