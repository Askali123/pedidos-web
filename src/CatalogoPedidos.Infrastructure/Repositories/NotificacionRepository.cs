using CatalogoPedidos.Application.Notificaciones;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatalogoPedidos.Infrastructure.Repositories;

public class NotificacionRepository(IDbContextFactory<AppDbContext> dbFactory) : INotificacionRepository
{
    public async Task<Notificacion> CrearAsync(Notificacion notificacion, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Notificaciones.Add(notificacion);
        await db.SaveChangesAsync(ct);
        return notificacion;
    }

    public async Task<List<Notificacion>> ObtenerPorUsuarioAsync(string usuarioId, int limite = 20, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Notificaciones
            .Where(n => n.UsuarioDestinoId == usuarioId)
            .OrderByDescending(n => n.FechaCreacion)
            .Take(limite)
            .ToListAsync(ct);
    }

    public async Task<int> ContarNoLeidasAsync(string usuarioId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Notificaciones.CountAsync(n => n.UsuarioDestinoId == usuarioId && !n.Leida, ct);
    }

    public async Task<Notificacion?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Notificaciones.FirstOrDefaultAsync(n => n.Id == id, ct);
    }

    public async Task ActualizarAsync(Notificacion notificacion, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Notificaciones.Update(notificacion);
        await db.SaveChangesAsync(ct);
    }

    public async Task MarcarTodasLeidasAsync(string usuarioId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Notificaciones
            .Where(n => n.UsuarioDestinoId == usuarioId && !n.Leida)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Leida, true), ct);
    }
}
