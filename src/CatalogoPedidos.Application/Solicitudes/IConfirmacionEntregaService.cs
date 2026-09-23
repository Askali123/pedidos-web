using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Solicitudes;

public interface IConfirmacionEntregaService
{
    /// <summary>Todas las confirmaciones del pedido — una por proveedor (más la eventual "sin proveedor").</summary>
    Task<List<ConfirmacionEntrega>> ObtenerPorPedidoAsync(int pedidoId, CancellationToken ct = default);

    /// <summary>
    /// En qué "grupos" se divide la entrega de este pedido — uno por proveedor al que
    /// realmente se le envió algo, más el grupo "sin proveedor" si queda algo Aprobado sin
    /// cubrir por ningún envío — y cuáles de esos grupos ya están confirmados. Base tanto
    /// para ofrecer un botón "Confirmar entrega" por grupo pendiente (tarea 5) como para
    /// <see cref="ObtenerProgresoAsync"/>.
    /// </summary>
    Task<List<GrupoEntregaDto>> ObtenerGruposDeEntregaAsync(int pedidoId, CancellationToken ct = default);

    /// <summary>Resumen del progreso de entrega del pedido (cuántos grupos hay y cuántos ya se confirmaron) — para mostrar "2/3 entregados" en vez de un único ✓/nada.</summary>
    Task<EntregaProgresoDto> ObtenerProgresoAsync(int pedidoId, CancellationToken ct = default);

    /// <summary>
    /// Deja registrada la entrega de las líneas Aprobadas del grupo indicado por
    /// <paramref name="proveedorId"/> (<c>null</c> = grupo "sin proveedor asociado"). Ese
    /// grupo tiene que existir (líneas Aprobadas sin cubrir todavía) y no tener ya una
    /// confirmación — si no, tira <see cref="InvalidOperationException"/>.
    /// </summary>
    Task<ConfirmacionEntrega> ConfirmarAsync(int pedidoId, int? proveedorId, string gestorId, string gestorNombre, ConfirmarEntregaDto dto, CancellationToken ct = default);

    /// <summary>
    /// Revisa los pedidos con algo Aprobado hace más de 72h sin confirmar la entrega y le
    /// avisa a los gestores una sola vez por pedido (no repite mientras siga sin
    /// confirmarse). Pensado para llamarse periódicamente desde un job en segundo plano —
    /// mismo espíritu que <see cref="ISolicitudService.EnviarRecordatoriosPendientesAsync"/>.
    /// </summary>
    Task EnviarRecordatoriosEntregaPendienteAsync(CancellationToken ct = default);
}
