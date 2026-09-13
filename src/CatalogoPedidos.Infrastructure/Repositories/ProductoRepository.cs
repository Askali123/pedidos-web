using CatalogoPedidos.Application.Productos;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatalogoPedidos.Infrastructure.Repositories;

public class ProductoRepository(IDbContextFactory<AppDbContext> dbFactory) : IProductoRepository
{
    public async Task<List<Producto>> ObtenerCatalogoAsync(string? texto, string? categoria, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var query = db.Productos.Where(p => p.Activo).AsQueryable();

        if (!string.IsNullOrWhiteSpace(texto))
            query = query.Where(p => p.Nombre.Contains(texto) || p.Descripcion.Contains(texto));

        if (!string.IsNullOrWhiteSpace(categoria))
            query = query.Where(p => p.Categoria == categoria);

        return await query.OrderBy(p => p.Nombre).ToListAsync(ct);
    }

    public async Task<Producto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Productos.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<Producto> CrearAsync(Producto producto, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Productos.Add(producto);
        await db.SaveChangesAsync(ct);
        return producto;
    }

    public async Task ActualizarAsync(Producto producto, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Productos.Update(producto);
        await db.SaveChangesAsync(ct);
    }

    public async Task AgregarRangoAsync(IEnumerable<Producto> productos, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Productos.AddRange(productos);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<string>> ObtenerCategoriasAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Productos
            .Where(p => p.Activo && p.Categoria != "")
            .Select(p => p.Categoria)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);
    }
}
