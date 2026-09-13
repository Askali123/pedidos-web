using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Notificaciones;

public interface INotificacionRepository
{
    Task<Notificacion> CrearAsync(Notificacion notificacion, CancellationToken ct = default);
    Task<List<Notificacion>> ObtenerPorUsuarioAsync(string usuarioId, int limite = 20, CancellationToken ct = default);
    Task<int> ContarNoLeidasAsync(string usuarioId, CancellationToken ct = default);
    Task<Notificacion?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task ActualizarAsync(Notificacion notificacion, CancellationToken ct = default);
    Task MarcarTodasLeidasAsync(string usuarioId, CancellationToken ct = default);
}
