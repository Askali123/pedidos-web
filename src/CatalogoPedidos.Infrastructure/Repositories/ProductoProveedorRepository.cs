using CatalogoPedidos.Application.Proveedores;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatalogoPedidos.Infrastructure.Repositories;

public class ProductoProveedorRepository(IDbContextFactory<AppDbContext> dbFactory) : IProductoProveedorRepository
{
    public async Task<List<ProductoProveedor>> ObtenerPorProductoAsync(int productoId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ProductoProveedores
            .Include(pp => pp.Proveedor)
            .Where(pp => pp.ProductoId == productoId)
            .OrderByDescending(pp => pp.EsPreferido)
            .ThenBy(pp => pp.Proveedor!.Nombre)
            .ToListAsync(ct);
    }

    public async Task<List<ProductoProveedor>> ObtenerPorProveedorAsync(
        int proveedorId, string? texto = null, string? categoria = null, bool soloActivos = false, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var query = db.ProductoProveedores
            .Include(pp => pp.Producto)
            .Where(pp => pp.ProveedorId == proveedorId);

        if (soloActivos)
            query = query.Where(pp => pp.Producto!.Activo);

        if (!string.IsNullOrWhiteSpace(texto))
            query = query.Where(pp =>
                pp.Producto!.Nombre.Contains(texto) ||
                pp.Producto!.Descripcion.Contains(texto) ||
                pp.CodigoProveedor.Contains(texto));

        if (!string.IsNullOrWhiteSpace(categoria))
            query = query.Where(pp => pp.Producto!.Categoria == categoria);

        return await query.OrderBy(pp => pp.Producto!.Nombre).ToListAsync(ct);
    }

    public async Task<ProductoProveedor?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ProductoProveedores.Include(pp => pp.Producto).Include(pp => pp.Proveedor)
            .FirstOrDefaultAsync(pp => pp.Id == id, ct);
    }

    public async Task<ProductoProveedor?> ObtenerPorProveedorYCodigoAsync(int proveedorId, string codigo, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ProductoProveedores.Include(pp => pp.Producto)
            .FirstOrDefaultAsync(pp => pp.ProveedorId == proveedorId && pp.CodigoProveedor == codigo, ct);
    }

    public async Task<List<ProductoProveedor>> ObtenerPreferidosPorProductosAsync(IEnumerable<int> productoIds, CancellationToken ct = default)
    {
        var ids = productoIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var candidatas = await db.ProductoProveedores
            .Include(pp => pp.Proveedor)
            .Where(pp => ids.Contains(pp.ProductoId))
            .OrderByDescending(pp => pp.EsPreferido)
            .ThenBy(pp => pp.FechaAsociacion)
            .ToListAsync(ct);

        return candidatas
            .GroupBy(pp => pp.ProductoId)
            .Select(g => g.First())
            .ToList();
    }

    public async Task<bool> ExisteAsociacionAsync(int productoId, int proveedorId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ProductoProveedores.AnyAsync(pp => pp.ProductoId == productoId && pp.ProveedorId == proveedorId, ct);
    }

    public async Task<bool> ExisteCodigoParaOtroProductoAsync(int proveedorId, string codigo, int productoId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ProductoProveedores.AnyAsync(pp =>
            pp.ProveedorId == proveedorId &&
            pp.CodigoProveedor == codigo &&
            pp.ProductoId != productoId, ct);
    }

    public async Task<ProductoProveedor> CrearAsync(ProductoProveedor asociacion, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.ProductoProveedores.Add(asociacion);
        await db.SaveChangesAsync(ct);
        return asociacion;
    }

    public async Task ActualizarAsync(ProductoProveedor asociacion, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.ProductoProveedores.Update(asociacion);
        await db.SaveChangesAsync(ct);
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var asociacion = await db.ProductoProveedores.FindAsync([id], ct);
        if (asociacion is not null)
        {
            db.ProductoProveedores.Remove(asociacion);
            await db.SaveChangesAsync(ct);
        }
    }
}
