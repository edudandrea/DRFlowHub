using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UniFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddPerfisSistema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PerfilSistema",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    PadraoSistema = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerfilSistema", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PerfilSistemaAcesso",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PerfilSistemaId = table.Column<int>(type: "integer", nullable: false),
                    Chave = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerfilSistemaAcesso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerfilSistemaAcesso_PerfilSistema_PerfilSistemaId",
                        column: x => x.PerfilSistemaId,
                        principalTable: "PerfilSistema",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PerfilSistema_Nome",
                table: "PerfilSistema",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerfilSistemaAcesso_PerfilSistemaId_Chave",
                table: "PerfilSistemaAcesso",
                columns: new[] { "PerfilSistemaId", "Chave" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PerfilSistemaAcesso");

            migrationBuilder.DropTable(
                name: "PerfilSistema");
        }
    }
}
