using CatalogoPedidos.Application.Notificaciones;
using CatalogoPedidos.Application.Productos;
using CatalogoPedidos.Application.Usuarios;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Domain.Enums;

namespace CatalogoPedidos.Application.Solicitudes;

public class SolicitudService(
    ISolicitudRepository repositorio,
    IProductoRepository productos,
    INotificacionRepository notificaciones,
    INotificacionBroadcaster broadcaster,
    IGestorDirectory gestores) : ISolicitudService
{
    public async Task<SolicitudProducto> CrearAsync(string solicitanteId, string solicitanteNombre, CrearSolicitudDto dto, CancellationToken ct = default)
    {
        var producto = await productos.ObtenerPorIdAsync(dto.ProductoId, ct)
            ?? throw new InvalidOperationException("El producto seleccionado no existe.");

        if (dto.Cantidad <= 0)
            throw new InvalidOperationException("La cantidad debe ser mayor a cero.");

        var solicitud = new SolicitudProducto
        {
            ProductoId = producto.Id,
            Cantidad = dto.Cantidad,
            Comentario = dto.Comentario,
            SolicitanteId = solicitanteId,
            SolicitanteNombre = solicitanteNombre,
            Estado = EstadoSolicitud.Pendiente,
            FechaSolicitud = DateTime.UtcNow
        };

        var creada = await repositorio.CrearAsync(solicitud, ct);

        var idsGestores = await gestores.ObtenerIdsGestoresAsync(ct);
        foreach (var gestorId in idsGestores)
        {
            var notificacion = new Notificacion
            {
                UsuarioDestinoId = gestorId,
                Tipo = TipoNotificacion.SolicitudCreada,
                Titulo = "Nuevo pedido",
                Mensaje = $"{solicitanteNombre} solicitó {dto.Cantidad}x \"{producto.Nombre}\".",
                Url = "/solicitudes/bandeja"
            };

            var guardada = await notificaciones.CrearAsync(notificacion, ct);
            broadcaster.Publicar(guardada);
        }

        return creada;
    }

    public Task<List<SolicitudProducto>> ObtenerMisSolicitudesAsync(string solicitanteId, CancellationToken ct = default)
        => repositorio.ObtenerPorSolicitanteAsync(solicitanteId, ct);

    public Task<List<SolicitudProducto>> ObtenerBandejaAsync(CancellationToken ct = default)
        => repositorio.ObtenerPendientesAsync(ct);

    public Task<SolicitudProducto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
        => repositorio.ObtenerPorIdAsync(id, ct);

    public async Task ResolverAsync(int solicitudId, string gestorId, string gestorNombre, ResolverSolicitudDto dto, CancellationToken ct = default)
    {
        var solicitud = await repositorio.ObtenerPorIdAsync(solicitudId, ct)
            ?? throw new InvalidOperationException("La solicitud no existe.");

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
            throw new InvalidOperationException("Esta solicitud ya fue resuelta.");

        solicitud.Estado = dto.Aprobar ? EstadoSolicitud.Aprobada : EstadoSolicitud.Rechazada;
        solicitud.GestorId = gestorId;
        solicitud.GestorNombre = gestorNombre;
        solicitud.ComentarioGestor = dto.ComentarioGestor;
        solicitud.FechaResolucion = DateTime.UtcNow;

        await repositorio.ActualizarAsync(solicitud, ct);

        var aprobada = solicitud.Estado == EstadoSolicitud.Aprobada;
        var notificacion = new Notificacion
        {
            UsuarioDestinoId = solicitud.SolicitanteId,
            Tipo = TipoNotificacion.SolicitudResuelta,
            Titulo = aprobada ? "Pedido aprobado" : "Pedido rechazado",
            Mensaje = $"Tu pedido de \"{solicitud.Producto?.Nombre}\" fue {(aprobada ? "aprobado" : "rechazado")} por {gestorNombre}.",
            Url = "/solicitudes/mis-solicitudes"
        };

        var guardada = await notificaciones.CrearAsync(notificacion, ct);
        broadcaster.Publicar(guardada);
    }

    public Task<List<SolicitudProducto>> BuscarAsync(FiltroSolicitudesDto filtro, CancellationToken ct = default)
        => repositorio.BuscarAsync(filtro, ct);

    public Task<List<SolicitanteResumenDto>> ObtenerSolicitantesAsync(CancellationToken ct = default)
        => repositorio.ObtenerSolicitantesAsync(ct);
}
