using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Solicitudes;

public interface ISolicitudRepository
{
    Task<Pedido> CrearPedidoAsync(Pedido pedido, CancellationToken ct = default);
    Task<Pedido?> ObtenerPedidoAsync(int pedidoId, CancellationToken ct = default);
    Task ActualizarPedidoAsync(Pedido pedido, CancellationToken ct = default);
    Task<SolicitudProducto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<List<SolicitudProducto>> ObtenerPorSolicitanteAsync(string solicitanteId, CancellationToken ct = default);
    Task<List<SolicitudProducto>> ObtenerPendientesAsync(CancellationToken ct = default);
    Task ActualizarAsync(SolicitudProducto solicitud, CancellationToken ct = default);

    Task<List<SolicitudProducto>> BuscarAsync(FiltroSolicitudesDto filtro, CancellationToken ct = default);
    Task<List<SolicitanteResumenDto>> ObtenerSolicitantesAsync(CancellationToken ct = default);

    /// <summary>Historial de resoluciones (Aprobada/Rechazada) para auditoría del gestor — ver docs/PLAN_TRAZABILIDAD_ENTREGAS.md #17.</summary>
    Task<List<SolicitudProducto>> BuscarResueltasAsync(FiltroHistorialResolucionesDto filtro, CancellationToken ct = default);
    Task<List<GestorResumenDto>> ObtenerGestoresAsync(CancellationToken ct = default);

    /// <summary>
    /// Pedidos con al menos una línea Aprobada, sin <see cref="Domain.Entities.ConfirmacionEntrega"/>
    /// todavía y a los que no se les mandó ya el recordatorio — candidatos para
    /// <see cref="IConfirmacionEntregaService.EnviarRecordatoriosEntregaPendienteAsync"/>.
    /// </summary>
    Task<List<Pedido>> ObtenerAprobadosSinEntregaAsync(CancellationToken ct = default);
}
