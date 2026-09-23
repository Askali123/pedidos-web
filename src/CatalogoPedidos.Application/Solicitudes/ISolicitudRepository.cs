using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Solicitudes;

public interface ISolicitudRepository
{
    Task<Solicitud> CrearSolicitudAsync(Solicitud solicitud, CancellationToken ct = default);
    Task<Solicitud?> ObtenerSolicitudAsync(int solicitudId, CancellationToken ct = default);
    Task ActualizarSolicitudAsync(Solicitud solicitud, CancellationToken ct = default);
    Task<DetalleSolicitud?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<List<DetalleSolicitud>> ObtenerPorSolicitanteAsync(string solicitanteId, CancellationToken ct = default);
    Task<List<DetalleSolicitud>> ObtenerPendientesAsync(CancellationToken ct = default);
    Task ActualizarAsync(DetalleSolicitud solicitud, CancellationToken ct = default);

    Task<List<DetalleSolicitud>> BuscarAsync(FiltroSolicitudesDto filtro, CancellationToken ct = default);
    Task<List<SolicitanteResumenDto>> ObtenerSolicitantesAsync(CancellationToken ct = default);

    /// <summary>Historial de resoluciones (Aprobada/Rechazada) para auditoría del gestor — ver docs/PLAN_TRAZABILIDAD_ENTREGAS.md #17.</summary>
    Task<List<DetalleSolicitud>> BuscarResueltasAsync(FiltroHistorialResolucionesDto filtro, CancellationToken ct = default);
    Task<List<GestorResumenDto>> ObtenerGestoresAsync(CancellationToken ct = default);

    /// <summary>
    /// Solicitudes con al menos una línea Aprobada, sin <see cref="Domain.Entities.ConfirmacionEntrega"/>
    /// todavía y a los que no se les mandó ya el recordatorio — candidatos para
    /// <see cref="IConfirmacionEntregaService.EnviarRecordatoriosEntregaPendienteAsync"/>.
    /// </summary>
    Task<List<Solicitud>> ObtenerAprobadosSinEntregaAsync(CancellationToken ct = default);
}
