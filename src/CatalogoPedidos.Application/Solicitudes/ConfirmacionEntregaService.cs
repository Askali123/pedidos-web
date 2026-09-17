using CatalogoPedidos.Application.Notificaciones;
using CatalogoPedidos.Application.Usuarios;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Domain.Enums;

namespace CatalogoPedidos.Application.Solicitudes;

public class ConfirmacionEntregaService(
    ISolicitudRepository solicitudes,
    IConfirmacionEntregaRepository confirmaciones,
    INotificacionRepository notificaciones,
    INotificacionBroadcaster broadcaster,
    IGestorDirectory gestores) : IConfirmacionEntregaService
{
    private const int MensajeMaxLength = 500;

    /// <summary>Tiempo aprobado sin confirmar entrega a partir del cual se manda el recordatorio.</summary>
    private static readonly TimeSpan UmbralEntregaPendiente = TimeSpan.FromHours(72);

    public Task<ConfirmacionEntrega?> ObtenerPorPedidoAsync(int pedidoId, CancellationToken ct = default)
        => confirmaciones.ObtenerPorPedidoAsync(pedidoId, ct);

    public async Task<ConfirmacionEntrega> ConfirmarAsync(int pedidoId, string gestorId, string gestorNombre, ConfirmarEntregaDto dto, CancellationToken ct = default)
    {
        var pedido = await solicitudes.ObtenerPedidoAsync(pedidoId, ct)
            ?? throw new InvalidOperationException("El pedido no existe.");

        // La UI ya oculta el botón en este caso (ver Bandeja/Administrar), pero el
        // servicio es la última línea de defensa — un pedido sin nada Aprobado no tiene
        // nada que entregar (ver docs/PLAN_TRAZABILIDAD_ENTREGAS.md #13).
        if (!pedido.Items.Any(i => i.Estado == EstadoSolicitud.Aprobada))
            throw new InvalidOperationException("Este pedido no tiene ninguna línea aprobada — no hay nada que entregar.");

        var existente = await confirmaciones.ObtenerPorPedidoAsync(pedidoId, ct);
        if (existente is not null)
            throw new InvalidOperationException("La entrega de este pedido ya fue confirmada.");

        var confirmacion = new ConfirmacionEntrega
        {
            PedidoId = pedido.Id,
            FechaEntrega = DateTime.UtcNow,
            ConfirmadoPorId = gestorId,
            ConfirmadoPorNombre = gestorNombre,
            DireccionEntregada = string.IsNullOrWhiteSpace(dto.DireccionEntregada) ? null : dto.DireccionEntregada.Trim(),
            CoincideConDireccionIndicada = dto.CoincideConDireccionIndicada,
            Observaciones = string.IsNullOrWhiteSpace(dto.Observaciones) ? null : dto.Observaciones.Trim()
        };

        var creada = await confirmaciones.CrearAsync(confirmacion, ct);
        await NotificarEntregaAsync(pedido, creada, ct);

        return creada;
    }

    private async Task NotificarEntregaAsync(Pedido pedido, ConfirmacionEntrega confirmacion, CancellationToken ct)
    {
        try
        {
            var mensaje = $"Tu pedido #{pedido.Id} fue entregado el {confirmacion.FechaEntrega:dd/MM/yyyy HH:mm}";
            mensaje += string.IsNullOrWhiteSpace(confirmacion.DireccionEntregada)
                ? "."
                : $" en \"{confirmacion.DireccionEntregada}\".";

            if (!confirmacion.CoincideConDireccionIndicada)
                mensaje += " La dirección de entrega no coincidió con la que indicaste.";

            var url = $"/solicitudes/mis-solicitudes#pedido-{pedido.Id}";
            await NotificarAsync(pedido.SolicitanteId, "Entrega confirmada", mensaje, url, TipoNotificacion.EntregaConfirmada, ct);
        }
        catch
        {
            // Best-effort: la entrega ya quedó confirmada aunque la notificación falle.
        }
    }

    public async Task EnviarRecordatoriosEntregaPendienteAsync(CancellationToken ct = default)
    {
        var candidatos = await solicitudes.ObtenerAprobadosSinEntregaAsync(ct);
        var limite = DateTime.UtcNow - UmbralEntregaPendiente;

        foreach (var pedido in candidatos)
        {
            // El reloj arranca desde la ÚLTIMA aprobación del pedido, no desde que se
            // creó — un pedido puede quedar con líneas pendientes mucho tiempo antes de
            // que se apruebe lo que sí hay que entregar (ver #19 del plan).
            var ultimaAprobacion = pedido.Items
                .Where(i => i.Estado == EstadoSolicitud.Aprobada)
                .Max(i => i.FechaResolucion);

            if (ultimaAprobacion is null || ultimaAprobacion > limite)
                continue;

            await NotificarRecordatorioEntregaAsync(pedido, ct);

            pedido.RecordatorioEntregaEnviado = true;
            await solicitudes.ActualizarPedidoAsync(pedido, ct);
        }
    }

    private async Task NotificarRecordatorioEntregaAsync(Pedido pedido, CancellationToken ct)
    {
        try
        {
            var horas = (int)UmbralEntregaPendiente.TotalHours;
            var aprobadas = pedido.Items.Count(i => i.Estado == EstadoSolicitud.Aprobada);
            var mensaje = $"El pedido #{pedido.Id} de {pedido.SolicitanteNombre} tiene {aprobadas} producto{(aprobadas == 1 ? "" : "s")} aprobado{(aprobadas == 1 ? "" : "s")} hace más de {horas}h sin confirmar la entrega.";

            var idsGestores = await gestores.ObtenerIdsGestoresAsync(ct);
            foreach (var gestorId in idsGestores)
                await NotificarAsync(gestorId, "Entrega pendiente de confirmar", mensaje, "/solicitudes/administrar", TipoNotificacion.RecordatorioEntregaPendiente, ct);
        }
        catch
        {
            // Best-effort: no bloquea marcar el pedido como recordado aunque falle el aviso.
        }
    }

    private async Task NotificarAsync(string usuarioDestinoId, string titulo, string mensaje, string url, TipoNotificacion tipo, CancellationToken ct)
    {
        if (mensaje.Length > MensajeMaxLength)
            mensaje = string.Concat(mensaje.AsSpan(0, MensajeMaxLength - 1), "…");

        var notificacion = new Notificacion
        {
            UsuarioDestinoId = usuarioDestinoId,
            Tipo = tipo,
            Titulo = titulo,
            Mensaje = mensaje,
            Url = url
        };

        var guardada = await notificaciones.CrearAsync(notificacion, ct);
        broadcaster.Publicar(guardada);
    }
}
