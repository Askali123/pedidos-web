namespace CatalogoPedidos.Domain.Entities;

/// <summary>
/// Cabecera de un pedido hecho a UN proveedor concreto, a partir de una <see cref="Solicitud"/>
/// (lo que pidió el usuario) y de la asociación <see cref="ProductoProveedor"/> del catálogo.
///
/// Es el documento persistente que reemplaza al envío efímero anterior (que solo dejaba una
/// <see cref="NotificacionProveedor"/> con un conteo): guarda qué líneas, con qué códigos y
/// con qué cantidades se le pidieron a cada proveedor, para poder auditar el pasado aunque el
/// catálogo cambie después — ver <see cref="DetallePedidoProveedor"/>, que congela los datos
/// como snapshot.
///
/// Se crea de forma inmutable al primer envío: mismo pedido + proveedor reutiliza el MISMO
/// documento (un índice único lo garantiza), y un reenvío solo agrega una
/// <see cref="NotificacionProveedor"/> nueva, no otro documento — ver
/// docs/PLAN_PEDIDO_PROVEEDOR.md, sección 5.
/// </summary>
public class PedidoProveedor
{
    public int Id { get; set; }

    public int SolicitudId { get; set; }
    public Solicitud? Solicitud { get; set; }

    public int ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    /// <summary>Quién disparó el pedido — mismo patrón que GestorId/GestorNombre en <see cref="DetalleSolicitud"/>.</summary>
    public string? GestorId { get; set; }
    public string? GestorNombre { get; set; }

    public ICollection<DetallePedidoProveedor> Items { get; set; } = new List<DetallePedidoProveedor>();

    /// <summary>Cada correo con el que se le notificó este pedido al proveedor.</summary>
    public ICollection<NotificacionProveedor> Notificaciones { get; set; } = new List<NotificacionProveedor>();
}