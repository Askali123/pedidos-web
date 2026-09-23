namespace CatalogoPedidos.Domain.Entities;

/// <summary>
/// Registro de que la Solicitud <see cref="SolicitudId"/> le fue notificada por correo a un
/// proveedor, con el detalle de las líneas de ESE proveedor dentro de la solicitud (una
/// solicitud puede tener productos de varios proveedores distintos, así que puede terminar
/// con varios registros — uno por proveedor notificado). Sirve de historial/auditoría de qué
/// se le mandó a quién y cuándo.
/// </summary>
public class NotificacionProveedor
{
    public int Id { get; set; }

    public int SolicitudId { get; set; }
    public Solicitud? Solicitud { get; set; }

    public int ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }

    /// <summary>Copia del email del proveedor al momento del envío (por si luego lo cambian).</summary>
    public string Email { get; set; } = string.Empty;

    public int CantidadLineas { get; set; }
    public DateTime FechaEnvio { get; set; } = DateTime.UtcNow;

    /// <summary>Quién disparó el envío — mismo patrón que GestorId/GestorNombre en SolicitudProducto.</summary>
    public string? GestorId { get; set; }
    public string? GestorNombre { get; set; }

    /// <summary>
    /// Documento <see cref="PedidoProveedor"/> del que este correo es un envío. Nullable solo
    /// para filas preexistentes al modelo nuevo; todo envío nuevo referencia su documento.
    /// </summary>
    public int? PedidoProveedorId { get; set; }
    public PedidoProveedor? PedidoProveedor { get; set; }
}
