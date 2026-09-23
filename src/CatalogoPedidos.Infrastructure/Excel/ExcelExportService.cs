using CatalogoPedidos.Application.Exportacion;
using CatalogoPedidos.Domain.Entities;
using ClosedXML.Excel;

namespace CatalogoPedidos.Infrastructure.Excel;

public class ExcelExportService : IExcelExportService
{
    public byte[] ExportarSolicitudes(IEnumerable<DetalleSolicitud> solicitudes)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Solicitudes");

        string[] encabezados = ["N.º", "Solicitud", "Producto", "Categoría", "Cantidad", "Solicitante", "Fecha solicitud", "Estado", "Gestor", "Fecha resolución", "Comentario", "Comentario gestor"];
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
            hoja.Cell(fila, 2).Value = s.SolicitudId;
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
            hoja.Cell(fila, 11).Value = s.Solicitud?.Comentario;
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

    public byte[] ExportarSolicitudesPorProveedor(IEnumerable<DetalleSolicitud> solicitudes, IReadOnlyDictionary<int, string> codigosProveedorPorProducto, string proveedorNombre)
    {
        // "Detalle por proveedor", no "Pedido a proveedor" (ese título queda reservado para
        // ExportarPedidoProveedor, el documento real de UN PedidoProveedor): este export lo
        // usa tanto el envío real de una solicitud puntual como el reporte filtrado por
        // proveedor de Administrar solicitudes, que puede traer líneas de varias solicitudes
        // a la vez (columna "Solicitud" abajo).
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Detalle por proveedor");

        hoja.Cell(1, 1).Value = $"Proveedor: {proveedorNombre}";
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;

        string[] encabezados = ["Código interno", "Código proveedor", "Producto", "Categoría", "Cantidad", "Solicitante", "Fecha solicitud", "Estado", "Solicitud", "Comentario"];
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
            hoja.Cell(fila, 9).Value = s.SolicitudId;
            hoja.Cell(fila, 10).Value = s.Solicitud?.Comentario;
            fila++;
        }

        hoja.Columns().AdjustToContents();
        hoja.SheetView.FreezeRows(3);
        hoja.Range(3, 1, 3, encabezados.Length).SetAutoFilter();

        using var stream = new MemoryStream();
        libro.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportarCatalogoPorProveedor(IEnumerable<ProductoProveedor> asociaciones, string proveedorNombre)
    {
        var lista = asociaciones.ToList();

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Catálogo por proveedor");

        hoja.Cell(1, 1).Value = $"Catálogo de productos — Proveedor: {proveedorNombre}";
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;

        string[] encabezados = ["Código interno", "Código proveedor", "Producto", "Categoría", "Precio proveedor", "Stock"];
        for (var i = 0; i < encabezados.Length; i++)
        {
            var celda = hoja.Cell(3, i + 1);
            celda.Value = encabezados[i];
            celda.Style.Font.Bold = true;
            celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B1F27");
            celda.Style.Font.FontColor = XLColor.White;
        }

        var fila = 4;
        foreach (var a in lista)
        {
            hoja.Cell(fila, 1).Value = a.ProductoId;
            hoja.Cell(fila, 2).Value = a.CodigoProveedor;
            hoja.Cell(fila, 3).Value = a.Producto?.Nombre;
            hoja.Cell(fila, 4).Value = a.Producto?.Categoria;
            hoja.Cell(fila, 5).Value = a.PrecioProveedor ?? a.Producto?.Precio ?? 0;
            hoja.Cell(fila, 5).Style.NumberFormat.Format = "#,##0.00";
            hoja.Cell(fila, 6).Value = a.Producto?.Stock ?? 0;
            fila++;
        }

        hoja.Columns().AdjustToContents();
        hoja.SheetView.FreezeRows(3);
        hoja.Range(3, 1, 3, encabezados.Length).SetAutoFilter();

        using var stream = new MemoryStream();
        libro.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportarPedidoProveedor(PedidoProveedor documento, string proveedorNombre)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Solicitud a proveedor");

        hoja.Cell(1, 1).Value = $"Solicitud N.º {documento.SolicitudId} — {proveedorNombre}";
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;
        if (documento.Solicitud is not null)
        {
            hoja.Cell(2, 1).Value = $"Solicitado por: {documento.Solicitud.SolicitanteNombre}";
            hoja.Cell(2, 1).Style.Font.FontSize = 10;
            hoja.Cell(2, 2).Value = documento.Solicitud.FechaCreacion.ToLocalTime();
            hoja.Cell(2, 2).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
        }

        string[] encabezados = ["Código interno", "Código proveedor", "Producto", "Cantidad", "Unidad", "Precio"];
        for (var i = 0; i < encabezados.Length; i++)
        {
            var celda = hoja.Cell(3, i + 1);
            celda.Value = encabezados[i];
            celda.Style.Font.Bold = true;
            celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B1F27");
            celda.Style.Font.FontColor = XLColor.White;
        }

        var fila = 4;
        foreach (var d in documento.Items)
        {
            hoja.Cell(fila, 1).Value = d.ProductoId;
            hoja.Cell(fila, 2).Value = d.CodigoProveedor;
            hoja.Cell(fila, 3).Value = d.ProductoNombre;
            hoja.Cell(fila, 4).Value = d.Cantidad;
            hoja.Cell(fila, 5).Value = d.UnidadMedida ?? "-";
            hoja.Cell(fila, 6).Value = d.PrecioProveedor ?? 0;
            hoja.Cell(fila, 6).Style.NumberFormat.Format = "#,##0.00";
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
