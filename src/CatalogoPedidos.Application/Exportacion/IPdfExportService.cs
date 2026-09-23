using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Exportacion;

public interface IPdfExportService
{
    byte[] ExportarPedido(Solicitud pedido);
    byte[] ExportarCatalogo(IEnumerable<Producto> productos);
    byte[] ExportarCatalogoPorProveedor(IEnumerable<ProductoProveedor> asociaciones, string proveedorNombre);
    byte[] ExportarSolicitudes(IEnumerable<DetalleSolicitud> solicitudes);
    byte[] ExportarSolicitudesPorProveedor(IEnumerable<DetalleSolicitud> solicitudes, IReadOnlyDictionary<int, string> codigosProveedorPorProducto, string proveedorNombre);

    /// <summary>
    /// Detalle de un <see cref="PedidoProveedor"/> a partir de SUS SNAPSHOTS
    /// (<see cref="DetallePedidoProveedor"/>), no del catálogo vivo — el adjunto del correo
    /// muestra exactamente lo que se le pidió al proveedor en ese momento (nombre, código
    /// de proveedor, cantidad y unidad congelados al envío). <paramref name="proveedorNombre"/>
    /// viaja aparte porque el documento puede estar construido sin la navegación cargada.
    /// </summary>
    byte[] ExportarPedidoProveedor(PedidoProveedor documento, string proveedorNombre);
}
