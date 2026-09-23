using CatalogoPedidos.Application.Solicitudes;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Domain.Enums;
using CatalogoPedidos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatalogoPedidos.Infrastructure.Repositories;

public class SolicitudRepository(IDbContextFactory<AppDbContext> dbFactory) : ISolicitudRepository
{
    public async Task<Solicitud> CrearSolicitudAsync(Solicitud solicitud, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Solicitudes.Add(solicitud);
        await db.SaveChangesAsync(ct);
        return solicitud;
    }

    public async Task<Solicitud?> ObtenerSolicitudAsync(int solicitudId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Solicitudes
            .Include(p => p.Items).ThenInclude(i => i.Producto)
            .Include(p => p.ConfirmacionesEntrega).ThenInclude(c => c.Proveedor)
            .FirstOrDefaultAsync(p => p.Id == solicitudId, ct);
    }

    public async Task ActualizarSolicitudAsync(Solicitud solicitud, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Solicitudes.Update(solicitud);
        await db.SaveChangesAsync(ct);
    }

    public async Task<DetalleSolicitud?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.DetallesSolicitud.Include(s => s.Producto).Include(s => s.Solicitud).FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<List<DetalleSolicitud>> ObtenerPorSolicitanteAsync(string solicitanteId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.DetallesSolicitud
            .Include(s => s.Producto)
            .Include(s => s.Solicitud)
            .Where(s => s.SolicitanteId == solicitanteId)
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync(ct);
    }

    public async Task<List<DetalleSolicitud>> ObtenerPendientesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.DetallesSolicitud
            .Include(s => s.Producto)
            .Include(s => s.Solicitud)
            .Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .OrderBy(s => s.FechaSolicitud)
            .ToListAsync(ct);
    }

    public async Task ActualizarAsync(DetalleSolicitud solicitud, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.DetallesSolicitud.Update(solicitud);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<DetalleSolicitud>> BuscarAsync(FiltroSolicitudesDto filtro, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var query = db.DetallesSolicitud.Include(s => s.Producto).Include(s => s.Solicitud).AsQueryable();

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
        return await db.DetallesSolicitud
            .Select(s => new { s.SolicitanteId, s.SolicitanteNombre })
            .Distinct()
            .OrderBy(s => s.SolicitanteNombre)
            .Select(s => new SolicitanteResumenDto { Id = s.SolicitanteId, Nombre = s.SolicitanteNombre })
            .ToListAsync(ct);
    }

    public async Task<List<DetalleSolicitud>> BuscarResueltasAsync(FiltroHistorialResolucionesDto filtro, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Solo lo que ya se resolvió — una pantalla de auditoría de "quién resolvió qué
        // y cuándo" no tiene sentido para líneas todavía Pendientes.
        var query = db.DetallesSolicitud
            .Include(s => s.Producto)
            .Include(s => s.Solicitud)
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
        return await db.DetallesSolicitud
            .Where(s => s.GestorId != null)
            .Select(s => new { s.GestorId, s.GestorNombre })
            .Distinct()
            .OrderBy(s => s.GestorNombre)
            .Select(s => new GestorResumenDto { Id = s.GestorId!, Nombre = s.GestorNombre! })
            .ToListAsync(ct);
    }

    public async Task<List<Solicitud>> ObtenerAprobadosSinEntregaAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // No filtra por "sin ninguna confirmación": con confirmación por proveedor, una
        // solicitud puede tener una confirmación parcial (ej. un proveedor ya entregó, otro
        // no) y seguir teniendo algo genuinamente pendiente. Quién de estos candidatos
        // todavía tiene algún grupo de entrega sin confirmar se resuelve en
        // ConfirmacionEntregaService.EnviarRecordatoriosEntregaPendienteAsync, que sí
        // conoce los grupos (requiere ProductoProveedor/NotificacionProveedor, fuera del
        // agregado Solicitud) — ver docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md #8.
        return await db.Solicitudes
            .Include(p => p.Items).ThenInclude(i => i.Producto)
            .Where(p => !p.RecordatorioEntregaEnviado
                     && p.Items.Any(i => i.Estado == EstadoSolicitud.Aprobada))
            .ToListAsync(ct);
    }
}
