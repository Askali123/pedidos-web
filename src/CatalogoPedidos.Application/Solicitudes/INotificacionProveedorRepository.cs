using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Solicitudes;

public interface INotificacionProveedorRepository
{
    Task<NotificacionProveedor> CrearAsync(NotificacionProveedor envio, CancellationToken ct = default);
    Task<List<NotificacionProveedor>> ObtenerPorPedidoAsync(int pedidoId, CancellationToken ct = default);
}
