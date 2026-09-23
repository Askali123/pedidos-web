using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogoPedidos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregaEmpresasYSedes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                table: "Pedidos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmpresaNombre",
                table: "Pedidos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SedeId",
                table: "Pedidos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SedeNombre",
                table: "Pedidos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SedeId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Empresas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Nit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sedes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Pais = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ciudad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Direccion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Contacto = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sedes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sedes_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_EmpresaId",
                table: "Pedidos",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_SedeId",
                table: "Pedidos",
                column: "SedeId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_SedeId",
                table: "AspNetUsers",
                column: "SedeId");

            migrationBuilder.CreateIndex(
                name: "IX_Sedes_EmpresaId",
                table: "Sedes",
                column: "EmpresaId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Sedes_SedeId",
                table: "AspNetUsers",
                column: "SedeId",
                principalTable: "Sedes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pedidos_Empresas_EmpresaId",
                table: "Pedidos",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Pedidos_Sedes_SedeId",
                table: "Pedidos",
                column: "SedeId",
                principalTable: "Sedes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Sedes_SedeId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Pedidos_Empresas_EmpresaId",
                table: "Pedidos");

            migrationBuilder.DropForeignKey(
                name: "FK_Pedidos_Sedes_SedeId",
                table: "Pedidos");

            migrationBuilder.DropTable(
                name: "Sedes");

            migrationBuilder.DropTable(
                name: "Empresas");

            migrationBuilder.DropIndex(
                name: "IX_Pedidos_EmpresaId",
                table: "Pedidos");

            migrationBuilder.DropIndex(
                name: "IX_Pedidos_SedeId",
                table: "Pedidos");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_SedeId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "EmpresaNombre",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "SedeId",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "SedeNombre",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "SedeId",
                table: "AspNetUsers");
        }
    }
}
