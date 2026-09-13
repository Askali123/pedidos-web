using CatalogoPedidos.Application.Solicitudes;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Domain.Enums;
using CatalogoPedidos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatalogoPedidos.Infrastructure.Repositories;

public class SolicitudRepository(IDbContextFactory<AppDbContext> dbFactory) : ISolicitudRepository
{
    public async Task<SolicitudProducto> CrearAsync(SolicitudProducto solicitud, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Solicitudes.Add(solicitud);
        await db.SaveChangesAsync(ct);
        return solicitud;
    }

    public async Task<SolicitudProducto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Solicitudes.Include(s => s.Producto).FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<List<SolicitudProducto>> ObtenerPorSolicitanteAsync(string solicitanteId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Solicitudes
            .Include(s => s.Producto)
            .Where(s => s.SolicitanteId == solicitanteId)
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync(ct);
    }

    public async Task<List<SolicitudProducto>> ObtenerPendientesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Solicitudes
            .Include(s => s.Producto)
            .Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .OrderBy(s => s.FechaSolicitud)
            .ToListAsync(ct);
    }

    public async Task ActualizarAsync(SolicitudProducto solicitud, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Solicitudes.Update(solicitud);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<SolicitudProducto>> BuscarAsync(FiltroSolicitudesDto filtro, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var query = db.Solicitudes.Include(s => s.Producto).AsQueryable();

        if (filtro.FechaDesde is not null)
            query = query.Where(s => s.FechaSolicitud >= filtro.FechaDesde);

        if (filtro.FechaHasta is not null)
            query = query.Where(s => s.FechaSolicitud < filtro.FechaHasta);

        if (!string.IsNullOrEmpty(filtro.SolicitanteId))
            query = query.Where(s => s.SolicitanteId == filtro.SolicitanteId);

        if (filtro.ProductoId is not null)
            query = query.Where(s => s.ProductoId == filtro.ProductoId);

        if (filtro.Estado is not null)
            query = query.Where(s => s.Estado == filtro.Estado);

        return await query.OrderByDescending(s => s.FechaSolicitud).ToListAsync(ct);
    }

    public async Task<List<SolicitanteResumenDto>> ObtenerSolicitantesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Solicitudes
            .Select(s => new { s.SolicitanteId, s.SolicitanteNombre })
            .Distinct()
            .OrderBy(s => s.SolicitanteNombre)
            .Select(s => new SolicitanteResumenDto { Id = s.SolicitanteId, Nombre = s.SolicitanteNombre })
            .ToListAsync(ct);
    }
}
