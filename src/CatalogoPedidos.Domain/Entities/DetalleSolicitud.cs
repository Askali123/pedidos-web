using CatalogoPedidos.Domain.Enums;

namespace CatalogoPedidos.Domain.Entities;

/// <summary>Línea de una <see cref="Solicitud"/> — antes se llamaba <c>SolicitudProducto</c>.</summary>
public class DetalleSolicitud
{
    public int Id { get; set; }

    public int SolicitudId { get; set; }
    public Solicitud? Solicitud { get; set; }

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public int Cantidad { get; set; }

    public string SolicitanteId { get; set; } = string.Empty;
    public string SolicitanteNombre { get; set; } = string.Empty;

    public string? GestorId { get; set; }
    public string? GestorNombre { get; set; }
    public string? ComentarioGestor { get; set; }

    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;
    public DateTime? FechaResolucion { get; set; }
}
