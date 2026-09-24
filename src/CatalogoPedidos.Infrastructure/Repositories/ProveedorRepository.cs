using CatalogoPedidos.Application.Proveedores;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatalogoPedidos.Infrastructure.Repositories;

public class ProveedorRepository(IDbContextFactory<AppDbContext> dbFactory) : IProveedorRepository
{
    public async Task<List<Proveedor>> ObtenerTodosAsync(bool incluirInactivos = false, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.Proveedores.AsQueryable();
        if (!incluirInactivos)
            query = query.Where(p => p.Activo);

        return await query.OrderBy(p => p.Nombre).ToListAsync(ct);
    }

    public async Task<Proveedor?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Proveedores.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<Proveedor> CrearAsync(Proveedor proveedor, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Proveedores.Add(proveedor);
        await db.SaveChangesAsync(ct);
        return proveedor;
    }

    public async Task ActualizarAsync(Proveedor proveedor, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Proveedores.Update(proveedor);
        await db.SaveChangesAsync(ct);
    }
}
