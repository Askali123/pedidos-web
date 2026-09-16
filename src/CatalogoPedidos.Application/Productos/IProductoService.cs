using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Productos;

public interface IProductoService
{
    Task<List<Producto>> ObtenerCatalogoAsync(string? texto = null, string? categoria = null, CancellationToken ct = default);
    Task<List<string>> ObtenerCategoriasAsync(CancellationToken ct = default);
    Task<Producto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Producto> CrearAsync(CrearProductoDto dto, CancellationToken ct = default);
    Task ActualizarAsync(int id, CrearProductoDto dto, CancellationToken ct = default);
    Task ActualizarStockMinimoAsync(int id, int? stockMinimo, CancellationToken ct = default);

    /// <summary>
    /// Baja lógica (no se borra el producto: conserva su historial de solicitudes). Deja de
    /// aparecer en el catálogo y en los selectores de nuevas solicitudes; sus solicitudes ya
    /// hechas no se ven afectadas.
    /// </summary>
    Task DesactivarAsync(int id, CancellationToken ct = default);
    Task<ResultadoAnalisisImportacion> AnalizarImportacionAsync(Stream archivo, int proveedorId, CancellationToken ct = default);
    Task<ImportarProductosResultado> ConfirmarImportacionAsync(List<FilaImportacion> filas, int proveedorId, CancellationToken ct = default);
}
