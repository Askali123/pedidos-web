using CatalogoPedidos.Application.Empresas;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatalogoPedidos.Infrastructure.Repositories;

public class EmpresaRepository(IDbContextFactory<AppDbContext> dbFactory) : IEmpresaRepository
{
    public async Task<List<Empresa>> ObtenerTodasAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Empresas.Where(e => e.Activo).OrderBy(e => e.Nombre).ToListAsync(ct);
    }

    public async Task<Empresa?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Empresas.FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<Empresa> CrearAsync(Empresa empresa, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Empresas.Add(empresa);
        await db.SaveChangesAsync(ct);
        return empresa;
    }

    public async Task ActualizarAsync(Empresa empresa, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Empresas.Update(empresa);
        await db.SaveChangesAsync(ct);
    }
}
