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

    /// <summary>
    /// Cuántos proveedores distintos tiene asociado cada producto, indexado por
    /// <c>ProductoId</c> (productos sin ninguno no aparecen en el resultado). Pensado para
    /// avisar en la UI cuando el código "de referencia" que se muestra (el preferido) no es
    /// la única opción — hay más proveedores entre los que elegir al enviar el pedido.
    /// </summary>
    Task<Dictionary<int, int>> ContarProveedoresPorProductoAsync(IEnumerable<int> productoIds, CancellationToken ct = default);
    Task<List<ProductoProveedor>> ObtenerProductosDeProveedorAsync(
        int proveedorId,
        string? texto = null,
        string? categoria = null,
        bool soloActivos = false,
        CancellationToken ct = default);
    Task<ProductoProveedor> AsociarProveedorAsync(AsociarProveedorDto dto, CancellationToken ct = default);

    /// <summary>
    /// Edita una asociación existente (código, precio, preferido) sin tocar su estado
    /// Activo — para corregir/ajustar, no para quitar (ver DesactivarAsociacionAsync).
    /// </summary>
    Task ActualizarAsociacionAsync(int asociacionId, AsociarProveedorDto dto, CancellationToken ct = default);

    /// <summary>
    /// "Quita" la asociación desactivándola (no borra la fila): conserva el histórico —
    /// los PedidoProveedor ya emitidos guardan sus snapshots y no dependen de esto; solo
    /// los pedidos NUEVOS dejan de ofrecer este proveedor para este producto.
    /// </summary>
    Task DesactivarAsociacionAsync(int asociacionId, CancellationToken ct = default);
    Task ReactivarAsociacionAsync(int asociacionId, CancellationToken ct = default);
}
