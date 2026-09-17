using CatalogoPedidos.Domain.Enums;

namespace CatalogoPedidos.Application.Solicitudes;

public class FiltroHistorialResolucionesDto
{
    public string? GestorId { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public EstadoSolicitud? Estado { get; set; }
}

public class GestorResumenDto
{
    public string Id { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}
