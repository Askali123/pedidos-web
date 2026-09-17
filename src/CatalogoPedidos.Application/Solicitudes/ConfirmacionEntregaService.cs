using CatalogoPedidos.Application.Notificaciones;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Domain.Enums;

namespace CatalogoPedidos.Application.Solicitudes;

public class ConfirmacionEntregaService(
    ISolicitudRepository solicitudes,
    IConfirmacionEntregaRepository confirmaciones,
    INotificacionRepository notificaciones,
    INotificacionBroadcaster broadcaster) : IConfirmacionEntregaService
{
    private const int MensajeMaxLength = 500;

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

            if (mensaje.Length > MensajeMaxLength)
                mensaje = string.Concat(mensaje.AsSpan(0, MensajeMaxLength - 1), "…");

            var notificacion = new Notificacion
            {
                UsuarioDestinoId = pedido.SolicitanteId,
                Tipo = TipoNotificacion.EntregaConfirmada,
                Titulo = "Entrega confirmada",
                Mensaje = mensaje,
                Url = $"/solicitudes/mis-solicitudes#pedido-{pedido.Id}"
            };

            var guardada = await notificaciones.CrearAsync(notificacion, ct);
            broadcaster.Publicar(guardada);
        }
        catch
        {
            // Best-effort: la entrega ya quedó confirmada aunque la notificación falle.
        }
    }
}
