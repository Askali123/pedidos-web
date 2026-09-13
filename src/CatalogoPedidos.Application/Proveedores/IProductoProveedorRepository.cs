using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Proveedores;

public interface IProductoProveedorRepository
{
    Task<List<ProductoProveedor>> ObtenerPorProductoAsync(int productoId, CancellationToken ct = default);
    Task<List<ProductoProveedor>> ObtenerPorProveedorAsync(int proveedorId, CancellationToken ct = default);
    Task<ProductoProveedor?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<bool> ExisteAsociacionAsync(int productoId, int proveedorId, CancellationToken ct = default);
    Task<bool> ExisteCodigoParaOtroProductoAsync(int proveedorId, string codigo, int productoId, CancellationToken ct = default);
    Task<ProductoProveedor> CrearAsync(ProductoProveedor asociacion, CancellationToken ct = default);
    Task EliminarAsync(int id, CancellationToken ct = default);
}
