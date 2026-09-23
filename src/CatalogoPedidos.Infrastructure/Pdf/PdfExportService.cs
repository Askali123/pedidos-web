using CatalogoPedidos.Application.Exportacion;
using CatalogoPedidos.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CatalogoPedidos.Infrastructure.Pdf;

public class PdfExportService : IPdfExportService
{
    public byte[] ExportarPedido(Solicitud pedido)
    {
        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Text($"Solicitud N.º {pedido.Id}").FontSize(20).Bold();

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(8);

                    col.Item().Text($"Solicitante: {pedido.SolicitanteNombre}");
                    col.Item().Text($"Fecha: {pedido.FechaCreacion:dd/MM/yyyy HH:mm}");
                    if (!string.IsNullOrWhiteSpace(pedido.Comentario))
                        col.Item().Text($"Comentario: {pedido.Comentario}");
                    if (!string.IsNullOrWhiteSpace(pedido.DireccionEntrega))
                        col.Item().Text($"Dirección de entrega: {pedido.DireccionEntrega}");

                    col.Item().LineHorizontal(1);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Producto").Bold();
                            header.Cell().Text("Cant.").Bold();
                            header.Cell().Text("Estado").Bold();
                            header.Cell().Text("Gestor").Bold();
                        });

                        foreach (var item in pedido.Items)
                        {
                            table.Cell().Text(item.Producto?.Nombre);
                            table.Cell().Text(item.Cantidad.ToString());
                            table.Cell().Text(item.Estado.ToString());
                            table.Cell().Text(item.GestorNombre ?? "-");
                        }
                    });

                    // Una sección "Entrega" por cada confirmación — un pedido con
                    // productos de varios proveedores puede tener varias, una por
                    // proveedor (más la eventual "sin proveedor asociado"), en vez de una
                    // sola para todo el pedido (ver
                    // docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md #3). Un pedido recién
                    // aprobado, o uno todavía sin nada aprobado, no tiene ninguna.
                    foreach (var confirmacion in pedido.ConfirmacionesEntrega.OrderBy(c => c.FechaEntrega))
                    {
                        col.Item().LineHorizontal(1);

                        var titulo = confirmacion.Proveedor is not null
                            ? $"Entrega — {confirmacion.Proveedor.Nombre}"
                            : "Entrega — sin proveedor asociado";
                        col.Item().Text(titulo).Bold().FontSize(14);
                        col.Item().Text($"Fecha: {confirmacion.FechaEntrega:dd/MM/yyyy HH:mm}");
                        col.Item().Text($"Confirmada por: {confirmacion.ConfirmadoPorNombre}");
                        if (!string.IsNullOrWhiteSpace(confirmacion.RecibidoPorNombre))
                            col.Item().Text($"Recibido por: {confirmacion.RecibidoPorNombre}");
                        if (!string.IsNullOrWhiteSpace(confirmacion.DireccionEntregada))
                            col.Item().Text($"Dirección entregada: {confirmacion.DireccionEntregada}");
                        col.Item().Text(confirmacion.CoincideConDireccionIndicada
                            ? "Coincidió con la dirección indicada por el solicitante."
                            : "NO coincidió con la dirección indicada por el solicitante.");
                        if (!string.IsNullOrWhiteSpace(confirmacion.Observaciones))
                            col.Item().Text($"Observaciones: {confirmacion.Observaciones}");
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Generado por CatalogoPedidos - ").FontSize(9);
                    x.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                });
            });
        });

        return documento.GeneratePdf();
    }

    public byte[] ExportarCatalogo(IEnumerable<Producto> productos)
    {
        var lista = productos.ToList();

        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Text("Catálogo de productos").FontSize(20).Bold();

                page.Content().PaddingVertical(15).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Producto").Bold();
                        header.Cell().Text("Categoría").Bold();
                        header.Cell().Text("Precio").Bold();
                        header.Cell().Text("Stock").Bold();
                    });

                    foreach (var producto in lista)
                    {
                        table.Cell().Text(producto.Nombre);
                        table.Cell().Text(producto.Categoria);
                        table.Cell().Text($"{producto.Precio:C}");
                        table.Cell().Text(producto.Stock.ToString());
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Generado por CatalogoPedidos - ").FontSize(9);
                    x.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                });
            });
        });

        return documento.GeneratePdf();
    }

    public byte[] ExportarCatalogoPorProveedor(IEnumerable<ProductoProveedor> asociaciones, string proveedorNombre)
    {
        var lista = asociaciones.ToList();

        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("Catálogo de productos").FontSize(20).Bold();
                    col.Item().Text($"Proveedor: {proveedorNombre}").FontSize(12);
                });

                page.Content().PaddingVertical(15).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Código interno").Bold();
                        header.Cell().Text("Código proveedor").Bold();
                        header.Cell().Text("Producto").Bold();
                        header.Cell().Text("Categoría").Bold();
                        header.Cell().Text("Precio").Bold();
                        header.Cell().Text("Stock").Bold();
                    });

                    foreach (var asociacion in lista)
                    {
                        table.Cell().Text(asociacion.ProductoId.ToString());
                        table.Cell().Text(asociacion.CodigoProveedor);
                        table.Cell().Text(asociacion.Producto?.Nombre);
                        table.Cell().Text(asociacion.Producto?.Categoria);
                        table.Cell().Text($"{(asociacion.PrecioProveedor ?? asociacion.Producto?.Precio ?? 0):C}");
                        table.Cell().Text(asociacion.Producto?.Stock.ToString() ?? "-");
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Generado por CatalogoPedidos - ").FontSize(9);
                    x.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                });
            });
        });

        return documento.GeneratePdf();
    }

    public byte[] ExportarSolicitudesPorProveedor(IEnumerable<DetalleSolicitud> solicitudes, IReadOnlyDictionary<int, string> codigosProveedorPorProducto, string proveedorNombre)
    {
        var lista = solicitudes.ToList();

        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    // "Detalle de productos", no "Pedido a proveedor" (ese título queda reservado
                    // para ExportarPedidoProveedor, el documento real de UN PedidoProveedor): este
                    // mismo export lo usa tanto el envío real de una solicitud puntual
                    // (PedidoNotificacionProveedorService) como el reporte filtrado por proveedor
                    // de Administrar solicitudes, que puede traer líneas de varias solicitudes
                    // distintas a la vez (columna "Solicitud" abajo).
                    col.Item().Text("Detalle de productos para proveedor").FontSize(18).Bold();
                    col.Item().Text($"Proveedor: {proveedorNombre}").FontSize(12);
                });

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Código interno").Bold();
                        header.Cell().Text("Código proveedor").Bold();
                        header.Cell().Text("Producto").Bold();
                        header.Cell().Text("Cant.").Bold();
                        header.Cell().Text("Solicitud").Bold();
                        header.Cell().Text("Solicitante").Bold();
                        header.Cell().Text("Fecha").Bold();
                        header.Cell().Text("Estado").Bold();
                        header.Cell().Text("Gestor").Bold();
                    });

                    foreach (var s in lista)
                    {
                        table.Cell().Text(s.ProductoId.ToString());
                        table.Cell().Text(codigosProveedorPorProducto.GetValueOrDefault(s.ProductoId, "-"));
                        table.Cell().Text(s.Producto?.Nombre);
                        table.Cell().Text(s.Cantidad.ToString());
                        table.Cell().Text($"#{s.SolicitudId}");
                        table.Cell().Text(s.SolicitanteNombre);
                        table.Cell().Text(s.FechaSolicitud.ToLocalTime().ToString("dd/MM/yyyy"));
                        table.Cell().Text(s.Estado.ToString());
                        table.Cell().Text(s.GestorNombre ?? "-");
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span($"{lista.Count} producto(s) - Generado por CatalogoPedidos - ").FontSize(8);
                    x.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8);
                });
            });
        });

        return documento.GeneratePdf();
    }

    public byte[] ExportarPedidoProveedor(PedidoProveedor documento, string proveedorNombre)
    {
        var lista = documento.Items.ToList();

        var documentoPdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text($"Solicitud de reabastecimiento N.º {documento.SolicitudId}").FontSize(18).Bold();
                    col.Item().Text($"Proveedor: {proveedorNombre}").FontSize(12);
                    col.Item().Text($"Solicitado por: {documento.Solicitud?.SolicitanteNombre}")
                        .FontSize(10);
                    if (documento.Solicitud is not null)
                        col.Item().Text($"Fecha del pedido: {documento.Solicitud.FechaCreacion:dd/MM/yyyy HH:mm}").FontSize(10);
                });

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Código interno").Bold();
                        header.Cell().Text("Código proveedor").Bold();
                        header.Cell().Text("Producto").Bold();
                        header.Cell().Text("Categoría").Bold();
                        header.Cell().Text("Cant.").Bold();
                        header.Cell().Text("Unidad").Bold();
                        header.Cell().Text("Precio").Bold();
                    });

                    foreach (var d in lista)
                    {
                        table.Cell().Text(d.ProductoId.ToString());
                        table.Cell().Text(d.CodigoProveedor);
                        table.Cell().Text(d.ProductoNombre);
                        table.Cell().Text(d.Categoria ?? "-");
                        table.Cell().Text(d.Cantidad.ToString());
                        table.Cell().Text(d.UnidadMedida ?? "-");
                        table.Cell().Text($"{(d.PrecioProveedor ?? 0):C}");
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span($"{lista.Count} producto(s) - Generado por CatalogoPedidos - ").FontSize(8);
                    x.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8);
                });
            });
        });

        return documentoPdf.GeneratePdf();
    }

    public byte[] ExportarSolicitudes(IEnumerable<DetalleSolicitud> solicitudes)
    {
        var lista = solicitudes.ToList();

        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Text("Reporte de solicitudes").FontSize(18).Bold();

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("N.º").Bold();
                        header.Cell().Text("Solicitud").Bold();
                        header.Cell().Text("Producto").Bold();
                        header.Cell().Text("Cant.").Bold();
                        header.Cell().Text("Solicitante").Bold();
                        header.Cell().Text("Fecha").Bold();
                        header.Cell().Text("Estado").Bold();
                        header.Cell().Text("Gestor").Bold();
                    });

                    foreach (var s in lista)
                    {
                        table.Cell().Text(s.Id.ToString());
                        table.Cell().Text(s.SolicitudId.ToString());
                        table.Cell().Text(s.Producto?.Nombre);
                        table.Cell().Text(s.Cantidad.ToString());
                        table.Cell().Text(s.SolicitanteNombre);
                        table.Cell().Text(s.FechaSolicitud.ToString("dd/MM/yyyy HH:mm"));
                        table.Cell().Text(s.Estado.ToString());
                        table.Cell().Text(s.GestorNombre ?? "-");
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span($"{lista.Count} solicitudes - Generado por CatalogoPedidos - ").FontSize(8);
                    x.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8);
                });
            });
        });

        return documento.GeneratePdf();
    }
}
