using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Solicitudes;

public interface ISolicitudService
{
    Task<Pedido> CrearPedidoAsync(string solicitanteId, string solicitanteNombre, string? comentario, List<CrearSolicitudDto> items, string? direccionEntrega = null, CancellationToken ct = default);
    Task<List<SolicitudProducto>> ObtenerMisSolicitudesAsync(string solicitanteId, CancellationToken ct = default);
    Task<List<SolicitudProducto>> ObtenerBandejaAsync(CancellationToken ct = default);
    Task<SolicitudProducto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Pedido?> ObtenerPedidoAsync(int pedidoId, CancellationToken ct = default);
    Task ResolverAsync(int solicitudId, string gestorId, string gestorNombre, ResolverSolicitudDto dto, CancellationToken ct = default);
    Task<List<SolicitudProducto>> ResolverPedidoAsync(int pedidoId, string gestorId, string gestorNombre, ResolverSolicitudDto dto, CancellationToken ct = default);

    /// <summary>
    /// Resuelve varias líneas del mismo pedido de un tirón, cada una con su propia
    /// decisión (aprobar/rechazar), y le manda al solicitante UNA sola notificación
    /// resumen en vez de una por línea (ver docs/PLAN_TRAZABILIDAD_ENTREGAS.md #10).
    /// </summary>
    Task<List<SolicitudProducto>> ResolverVariasAsync(int pedidoId, string gestorId, string gestorNombre, ResolverVariasDto dto, CancellationToken ct = default);

    /// <summary>
    /// Revisa los pedidos pendientes hace más de 48h y le avisa al gestor una sola vez por
    /// pedido (no repite el recordatorio en cada corrida mientras siga sin resolverse).
    /// Pensado para llamarse periódicamente desde un job en segundo plano.
    /// </summary>
    Task EnviarRecordatoriosPendientesAsync(CancellationToken ct = default);

    Task<List<SolicitudProducto>> BuscarAsync(FiltroSolicitudesDto filtro, CancellationToken ct = default);
    Task<List<SolicitanteResumenDto>> ObtenerSolicitantesAsync(CancellationToken ct = default);
}
