using CatalogoPedidos.Application.Sedes;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatalogoPedidos.Infrastructure.Repositories;

public class SedeRepository(IDbContextFactory<AppDbContext> dbFactory) : ISedeRepository
{
    public async Task<List<Sede>> ObtenerTodasAsync(int? empresaId = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.Sedes.Include(s => s.Empresa).Where(s => s.Activo);
        if (empresaId is not null)
            query = query.Where(s => s.EmpresaId == empresaId);

        return await query.OrderBy(s => s.Empresa!.Nombre).ThenBy(s => s.Nombre).ToListAsync(ct);
    }

    public async Task<Sede?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Sedes.Include(s => s.Empresa).FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Sede> CrearAsync(Sede sede, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Sedes.Add(sede);
        await db.SaveChangesAsync(ct);
        return sede;
    }

    public async Task ActualizarAsync(Sede sede, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Sedes.Update(sede);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<Sede>> ObtenerPorIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var idsList = ids.Distinct().ToList();
        return await db.Sedes.Include(s => s.Empresa).Where(s => idsList.Contains(s.Id)).ToListAsync(ct);
    }
}
