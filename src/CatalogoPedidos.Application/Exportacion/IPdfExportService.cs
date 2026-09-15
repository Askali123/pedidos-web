using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Exportacion;

public interface IPdfExportService
{
    byte[] ExportarSolicitud(SolicitudProducto solicitud);
    byte[] ExportarPedido(Pedido pedido);
    byte[] ExportarCatalogo(IEnumerable<Producto> productos);
    byte[] ExportarCatalogoPorProveedor(IEnumerable<ProductoProveedor> asociaciones, string proveedorNombre);
    byte[] ExportarSolicitudes(IEnumerable<SolicitudProducto> solicitudes);
}
