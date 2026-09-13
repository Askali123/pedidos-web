using CatalogoPedidos.Application.Productos;
using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Proveedores;

public class ProveedorService(
    IProveedorRepository proveedores,
    IProductoProveedorRepository asociaciones,
    IProductoRepository productos) : IProveedorService
{
    public Task<List<Proveedor>> ObtenerTodosAsync(CancellationToken ct = default)
        => proveedores.ObtenerTodosAsync(ct);

    public Task<Proveedor?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
        => proveedores.ObtenerPorIdAsync(id, ct);

    public Task<Proveedor> CrearAsync(CrearProveedorDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new InvalidOperationException("El nombre del proveedor es obligatorio.");

        var proveedor = new Proveedor
        {
            Nombre = dto.Nombre,
            Nit = dto.Nit,
            Contacto = dto.Contacto,
            Telefono = dto.Telefono,
            Email = dto.Email
        };

        return proveedores.CrearAsync(proveedor, ct);
    }

    public Task<List<ProductoProveedor>> ObtenerProveedoresDeProductoAsync(int productoId, CancellationToken ct = default)
        => asociaciones.ObtenerPorProductoAsync(productoId, ct);

    public Task<List<ProductoProveedor>> ObtenerProductosDeProveedorAsync(int proveedorId, CancellationToken ct = default)
        => asociaciones.ObtenerPorProveedorAsync(proveedorId, ct);

    public async Task<ProductoProveedor> AsociarProveedorAsync(AsociarProveedorDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.CodigoProveedor))
            throw new InvalidOperationException("El código del proveedor para este producto es obligatorio.");

        var producto = await productos.ObtenerPorIdAsync(dto.ProductoId, ct)
            ?? throw new InvalidOperationException("El producto no existe.");

        var proveedor = await proveedores.ObtenerPorIdAsync(dto.ProveedorId, ct)
            ?? throw new InvalidOperationException("El proveedor no existe.");

        if (await asociaciones.ExisteAsociacionAsync(dto.ProductoId, dto.ProveedorId, ct))
            throw new InvalidOperationException($"'{proveedor.Nombre}' ya está asociado a '{producto.Nombre}'.");

        // Un mismo proveedor no puede usar el mismo código para dos productos distintos:
        // ese código es SU forma de identificar el producto, debe ser único por proveedor.
        if (await asociaciones.ExisteCodigoParaOtroProductoAsync(dto.ProveedorId, dto.CodigoProveedor, dto.ProductoId, ct))
            throw new InvalidOperationException($"El proveedor '{proveedor.Nombre}' ya usa el código '{dto.CodigoProveedor}' para otro producto.");

        var asociacion = new ProductoProveedor
        {
            ProductoId = dto.ProductoId,
            ProveedorId = dto.ProveedorId,
            CodigoProveedor = dto.CodigoProveedor.Trim(),
            PrecioProveedor = dto.PrecioProveedor,
            EsPreferido = dto.EsPreferido,
            FechaAsociacion = DateTime.UtcNow
        };

        return await asociaciones.CrearAsync(asociacion, ct);
    }

    public Task QuitarAsociacionAsync(int asociacionId, CancellationToken ct = default)
        => asociaciones.EliminarAsync(asociacionId, ct);
}
