using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogoPedidos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmacionEntregaPorProveedor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConfirmacionesEntrega_PedidoId",
                table: "ConfirmacionesEntrega");

            migrationBuilder.AddColumn<int>(
                name: "ProveedorId",
                table: "ConfirmacionesEntrega",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecibidoPorNombre",
                table: "ConfirmacionesEntrega",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmacionesEntrega_PedidoId",
                table: "ConfirmacionesEntrega",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmacionesEntrega_ProveedorId",
                table: "ConfirmacionesEntrega",
                column: "ProveedorId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConfirmacionesEntrega_Proveedores_ProveedorId",
                table: "ConfirmacionesEntrega",
                column: "ProveedorId",
                principalTable: "Proveedores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConfirmacionesEntrega_Proveedores_ProveedorId",
                table: "ConfirmacionesEntrega");

            migrationBuilder.DropIndex(
                name: "IX_ConfirmacionesEntrega_PedidoId",
                table: "ConfirmacionesEntrega");

            migrationBuilder.DropIndex(
                name: "IX_ConfirmacionesEntrega_ProveedorId",
                table: "ConfirmacionesEntrega");

            migrationBuilder.DropColumn(
                name: "ProveedorId",
                table: "ConfirmacionesEntrega");

            migrationBuilder.DropColumn(
                name: "RecibidoPorNombre",
                table: "ConfirmacionesEntrega");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmacionesEntrega_PedidoId",
                table: "ConfirmacionesEntrega",
                column: "PedidoId",
                unique: true);
        }
    }
}
