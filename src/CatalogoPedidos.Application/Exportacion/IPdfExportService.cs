using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Exportacion;

public interface IPdfExportService
{
    byte[] ExportarSolicitud(SolicitudProducto solicitud);
    byte[] ExportarCatalogo(IEnumerable<Producto> productos);
    byte[] ExportarSolicitudes(IEnumerable<SolicitudProducto> solicitudes);
}
