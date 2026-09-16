using System.ComponentModel.DataAnnotations;
using CatalogoPedidos.Application.Productos;
using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Proveedores;

public class ProveedorService(
    IProveedorRepository proveedores,
    IProductoProveedorRepository asociaciones,
    IProductoRepository productos) : IProveedorService
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    /// <summary>
    /// Defensa en profundidad: <see cref="CrearProveedorDto.Email"/> ya lleva
    /// <c>[EmailAddress]</c> para la validación del lado del formulario (Blazor
    /// EditForm/DataAnnotationsValidator), pero este servicio puede llamarse desde
    /// cualquier lugar, no solo desde ese formulario — mismo `EmailAddressAttribute` para
    /// no duplicar la regla en dos sitios distintos.
    /// </summary>
    private static void ValidarEmail(string? email)
    {
        if (!string.IsNullOrWhiteSpace(email) && !EmailValidator.IsValid(email))
            throw new InvalidOperationException("El email no tiene un formato válido.");
    }

    public Task<List<Proveedor>> ObtenerTodosAsync(CancellationToken ct = default)
        => proveedores.ObtenerTodosAsync(ct);

    public Task<Proveedor?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
        => proveedores.ObtenerPorIdAsync(id, ct);

    public Task<Proveedor> CrearAsync(CrearProveedorDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new InvalidOperationException("El nombre del proveedor es obligatorio.");

        ValidarEmail(dto.Email);

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

    public async Task ActualizarAsync(int id, CrearProveedorDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new InvalidOperationException("El nombre del proveedor es obligatorio.");

        ValidarEmail(dto.Email);

        var proveedor = await proveedores.ObtenerPorIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Proveedor {id} no encontrado.");

        proveedor.Nombre = dto.Nombre;
        proveedor.Nit = dto.Nit;
        proveedor.Contacto = dto.Contacto;
        proveedor.Telefono = dto.Telefono;
        proveedor.Email = dto.Email;

        await proveedores.ActualizarAsync(proveedor, ct);
    }

    public async Task DesactivarAsync(int id, CancellationToken ct = default)
    {
        var proveedor = await proveedores.ObtenerPorIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Proveedor {id} no encontrado.");

        proveedor.Activo = false;
        await proveedores.ActualizarAsync(proveedor, ct);
    }

    public Task<List<ProductoProveedor>> ObtenerProveedoresDeProductoAsync(int productoId, CancellationToken ct = default)
        => asociaciones.ObtenerPorProductoAsync(productoId, ct);

    public async Task<Dictionary<int, ProductoProveedor>> ObtenerCodigosPreferidosAsync(IEnumerable<int> productoIds, CancellationToken ct = default)
    {
        var preferidos = await asociaciones.ObtenerPreferidosPorProductosAsync(productoIds, ct);
        return preferidos.ToDictionary(pp => pp.ProductoId);
    }

    public async Task<Dictionary<int, int>> ContarProveedoresPorProductoAsync(IEnumerable<int> productoIds, CancellationToken ct = default)
    {
        var todas = await asociaciones.ObtenerPorProductosAsync(productoIds, ct);
        return todas.GroupBy(pp => pp.ProductoId).ToDictionary(g => g.Key, g => g.Count());
    }

    public Task<List<ProductoProveedor>> ObtenerProductosDeProveedorAsync(
        int proveedorId, string? texto = null, string? categoria = null, bool soloActivos = false, CancellationToken ct = default)
        => asociaciones.ObtenerPorProveedorAsync(proveedorId, texto, categoria, soloActivos, ct);

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
