using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogoPedidos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pedidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SolicitanteId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SolicitanteNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Comentario = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pedidos", x => x.Id);
                });

            migrationBuilder.AddColumn<int>(
                name: "PedidoId",
                table: "Solicitudes",
                type: "int",
                nullable: true);

            // Backfill: cada Solicitud existente se convierte en su propio Pedido de 1 ítem,
            // llevándose consigo el Comentario que hasta ahora vivía en la línea (ver
            // Domain/Entities/Pedido.cs). Así no se pierden datos de solicitudes ya creadas.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [Solicitudes])
BEGIN
    CREATE TABLE #PedidoMap (SolicitudId INT NOT NULL, PedidoId INT NOT NULL);

    MERGE INTO [Pedidos] AS destino
    USING [Solicitudes] AS origen
    ON 1 = 0
    WHEN NOT MATCHED THEN
        INSERT ([SolicitanteId], [SolicitanteNombre], [Comentario], [FechaCreacion])
        VALUES (origen.[SolicitanteId], origen.[SolicitanteNombre], origen.[Comentario], origen.[FechaSolicitud])
    OUTPUT origen.[Id], inserted.[Id] INTO #PedidoMap ([SolicitudId], [PedidoId]);

    UPDATE s
    SET s.[PedidoId] = m.[PedidoId]
    FROM [Solicitudes] AS s
    INNER JOIN #PedidoMap AS m ON m.[SolicitudId] = s.[Id];

    DROP TABLE #PedidoMap;
END
");

            migrationBuilder.AlterColumn<int>(
                name: "PedidoId",
                table: "Solicitudes",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Comentario",
                table: "Solicitudes");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitudes_PedidoId",
                table: "Solicitudes",
                column: "PedidoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Solicitudes_Pedidos_PedidoId",
                table: "Solicitudes",
                column: "PedidoId",
                principalTable: "Pedidos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Solicitudes_Pedidos_PedidoId",
                table: "Solicitudes");

            migrationBuilder.DropTable(
                name: "Pedidos");

            migrationBuilder.DropIndex(
                name: "IX_Solicitudes_PedidoId",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "PedidoId",
                table: "Solicitudes");

            migrationBuilder.AddColumn<string>(
                name: "Comentario",
                table: "Solicitudes",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
