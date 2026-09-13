using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogoPedidos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarProveedores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Proveedores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Nit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Contacto = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proveedores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductoProveedores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    ProveedorId = table.Column<int>(type: "int", nullable: false),
                    CodigoProveedor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PrecioProveedor = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EsPreferido = table.Column<bool>(type: "bit", nullable: false),
                    FechaAsociacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoProveedores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductoProveedores_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductoProveedores_Proveedores_ProveedorId",
                        column: x => x.ProveedorId,
                        principalTable: "Proveedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductoProveedores_ProductoId_ProveedorId",
                table: "ProductoProveedores",
                columns: new[] { "ProductoId", "ProveedorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductoProveedores_ProveedorId_CodigoProveedor",
                table: "ProductoProveedores",
                columns: new[] { "ProveedorId", "CodigoProveedor" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductoProveedores");

            migrationBuilder.DropTable(
                name: "Proveedores");
        }
    }
}
