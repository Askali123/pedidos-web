using CatalogoPedidos.Application.Exportacion;
using CatalogoPedidos.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CatalogoPedidos.Infrastructure.Pdf;

public class PdfExportService : IPdfExportService
{
    public byte[] ExportarSolicitud(SolicitudProducto solicitud)
    {
        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Text("Solicitud de producto").FontSize(20).Bold();

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(8);

                    col.Item().Text($"Solicitud N.º {solicitud.Id}").Bold();
                    col.Item().Text($"Fecha: {solicitud.FechaSolicitud:dd/MM/yyyy HH:mm}");
                    col.Item().LineHorizontal(1);

                    col.Item().Text("Producto").Bold().FontSize(14);
                    col.Item().Text($"Nombre: {solicitud.Producto?.Nombre}");
                    col.Item().Text($"Categoría: {solicitud.Producto?.Categoria}");
                    col.Item().Text($"Precio unitario: {solicitud.Producto?.Precio:C}");
                    col.Item().Text($"Cantidad solicitada: {solicitud.Cantidad}");
                    if (!string.IsNullOrWhiteSpace(solicitud.Pedido?.Comentario))
                        col.Item().Text($"Comentario del solicitante: {solicitud.Pedido.Comentario}");

                    col.Item().LineHorizontal(1);

                    col.Item().Text("Solicitante").Bold().FontSize(14);
                    col.Item().Text(solicitud.SolicitanteNombre);

                    col.Item().LineHorizontal(1);

                    col.Item().Text("Estado").Bold().FontSize(14);
                    col.Item().Text(solicitud.Estado.ToString());

                    if (solicitud.GestorNombre is not null)
                    {
                        col.Item().Text($"Gestor: {solicitud.GestorNombre}");
                        col.Item().Text($"Resuelta: {solicitud.FechaResolucion:dd/MM/yyyy HH:mm}");
                        if (!string.IsNullOrWhiteSpace(solicitud.ComentarioGestor))
                            col.Item().Text($"Comentario del gestor: {solicitud.ComentarioGestor}");
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

    public byte[] ExportarPedido(Pedido pedido)
    {
        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Text($"Pedido N.º {pedido.Id}").FontSize(20).Bold();

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(8);

                    col.Item().Text($"Solicitante: {pedido.SolicitanteNombre}");
                    col.Item().Text($"Fecha: {pedido.FechaCreacion:dd/MM/yyyy HH:mm}");
                    if (!string.IsNullOrWhiteSpace(pedido.Comentario))
                        col.Item().Text($"Comentario: {pedido.Comentario}");

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

    public byte[] ExportarSolicitudesPorProveedor(IEnumerable<SolicitudProducto> solicitudes, IReadOnlyDictionary<int, string> codigosProveedorPorProducto, string proveedorNombre)
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
                    col.Item().Text("Pedido a proveedor").FontSize(18).Bold();
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
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Código interno").Bold();
                        header.Cell().Text("Código proveedor").Bold();
                        header.Cell().Text("Producto").Bold();
                        header.Cell().Text("Cant.").Bold();
                        header.Cell().Text("Solicitante").Bold();
                        header.Cell().Text("Fecha").Bold();
                        header.Cell().Text("Estado").Bold();
                    });

                    foreach (var s in lista)
                    {
                        table.Cell().Text(s.ProductoId.ToString());
                        table.Cell().Text(codigosProveedorPorProducto.GetValueOrDefault(s.ProductoId, "-"));
                        table.Cell().Text(s.Producto?.Nombre);
                        table.Cell().Text(s.Cantidad.ToString());
                        table.Cell().Text(s.SolicitanteNombre);
                        table.Cell().Text(s.FechaSolicitud.ToLocalTime().ToString("dd/MM/yyyy"));
                        table.Cell().Text(s.Estado.ToString());
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

    public byte[] ExportarSolicitudes(IEnumerable<SolicitudProducto> solicitudes)
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
                        header.Cell().Text("Pedido").Bold();
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
                        table.Cell().Text(s.PedidoId.ToString());
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
