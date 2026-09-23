using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Solicitudes;

public interface INotificacionProveedorRepository
{
    Task<NotificacionProveedor> CrearAsync(NotificacionProveedor envio, CancellationToken ct = default);
    Task<List<NotificacionProveedor>> ObtenerPorSolicitudAsync(int solicitudId, CancellationToken ct = default);
    Task<List<NotificacionProveedor>> BuscarAsync(FiltroEnviosProveedorDto filtro, CancellationToken ct = default);
}
