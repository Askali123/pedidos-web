using CatalogoPedidos.Domain.Enums;

namespace CatalogoPedidos.Application.Solicitudes;

public class FiltroSolicitudesDto
{
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public string? SolicitanteId { get; set; }
    public int? ProductoId { get; set; }
    public int? ProveedorId { get; set; }
    public EstadoSolicitud? Estado { get; set; }

    /// <summary>Filtros del dashboard de consumo por empresa/sede — ver docs/PLAN_EMPRESAS_FILIALES.md, Etapa 6.</summary>
    public int? EmpresaId { get; set; }
    public int? SedeId { get; set; }
    public int? Anio { get; set; }
    public int? Mes { get; set; }
}

public class SolicitanteResumenDto
{
    public string Id { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}
