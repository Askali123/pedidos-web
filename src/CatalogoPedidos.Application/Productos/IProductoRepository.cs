using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Productos;

public interface IProductoRepository
{
    Task<List<Producto>> ObtenerCatalogoAsync(string? texto, string? categoria, CancellationToken ct = default);
    Task<Producto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Producto> CrearAsync(Producto producto, CancellationToken ct = default);
    Task ActualizarAsync(Producto producto, CancellationToken ct = default);
    Task AgregarRangoAsync(IEnumerable<Producto> productos, CancellationToken ct = default);
    Task<List<string>> ObtenerCategoriasAsync(CancellationToken ct = default);
}
