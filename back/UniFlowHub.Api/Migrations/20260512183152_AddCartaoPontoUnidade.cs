using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCartaoPontoUnidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CnpjUnidade",
                table: "CartaoPontoArquivo",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UnidadeId",
                table: "CartaoPontoArquivo",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartaoPontoArquivo_UnidadeId",
                table: "CartaoPontoArquivo",
                column: "UnidadeId");

            migrationBuilder.AddForeignKey(
                name: "FK_CartaoPontoArquivo_Unidade_UnidadeId",
                table: "CartaoPontoArquivo",
                column: "UnidadeId",
                principalTable: "Unidade",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CartaoPontoArquivo_Unidade_UnidadeId",
                table: "CartaoPontoArquivo");

            migrationBuilder.DropIndex(
                name: "IX_CartaoPontoArquivo_UnidadeId",
                table: "CartaoPontoArquivo");

            migrationBuilder.DropColumn(
                name: "CnpjUnidade",
                table: "CartaoPontoArquivo");

            migrationBuilder.DropColumn(
                name: "UnidadeId",
                table: "CartaoPontoArquivo");
        }
    }
}
