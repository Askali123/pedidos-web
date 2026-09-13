using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Notificaciones;

public interface INotificacionService
{
    Task<List<Notificacion>> ObtenerRecientesAsync(string usuarioId, int limite = 20, CancellationToken ct = default);
    Task<int> ContarNoLeidasAsync(string usuarioId, CancellationToken ct = default);
    Task MarcarLeidaAsync(int notificacionId, CancellationToken ct = default);
    Task MarcarTodasLeidasAsync(string usuarioId, CancellationToken ct = default);
}
