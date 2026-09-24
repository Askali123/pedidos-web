using CatalogoPedidos.Application.Solicitudes;

namespace CatalogoPedidos.Application.Reportes;

public interface IConsumoEmpresaService
{
    /// <summary>Filtro combinable por Empresa/Sede/Año/Mes/rango de fechas (ver <see cref="FiltroSolicitudesDto"/>).</summary>
    Task<List<ConsumoDetalleDto>> ObtenerConsumoAsync(FiltroSolicitudesDto filtro, CancellationToken ct = default);
}
