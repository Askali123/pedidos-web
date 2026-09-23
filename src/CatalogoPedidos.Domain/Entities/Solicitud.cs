namespace CatalogoPedidos.Domain.Entities;

/// <summary>
/// Cabecera de una solicitud: agrupa uno o varios <see cref="DetalleSolicitud"/> enviados
/// juntos desde el carrito. El estado (Pendiente/Aprobada/Rechazada) sigue viviendo por
/// línea — un gestor puede aprobar unos productos de la solicitud y rechazar otros — pero
/// la Solicitud permite mostrarlos agrupados y guardar un único comentario para todo el envío.
///
/// Antes se llamaba <c>Pedido</c> — se renombró (2026-09-22) para separarla del "pedido"
/// real que el gestor le hace a UN proveedor concreto (ver <see cref="PedidoProveedor"/>):
/// la Solicitud es lo que el usuario pide, el Pedido es lo que se le compra a un proveedor
/// a partir de esa solicitud (ver docs/PLAN_PEDIDO_PROVEEDOR.md, sección 11).
/// </summary>
public class Solicitud
{
    public int Id { get; set; }

    public string SolicitanteId { get; set; } = string.Empty;
    public string SolicitanteNombre { get; set; } = string.Empty;
    public string? Comentario { get; set; }

    /// <summary>
    /// Dónde hay que entregar los insumos de esta solicitud. Se autocompleta desde
    /// <c>ApplicationUser.DireccionPredeterminada</c> al armar el carrito, pero es un
    /// snapshot editable en ese momento — igual que <c>SolicitanteNombre</c>, un cambio
    /// posterior en el perfil no altera solicitudes ya creadas.
    /// </summary>
    public string? DireccionEntrega { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// True cuando ya se le avisó al gestor que esta solicitud lleva mucho tiempo pendiente
    /// (ver <see cref="Application.Solicitudes.ISolicitudService.EnviarRecordatoriosPendientesAsync"/>).
    /// Evita mandar el mismo recordatorio una y otra vez mientras siga sin resolverse.
    /// </summary>
    public bool RecordatorioEnviado { get; set; }

    /// <summary>
    /// True cuando ya se le avisó al gestor que esta solicitud tiene algo Aprobado hace
    /// mucho tiempo sin confirmar la entrega (ver
    /// <see cref="Application.Solicitudes.IConfirmacionEntregaService.EnviarRecordatoriosEntregaPendienteAsync"/>).
    /// Mismo criterio que <see cref="RecordatorioEnviado"/> pero para la otra punta del
    /// flujo — evita repetir el mismo aviso mientras siga sin confirmarse.
    /// </summary>
    public bool RecordatorioEntregaEnviado { get; set; }

    public ICollection<DetalleSolicitud> Items { get; set; } = new List<DetalleSolicitud>();

    /// <summary>Historial de a qué proveedores se les notificó esta solicitud por correo.</summary>
    public ICollection<NotificacionProveedor> NotificacionesProveedor { get; set; } = new List<NotificacionProveedor>();

    /// <summary>
    /// Documento(s) de pedido persistido(s) por proveedor — uno por (Solicitud, Proveedor).
    /// Una solicitud con productos de varios proveedores termina con varios (ver
    /// <see cref="NotificacionProveedor"/>).
    /// </summary>
    public ICollection<PedidoProveedor> PedidosProveedor { get; set; } = new List<PedidoProveedor>();

    /// <summary>
    /// Confirmaciones de entrega de esta solicitud — una por proveedor al que se le mandó
    /// algo (más la eventual "sin proveedor asociado"), no una sola para toda la solicitud
    /// (ver docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md, tarea 3).
    /// </summary>
    public ICollection<ConfirmacionEntrega> ConfirmacionesEntrega { get; set; } = new List<ConfirmacionEntrega>();
}
