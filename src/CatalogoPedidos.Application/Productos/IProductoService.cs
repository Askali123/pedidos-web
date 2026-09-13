using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Productos;

public interface IProductoService
{
    Task<List<Producto>> ObtenerCatalogoAsync(string? texto = null, string? categoria = null, CancellationToken ct = default);
    Task<List<string>> ObtenerCategoriasAsync(CancellationToken ct = default);
    Task<Producto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Producto> CrearAsync(CrearProductoDto dto, CancellationToken ct = default);
    Task ActualizarAsync(int id, CrearProductoDto dto, CancellationToken ct = default);
    Task<ImportarProductosResultado> ImportarDesdeArchivoAsync(Stream archivo, CancellationToken ct = default);
}
