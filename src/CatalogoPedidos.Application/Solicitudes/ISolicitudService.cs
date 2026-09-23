using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Solicitudes;

public interface ISolicitudService
{
    Task<Solicitud> CrearSolicitudAsync(string solicitanteId, string solicitanteNombre, string? comentario, List<CrearSolicitudDto> items, string? direccionEntrega = null, CancellationToken ct = default);
    Task<List<DetalleSolicitud>> ObtenerMisSolicitudesAsync(string solicitanteId, CancellationToken ct = default);
    Task<List<DetalleSolicitud>> ObtenerBandejaAsync(CancellationToken ct = default);
    Task<DetalleSolicitud?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Solicitud?> ObtenerSolicitudAsync(int solicitudId, CancellationToken ct = default);
    Task ResolverAsync(int solicitudId, string gestorId, string gestorNombre, ResolverSolicitudDto dto, CancellationToken ct = default);
    Task<List<DetalleSolicitud>> ResolverSolicitudAsync(int solicitudId, string gestorId, string gestorNombre, ResolverSolicitudDto dto, CancellationToken ct = default);

    /// <summary>
    /// Resuelve varias líneas de la misma solicitud de un tirón, cada una con su propia
    /// decisión (aprobar/rechazar), y le manda al solicitante UNA sola notificación
    /// resumen en vez de una por línea (ver docs/PLAN_TRAZABILIDAD_ENTREGAS.md #10).
    /// </summary>
    Task<List<DetalleSolicitud>> ResolverVariasAsync(int solicitudId, string gestorId, string gestorNombre, ResolverVariasDto dto, CancellationToken ct = default);

    /// <summary>
    /// Revisa las solicitudes pendientes hace más de 48h y le avisa al gestor una sola vez
    /// por solicitud (no repite el recordatorio en cada corrida mientras siga sin resolverse).
    /// Pensado para llamarse periódicamente desde un job en segundo plano.
    /// </summary>
    Task EnviarRecordatoriosPendientesAsync(CancellationToken ct = default);

    Task<List<DetalleSolicitud>> BuscarAsync(FiltroSolicitudesDto filtro, CancellationToken ct = default);
    Task<List<SolicitanteResumenDto>> ObtenerSolicitantesAsync(CancellationToken ct = default);

    Task<List<DetalleSolicitud>> BuscarResueltasAsync(FiltroHistorialResolucionesDto filtro, CancellationToken ct = default);
    Task<List<GestorResumenDto>> ObtenerGestoresAsync(CancellationToken ct = default);
}
