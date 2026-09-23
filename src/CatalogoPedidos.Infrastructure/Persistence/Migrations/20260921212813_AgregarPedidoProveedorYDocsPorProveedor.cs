using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogoPedidos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPedidoProveedorYDocsPorProveedor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                table: "ProductoProveedores",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Las asociaciones existentes deben quedar Activo=true: desactivar solo debe
            // afectar pedidos NUEVOS (los PedidoProveedor ya emitidos guardan sus propios
            // snapshots y no dependen de este flag). Al agregar la columna no nullable con
            // defaultValue, SQL Server rellena todas las filas existentes con 1 — listo.

            migrationBuilder.AddColumn<int>(
                name: "PedidoProveedorId",
                table: "NotificacionesProveedor",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PedidosProveedor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PedidoId = table.Column<int>(type: "int", nullable: false),
                    ProveedorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GestorId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GestorNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidosProveedor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PedidosProveedor_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PedidosProveedor_Proveedores_ProveedorId",
                        column: x => x.ProveedorId,
                        principalTable: "Proveedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DetallesPedidoProveedor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PedidoProveedorId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    ProductoNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    CodigoProveedor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UnidadMedida = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PrecioProveedor = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Excluido = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesPedidoProveedor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetallesPedidoProveedor_PedidosProveedor_PedidoProveedorId",
                        column: x => x.PedidoProveedorId,
                        principalTable: "PedidosProveedor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetallesPedidoProveedor_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificacionesProveedor_PedidoProveedorId",
                table: "NotificacionesProveedor",
                column: "PedidoProveedorId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesPedidoProveedor_PedidoProveedorId_ProductoId",
                table: "DetallesPedidoProveedor",
                columns: new[] { "PedidoProveedorId", "ProductoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DetallesPedidoProveedor_ProductoId",
                table: "DetallesPedidoProveedor",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosProveedor_PedidoId_ProveedorId",
                table: "PedidosProveedor",
                columns: new[] { "PedidoId", "ProveedorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PedidosProveedor_ProveedorId",
                table: "PedidosProveedor",
                column: "ProveedorId");

            migrationBuilder.AddForeignKey(
                name: "FK_NotificacionesProveedor_PedidosProveedor_PedidoProveedorId",
                table: "NotificacionesProveedor",
                column: "PedidoProveedorId",
                principalTable: "PedidosProveedor",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotificacionesProveedor_PedidosProveedor_PedidoProveedorId",
                table: "NotificacionesProveedor");

            migrationBuilder.DropTable(
                name: "DetallesPedidoProveedor");

            migrationBuilder.DropTable(
                name: "PedidosProveedor");

            migrationBuilder.DropIndex(
                name: "IX_NotificacionesProveedor_PedidoProveedorId",
                table: "NotificacionesProveedor");

            migrationBuilder.DropColumn(
                name: "Activo",
                table: "ProductoProveedores");

            migrationBuilder.DropColumn(
                name: "PedidoProveedorId",
                table: "NotificacionesProveedor");
        }
    }
}
