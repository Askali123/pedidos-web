using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Exportacion;

public interface IExcelExportService
{
    byte[] ExportarSolicitudes(IEnumerable<SolicitudProducto> solicitudes);
}
