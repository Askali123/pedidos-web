using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Notificaciones;

public class NotificacionService(INotificacionRepository repositorio) : INotificacionService
{
    public Task<List<Notificacion>> ObtenerRecientesAsync(string usuarioId, int limite = 20, CancellationToken ct = default)
        => repositorio.ObtenerPorUsuarioAsync(usuarioId, limite, ct);

    public Task<int> ContarNoLeidasAsync(string usuarioId, CancellationToken ct = default)
        => repositorio.ContarNoLeidasAsync(usuarioId, ct);

    public async Task MarcarLeidaAsync(int notificacionId, CancellationToken ct = default)
    {
        var notificacion = await repositorio.ObtenerPorIdAsync(notificacionId, ct);
        if (notificacion is null || notificacion.Leida)
            return;

        notificacion.Leida = true;
        await repositorio.ActualizarAsync(notificacion, ct);
    }

    public Task MarcarTodasLeidasAsync(string usuarioId, CancellationToken ct = default)
        => repositorio.MarcarTodasLeidasAsync(usuarioId, ct);
}
