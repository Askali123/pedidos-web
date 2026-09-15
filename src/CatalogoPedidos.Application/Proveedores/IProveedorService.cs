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

    /// <summary>
    /// Código de proveedor "de referencia" por producto (el preferido, o el primero si
    /// ninguno está marcado como tal), indexado por <c>ProductoId</c>. Pensado para mostrar
    /// una columna de código de proveedor en listas de solicitudes/pedidos sin depender de
    /// que el gestor haya filtrado por un proveedor específico.
    /// </summary>
    Task<Dictionary<int, ProductoProveedor>> ObtenerCodigosPreferidosAsync(IEnumerable<int> productoIds, CancellationToken ct = default);
    Task<List<ProductoProveedor>> ObtenerProductosDeProveedorAsync(
        int proveedorId,
        string? texto = null,
        string? categoria = null,
        bool soloActivos = false,
        CancellationToken ct = default);
    Task<ProductoProveedor> AsociarProveedorAsync(AsociarProveedorDto dto, CancellationToken ct = default);
    Task QuitarAsociacionAsync(int asociacionId, CancellationToken ct = default);
}
