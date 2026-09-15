using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Proveedores;

public interface IProveedorService
{
    Task<List<Proveedor>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Proveedor?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Proveedor> CrearAsync(CrearProveedorDto dto, CancellationToken ct = default);
    Task ActualizarAsync(int id, CrearProveedorDto dto, CancellationToken ct = default);
    Task DesactivarAsync(int id, CancellationToken ct = default);

    Task<List<ProductoProveedor>> ObtenerProveedoresDeProductoAsync(int productoId, CancellationToken ct = default);
    Task<List<ProductoProveedor>> ObtenerProductosDeProveedorAsync(int proveedorId, CancellationToken ct = default);
    Task<ProductoProveedor> AsociarProveedorAsync(AsociarProveedorDto dto, CancellationToken ct = default);
    Task QuitarAsociacionAsync(int asociacionId, CancellationToken ct = default);
}
