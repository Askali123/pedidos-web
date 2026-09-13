using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Solicitudes;

public interface ISolicitudService
{
    Task<SolicitudProducto> CrearAsync(string solicitanteId, string solicitanteNombre, CrearSolicitudDto dto, CancellationToken ct = default);
    Task<List<SolicitudProducto>> ObtenerMisSolicitudesAsync(string solicitanteId, CancellationToken ct = default);
    Task<List<SolicitudProducto>> ObtenerBandejaAsync(CancellationToken ct = default);
    Task<SolicitudProducto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task ResolverAsync(int solicitudId, string gestorId, string gestorNombre, ResolverSolicitudDto dto, CancellationToken ct = default);

    Task<List<SolicitudProducto>> BuscarAsync(FiltroSolicitudesDto filtro, CancellationToken ct = default);
    Task<List<SolicitanteResumenDto>> ObtenerSolicitantesAsync(CancellationToken ct = default);
}
