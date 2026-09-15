using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogoPedidos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRecordatorioPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RecordatorioEnviado",
                table: "Pedidos",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecordatorioEnviado",
                table: "Pedidos");
        }
    }
}
