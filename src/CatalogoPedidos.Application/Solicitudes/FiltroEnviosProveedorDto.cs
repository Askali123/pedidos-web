namespace CatalogoPedidos.Application.Solicitudes;

public class FiltroEnviosProveedorDto
{
    public int? ProveedorId { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
}
