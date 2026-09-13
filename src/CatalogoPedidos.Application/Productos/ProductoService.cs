using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Productos;

public class ProductoService(IProductoRepository repositorio, IProductoImportador importador) : IProductoService
{
    public Task<List<Producto>> ObtenerCatalogoAsync(string? texto = null, string? categoria = null, CancellationToken ct = default)
        => repositorio.ObtenerCatalogoAsync(texto, categoria, ct);

    public Task<List<string>> ObtenerCategoriasAsync(CancellationToken ct = default)
        => repositorio.ObtenerCategoriasAsync(ct);

    public Task<Producto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
        => repositorio.ObtenerPorIdAsync(id, ct);

    public Task<Producto> CrearAsync(CrearProductoDto dto, CancellationToken ct = default)
    {
        var producto = new Producto
        {
            Nombre = dto.Nombre,
            Descripcion = dto.Descripcion,
            Categoria = dto.Categoria,
            Precio = dto.Precio,
            Stock = dto.Stock,
            Activo = true
        };

        return repositorio.CrearAsync(producto, ct);
    }

    public async Task ActualizarAsync(int id, CrearProductoDto dto, CancellationToken ct = default)
    {
        var producto = await repositorio.ObtenerPorIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Producto {id} no encontrado.");

        producto.Nombre = dto.Nombre;
        producto.Descripcion = dto.Descripcion;
        producto.Categoria = dto.Categoria;
        producto.Precio = dto.Precio;
        producto.Stock = dto.Stock;

        await repositorio.ActualizarAsync(producto, ct);
    }

    public Task<ImportarProductosResultado> ImportarDesdeArchivoAsync(Stream archivo, CancellationToken ct = default)
        => importador.ImportarAsync(archivo, ct);
}
