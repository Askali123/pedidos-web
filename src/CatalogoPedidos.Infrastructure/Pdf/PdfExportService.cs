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
                    if (!string.IsNullOrWhiteSpace(solicitud.Comentario))
                        col.Item().Text($"Comentario del solicitante: {solicitud.Comentario}");

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
