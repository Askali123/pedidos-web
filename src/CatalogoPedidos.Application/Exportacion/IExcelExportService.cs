using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Exportacion;

public interface IExcelExportService
{
    byte[] ExportarSolicitudes(IEnumerable<DetalleSolicitud> solicitudes);
    byte[] ExportarSolicitudesPorProveedor(IEnumerable<DetalleSolicitud> solicitudes, IReadOnlyDictionary<int, string> codigosProveedorPorProducto, string proveedorNombre);
    byte[] ExportarCatalogoPorProveedor(IEnumerable<ProductoProveedor> asociaciones, string proveedorNombre);

    /// <summary>Igual que <see cref="ExportarSolicitudesPorProveedor"/> pero desde los SNAPSHOTS
    /// de un <see cref="PedidoProveedor"/>, no del catálogo vivo — para el adjunto del correo.</summary>
    byte[] ExportarPedidoProveedor(PedidoProveedor documento, string proveedorNombre);
}
