namespace CatalogoPedidos.Domain.Entities;

/// <summary>
/// Cabecera de un pedido: agrupa uno o varios <see cref="SolicitudProducto"/> enviados
/// juntos desde el carrito. El estado (Pendiente/Aprobada/Rechazada) sigue viviendo por
/// línea — un gestor puede aprobar unos productos del pedido y rechazar otros — pero el
/// Pedido permite mostrarlos agrupados y guardar un único comentario para todo el envío.
/// </summary>
public class Pedido
{
    public int Id { get; set; }

    public string SolicitanteId { get; set; } = string.Empty;
    public string SolicitanteNombre { get; set; } = string.Empty;
    public string? Comentario { get; set; }

    /// <summary>
    /// Dónde hay que entregar los insumos de este pedido. Se autocompleta desde
    /// <c>ApplicationUser.DireccionPredeterminada</c> al armar el carrito, pero es un
    /// snapshot editable en ese momento — igual que <c>SolicitanteNombre</c>, un cambio
    /// posterior en el perfil no altera pedidos ya creados.
    /// </summary>
    public string? DireccionEntrega { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// True cuando ya se le avisó al gestor que este pedido lleva mucho tiempo pendiente
    /// (ver <see cref="Application.Solicitudes.ISolicitudService.EnviarRecordatoriosPendientesAsync"/>).
    /// Evita mandar el mismo recordatorio una y otra vez mientras siga sin resolverse.
    /// </summary>
    public bool RecordatorioEnviado { get; set; }

    /// <summary>
    /// True cuando ya se le avisó al gestor que este pedido tiene algo Aprobado hace
    /// mucho tiempo sin confirmar la entrega (ver
    /// <see cref="Application.Solicitudes.IConfirmacionEntregaService.EnviarRecordatoriosEntregaPendienteAsync"/>).
    /// Mismo criterio que <see cref="RecordatorioEnviado"/> pero para la otra punta del
    /// flujo — evita repetir el mismo aviso mientras siga sin confirmarse.
    /// </summary>
    public bool RecordatorioEntregaEnviado { get; set; }

    public ICollection<SolicitudProducto> Items { get; set; } = new List<SolicitudProducto>();

    /// <summary>Historial de a qué proveedores se les notificó este pedido por correo.</summary>
    public ICollection<NotificacionProveedor> NotificacionesProveedor { get; set; } = new List<NotificacionProveedor>();

    /// <summary>
    /// Confirmaciones de entrega de este pedido — una por proveedor al que se le mandó
    /// algo (más la eventual "sin proveedor asociado"), no una sola para todo el pedido
    /// (ver docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md, tarea 3).
    /// </summary>
    public ICollection<ConfirmacionEntrega> ConfirmacionesEntrega { get; set; } = new List<ConfirmacionEntrega>();
}
