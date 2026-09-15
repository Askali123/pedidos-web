using CatalogoPedidos.Application.Proveedores;
using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Productos;

public class ProductoService(
    IProductoRepository repositorio,
    IProductoImportador importador,
    IProductoProveedorRepository asociaciones) : IProductoService
{
    public Task<List<Producto>> ObtenerCatalogoAsync(string? texto = null, string? categoria = null, CancellationToken ct = default)
        => repositorio.ObtenerCatalogoAsync(texto, categoria, ct);

    public Task<List<string>> ObtenerCategoriasAsync(CancellationToken ct = default)
        => repositorio.ObtenerCategoriasAsync(ct);

    public Task<Producto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
        => repositorio.ObtenerPorIdAsync(id, ct);

    public async Task<Producto> CrearAsync(CrearProductoDto dto, CancellationToken ct = default)
    {
        if (dto.ProveedorId is int proveedorId)
        {
            if (string.IsNullOrWhiteSpace(dto.CodigoProveedor))
                throw new InvalidOperationException("Si eliges un proveedor, el código que él le da a este producto es obligatorio.");

            var existente = await asociaciones.ObtenerPorProveedorYCodigoAsync(proveedorId, dto.CodigoProveedor.Trim(), ct);
            if (existente is not null)
                throw new InvalidOperationException($"Ese proveedor ya usa el código '{dto.CodigoProveedor}' para el producto '{existente.Producto?.Nombre}'.");
        }

        var producto = new Producto
        {
            Nombre = dto.Nombre,
            Descripcion = dto.Descripcion,
            Categoria = dto.Categoria,
            UnidadMedida = dto.UnidadMedida,
            Precio = dto.Precio,
            Stock = dto.Stock,
            StockMinimo = dto.StockMinimo,
            Activo = true
        };

        producto = await repositorio.CrearAsync(producto, ct);

        if (dto.ProveedorId is int proveedorAsociado)
        {
            await asociaciones.CrearAsync(new ProductoProveedor
            {
                ProductoId = producto.Id,
                ProveedorId = proveedorAsociado,
                CodigoProveedor = dto.CodigoProveedor!.Trim()
            }, ct);
        }

        return producto;
    }

    public async Task ActualizarAsync(int id, CrearProductoDto dto, CancellationToken ct = default)
    {
        var producto = await repositorio.ObtenerPorIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Producto {id} no encontrado.");

        producto.Nombre = dto.Nombre;
        producto.Descripcion = dto.Descripcion;
        producto.Categoria = dto.Categoria;
        producto.UnidadMedida = dto.UnidadMedida;
        producto.Precio = dto.Precio;
        producto.Stock = dto.Stock;
        producto.StockMinimo = dto.StockMinimo;

        await repositorio.ActualizarAsync(producto, ct);
    }

    public async Task ActualizarStockMinimoAsync(int id, int? stockMinimo, CancellationToken ct = default)
    {
        var producto = await repositorio.ObtenerPorIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Producto {id} no encontrado.");

        producto.StockMinimo = stockMinimo;
        await repositorio.ActualizarAsync(producto, ct);
    }

    public Task<ResultadoAnalisisImportacion> AnalizarImportacionAsync(Stream archivo, int proveedorId, CancellationToken ct = default)
        => importador.AnalizarAsync(archivo, proveedorId, ct);

    public Task<ImportarProductosResultado> ConfirmarImportacionAsync(List<FilaImportacion> filas, int proveedorId, CancellationToken ct = default)
        => importador.ConfirmarAsync(filas, proveedorId, ct);
}
