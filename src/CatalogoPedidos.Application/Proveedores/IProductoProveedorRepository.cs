using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Proveedores;

public interface IProductoProveedorRepository
{
    Task<List<ProductoProveedor>> ObtenerPorProductoAsync(int productoId, CancellationToken ct = default);

    /// <summary>
    /// Catálogo de un proveedor específico. <paramref name="texto"/> busca por nombre/descripción
    /// del producto o por el código que ese proveedor le dio; <paramref name="soloActivos"/> no
    /// afecta la pantalla de gestión de asociaciones (que sí debe ver productos inactivos), solo
    /// se usa cuando este método sirve para alimentar el catálogo filtrado por proveedor.
    /// </summary>
    Task<List<ProductoProveedor>> ObtenerPorProveedorAsync(
        int proveedorId,
        string? texto = null,
        string? categoria = null,
        bool soloActivos = false,
        CancellationToken ct = default);
    Task<ProductoProveedor?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<ProductoProveedor?> ObtenerPorProveedorYCodigoAsync(int proveedorId, string codigo, CancellationToken ct = default);
    Task<bool> ExisteAsociacionAsync(int productoId, int proveedorId, CancellationToken ct = default);
    Task<bool> ExisteCodigoParaOtroProductoAsync(int proveedorId, string codigo, int productoId, CancellationToken ct = default);
    Task<ProductoProveedor> CrearAsync(ProductoProveedor asociacion, CancellationToken ct = default);
    Task ActualizarAsync(ProductoProveedor asociacion, CancellationToken ct = default);
    Task EliminarAsync(int id, CancellationToken ct = default);
}
