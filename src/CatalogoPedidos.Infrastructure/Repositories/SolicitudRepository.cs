using CatalogoPedidos.Application.Solicitudes;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Domain.Enums;
using CatalogoPedidos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatalogoPedidos.Infrastructure.Repositories;

public class SolicitudRepository(IDbContextFactory<AppDbContext> dbFactory) : ISolicitudRepository
{
    public async Task<Pedido> CrearPedidoAsync(Pedido pedido, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync(ct);
        return pedido;
    }

    public async Task<Pedido?> ObtenerPedidoAsync(int pedidoId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Pedidos
            .Include(p => p.Items).ThenInclude(i => i.Producto)
            .Include(p => p.ConfirmacionEntrega)
            .FirstOrDefaultAsync(p => p.Id == pedidoId, ct);
    }

    public async Task ActualizarPedidoAsync(Pedido pedido, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Pedidos.Update(pedido);
        await db.SaveChangesAsync(ct);
    }

    public async Task<SolicitudProducto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Solicitudes.Include(s => s.Producto).Include(s => s.Pedido).FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<List<SolicitudProducto>> ObtenerPorSolicitanteAsync(string solicitanteId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Solicitudes
            .Include(s => s.Producto)
            .Include(s => s.Pedido)
            .Where(s => s.SolicitanteId == solicitanteId)
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync(ct);
    }

    public async Task<List<SolicitudProducto>> ObtenerPendientesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Solicitudes
            .Include(s => s.Producto)
            .Include(s => s.Pedido)
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

        var query = db.Solicitudes.Include(s => s.Producto).Include(s => s.Pedido).AsQueryable();

        if (filtro.FechaDesde is not null)
            query = query.Where(s => s.FechaSolicitud >= filtro.FechaDesde);

        if (filtro.FechaHasta is not null)
            query = query.Where(s => s.FechaSolicitud < filtro.FechaHasta);

        if (!string.IsNullOrEmpty(filtro.SolicitanteId))
            query = query.Where(s => s.SolicitanteId == filtro.SolicitanteId);

        if (filtro.ProductoId is not null)
            query = query.Where(s => s.ProductoId == filtro.ProductoId);

        if (filtro.ProveedorId is not null)
            query = query.Where(s => s.Producto!.Proveedores.Any(pp => pp.ProveedorId == filtro.ProveedorId));

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

    public async Task<List<SolicitudProducto>> BuscarResueltasAsync(FiltroHistorialResolucionesDto filtro, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Solo lo que ya se resolvió — una pantalla de auditoría de "quién resolvió qué
        // y cuándo" no tiene sentido para líneas todavía Pendientes.
        var query = db.Solicitudes
            .Include(s => s.Producto)
            .Include(s => s.Pedido)
            .Where(s => s.Estado != EstadoSolicitud.Pendiente)
            .AsQueryable();

        if (!string.IsNullOrEmpty(filtro.GestorId))
            query = query.Where(s => s.GestorId == filtro.GestorId);

        if (filtro.FechaDesde is not null)
            query = query.Where(s => s.FechaResolucion >= filtro.FechaDesde);

        if (filtro.FechaHasta is not null)
            query = query.Where(s => s.FechaResolucion < filtro.FechaHasta);

        if (filtro.Estado is not null)
            query = query.Where(s => s.Estado == filtro.Estado);

        return await query.OrderByDescending(s => s.FechaResolucion).ToListAsync(ct);
    }

    public async Task<List<GestorResumenDto>> ObtenerGestoresAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Solicitudes
            .Where(s => s.GestorId != null)
            .Select(s => new { s.GestorId, s.GestorNombre })
            .Distinct()
            .OrderBy(s => s.GestorNombre)
            .Select(s => new GestorResumenDto { Id = s.GestorId!, Nombre = s.GestorNombre! })
            .ToListAsync(ct);
    }

    public async Task<List<Pedido>> ObtenerAprobadosSinEntregaAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Pedidos
            .Include(p => p.Items).ThenInclude(i => i.Producto)
            .Where(p => !p.RecordatorioEntregaEnviado
                     && p.ConfirmacionEntrega == null
                     && p.Items.Any(i => i.Estado == EstadoSolicitud.Aprobada))
            .ToListAsync(ct);
    }
}
