using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Solicitudes;

public interface ISolicitudRepository
{
    Task<Pedido> CrearPedidoAsync(Pedido pedido, CancellationToken ct = default);
    Task<Pedido?> ObtenerPedidoAsync(int pedidoId, CancellationToken ct = default);
    Task<SolicitudProducto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<List<SolicitudProducto>> ObtenerPorSolicitanteAsync(string solicitanteId, CancellationToken ct = default);
    Task<List<SolicitudProducto>> ObtenerPendientesAsync(CancellationToken ct = default);
    Task ActualizarAsync(SolicitudProducto solicitud, CancellationToken ct = default);

    Task<List<SolicitudProducto>> BuscarAsync(FiltroSolicitudesDto filtro, CancellationToken ct = default);
    Task<List<SolicitanteResumenDto>> ObtenerSolicitantesAsync(CancellationToken ct = default);
}
