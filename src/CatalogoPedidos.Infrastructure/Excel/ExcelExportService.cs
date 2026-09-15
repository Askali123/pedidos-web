using CatalogoPedidos.Application.Exportacion;
using CatalogoPedidos.Domain.Entities;
using ClosedXML.Excel;

namespace CatalogoPedidos.Infrastructure.Excel;

public class ExcelExportService : IExcelExportService
{
    public byte[] ExportarSolicitudes(IEnumerable<SolicitudProducto> solicitudes)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Solicitudes");

        string[] encabezados = ["N.º", "Pedido", "Producto", "Categoría", "Cantidad", "Solicitante", "Fecha solicitud", "Estado", "Gestor", "Fecha resolución", "Comentario", "Comentario gestor"];
        for (var i = 0; i < encabezados.Length; i++)
        {
            var celda = hoja.Cell(1, i + 1);
            celda.Value = encabezados[i];
            celda.Style.Font.Bold = true;
            celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B1F27");
            celda.Style.Font.FontColor = XLColor.White;
        }

        var fila = 2;
        foreach (var s in solicitudes)
        {
            hoja.Cell(fila, 1).Value = s.Id;
            hoja.Cell(fila, 2).Value = s.PedidoId;
            hoja.Cell(fila, 3).Value = s.Producto?.Nombre;
            hoja.Cell(fila, 4).Value = s.Producto?.Categoria;
            hoja.Cell(fila, 5).Value = s.Cantidad;
            hoja.Cell(fila, 6).Value = s.SolicitanteNombre;
            hoja.Cell(fila, 7).Value = s.FechaSolicitud.ToLocalTime();
            hoja.Cell(fila, 7).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            hoja.Cell(fila, 8).Value = s.Estado.ToString();
            hoja.Cell(fila, 9).Value = s.GestorNombre;
            if (s.FechaResolucion is not null)
            {
                hoja.Cell(fila, 10).Value = s.FechaResolucion.Value.ToLocalTime();
                hoja.Cell(fila, 10).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            }
            hoja.Cell(fila, 11).Value = s.Pedido?.Comentario;
            hoja.Cell(fila, 12).Value = s.ComentarioGestor;
            fila++;
        }

        hoja.Columns().AdjustToContents();
        hoja.SheetView.FreezeRows(1);
        hoja.RangeUsed()?.SetAutoFilter();

        using var stream = new MemoryStream();
        libro.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportarSolicitudesPorProveedor(IEnumerable<SolicitudProducto> solicitudes, IReadOnlyDictionary<int, string> codigosProveedorPorProducto, string proveedorNombre)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Pedido a proveedor");

        hoja.Cell(1, 1).Value = $"Proveedor: {proveedorNombre}";
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;

        string[] encabezados = ["Código interno", "Código proveedor", "Producto", "Categoría", "Cantidad", "Solicitante", "Fecha solicitud", "Estado", "Pedido", "Comentario"];
        for (var i = 0; i < encabezados.Length; i++)
        {
            var celda = hoja.Cell(3, i + 1);
            celda.Value = encabezados[i];
            celda.Style.Font.Bold = true;
            celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B1F27");
            celda.Style.Font.FontColor = XLColor.White;
        }

        var fila = 4;
        foreach (var s in solicitudes)
        {
            hoja.Cell(fila, 1).Value = s.ProductoId;
            hoja.Cell(fila, 2).Value = codigosProveedorPorProducto.GetValueOrDefault(s.ProductoId, "-");
            hoja.Cell(fila, 3).Value = s.Producto?.Nombre;
            hoja.Cell(fila, 4).Value = s.Producto?.Categoria;
            hoja.Cell(fila, 5).Value = s.Cantidad;
            hoja.Cell(fila, 6).Value = s.SolicitanteNombre;
            hoja.Cell(fila, 7).Value = s.FechaSolicitud.ToLocalTime();
            hoja.Cell(fila, 7).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            hoja.Cell(fila, 8).Value = s.Estado.ToString();
            hoja.Cell(fila, 9).Value = s.PedidoId;
            hoja.Cell(fila, 10).Value = s.Pedido?.Comentario;
            fila++;
        }

        hoja.Columns().AdjustToContents();
        hoja.SheetView.FreezeRows(3);
        hoja.Range(3, 1, 3, encabezados.Length).SetAutoFilter();

        using var stream = new MemoryStream();
        libro.SaveAs(stream);
        return stream.ToArray();
    }
}
