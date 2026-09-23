using CatalogoPedidos.Application.Proveedores;
using CatalogoPedidos.Application.Solicitudes;

namespace CatalogoPedidos.Application.Reportes;

public class ConsumoEmpresaService(
    ISolicitudRepository solicitudes,
    IPedidoProveedorRepository pedidosProveedor,
    IProveedorService proveedores) : IConsumoEmpresaService
{
    public async Task<List<ConsumoDetalleDto>> ObtenerConsumoAsync(FiltroSolicitudesDto filtro, CancellationToken ct = default)
    {
        var items = await solicitudes.BuscarAsync(filtro, ct);
        if (items.Count == 0)
            return [];

        var solicitudIds = items.Select(i => i.SolicitudId).Distinct().ToList();
        var documentos = await pedidosProveedor.ObtenerPorSolicitudesAsync(solicitudIds, ct);

        // Código con el que REALMENTE se pidió cada línea — snapshot congelado en el
        // momento del envío, indexado por (SolicitudId, ProductoId). Más preciso que el
        // preferido porque no depende de que la asociación del catálogo siga igual hoy.
        var codigosEnviados = new Dictionary<(int SolicitudId, int ProductoId), string>();
        foreach (var doc in documentos)
        {
            foreach (var linea in doc.Items.Where(l => !l.Excluido))
            {
                var clave = (doc.SolicitudId, linea.ProductoId);
                codigosEnviados.TryAdd(clave, linea.CodigoProveedor);
            }
        }

        // Para lo que todavía no se envió a ningún proveedor, el código del preferido del
        // catálogo, solo como referencia (puede cambiar si se reasigna después).
        var productoIds = items.Select(i => i.ProductoId).Distinct().ToList();
        var preferidos = await proveedores.ObtenerCodigosPreferidosAsync(productoIds, ct);

        return items.Select(i => new ConsumoDetalleDto
        {
            SolicitudId = i.SolicitudId,
            DetalleId = i.Id,
            Fecha = i.FechaSolicitud,
            SolicitanteNombre = i.SolicitanteNombre,
            EmpresaNombre = i.Solicitud?.EmpresaNombre,
            SedeNombre = i.Solicitud?.SedeNombre,
            ProductoNombre = i.Producto?.Nombre ?? string.Empty,
            Categoria = i.Producto?.Categoria,
            CodigoInterno = i.ProductoId,
            CodigoProveedor = codigosEnviados.TryGetValue((i.SolicitudId, i.ProductoId), out var codigoEnviado)
                ? codigoEnviado
                : preferidos.TryGetValue(i.ProductoId, out var preferido) ? preferido.CodigoProveedor : null,
            Cantidad = i.Cantidad,
            Estado = i.Estado
        }).ToList();
    }
}
