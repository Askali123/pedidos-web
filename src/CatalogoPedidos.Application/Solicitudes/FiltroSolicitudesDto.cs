using CatalogoPedidos.Domain.Enums;

namespace CatalogoPedidos.Application.Solicitudes;

public class FiltroSolicitudesDto
{
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public string? SolicitanteId { get; set; }
    public int? ProductoId { get; set; }
    public EstadoSolicitud? Estado { get; set; }
}

public class SolicitanteResumenDto
{
    public string Id { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}
