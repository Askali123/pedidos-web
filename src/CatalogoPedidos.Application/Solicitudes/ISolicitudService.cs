using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Solicitudes;

public interface ISolicitudService
{
    Task<Pedido> CrearPedidoAsync(string solicitanteId, string solicitanteNombre, string? comentario, List<CrearSolicitudDto> items, CancellationToken ct = default);
    Task<List<SolicitudProducto>> ObtenerMisSolicitudesAsync(string solicitanteId, CancellationToken ct = default);
    Task<List<SolicitudProducto>> ObtenerBandejaAsync(CancellationToken ct = default);
    Task<SolicitudProducto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Pedido?> ObtenerPedidoAsync(int pedidoId, CancellationToken ct = default);
    Task ResolverAsync(int solicitudId, string gestorId, string gestorNombre, ResolverSolicitudDto dto, CancellationToken ct = default);
    Task<List<SolicitudProducto>> ResolverPedidoAsync(int pedidoId, string gestorId, string gestorNombre, ResolverSolicitudDto dto, CancellationToken ct = default);

    Task<List<SolicitudProducto>> BuscarAsync(FiltroSolicitudesDto filtro, CancellationToken ct = default);
    Task<List<SolicitanteResumenDto>> ObtenerSolicitantesAsync(CancellationToken ct = default);
}
