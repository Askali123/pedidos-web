using CatalogoPedidos.Application.Solicitudes;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatalogoPedidos.Infrastructure.Repositories;

public class NotificacionProveedorRepository(IDbContextFactory<AppDbContext> dbFactory) : INotificacionProveedorRepository
{
    public async Task<NotificacionProveedor> CrearAsync(NotificacionProveedor envio, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.NotificacionesProveedor.Add(envio);
        await db.SaveChangesAsync(ct);
        return envio;
    }

    public async Task<List<NotificacionProveedor>> ObtenerPorSolicitudAsync(int solicitudId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.NotificacionesProveedor
            .Include(n => n.Proveedor)
            .Include(n => n.PedidoProveedor).ThenInclude(p => p!.Items)
            .Where(n => n.SolicitudId == solicitudId)
            .OrderByDescending(n => n.FechaEnvio)
            .ToListAsync(ct);
    }

    public async Task<List<NotificacionProveedor>> BuscarAsync(FiltroEnviosProveedorDto filtro, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var query = db.NotificacionesProveedor
            .Include(n => n.Proveedor)
            .Include(n => n.Solicitud)
            .Include(n => n.PedidoProveedor).ThenInclude(p => p!.Items)
            .AsQueryable();

        if (filtro.ProveedorId is not null)
            query = query.Where(n => n.ProveedorId == filtro.ProveedorId);

        if (filtro.FechaDesde is not null)
            query = query.Where(n => n.FechaEnvio >= filtro.FechaDesde);

        if (filtro.FechaHasta is not null)
            query = query.Where(n => n.FechaEnvio < filtro.FechaHasta);

        return await query.OrderByDescending(n => n.FechaEnvio).ToListAsync(ct);
    }
}
