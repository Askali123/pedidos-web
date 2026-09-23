using CatalogoPedidos.Application.Solicitudes;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatalogoPedidos.Infrastructure.Repositories;

public class ConfirmacionEntregaRepository(IDbContextFactory<AppDbContext> dbFactory) : IConfirmacionEntregaRepository
{
    public async Task<ConfirmacionEntrega> CrearAsync(ConfirmacionEntrega confirmacion, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.ConfirmacionesEntrega.Add(confirmacion);
        await db.SaveChangesAsync(ct);
        return confirmacion;
    }

    public async Task<List<ConfirmacionEntrega>> ObtenerPorSolicitudAsync(int solicitudId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ConfirmacionesEntrega
            .Include(c => c.Proveedor)
            .Where(c => c.SolicitudId == solicitudId)
            .OrderBy(c => c.FechaEntrega)
            .ToListAsync(ct);
    }
}
