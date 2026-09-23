using CatalogoPedidos.Application.Solicitudes;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatalogoPedidos.Infrastructure.Repositories;

public class PedidoProveedorRepository(IDbContextFactory<AppDbContext> dbFactory) : IPedidoProveedorRepository
{
    public async Task<PedidoProveedor?> ObtenerPorSolicitudYProveedorAsync(int solicitudId, int proveedorId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.PedidosProveedor
            .Include(pp => pp.Items)
            .Include(pp => pp.Proveedor)
            .FirstOrDefaultAsync(pp => pp.SolicitudId == solicitudId && pp.ProveedorId == proveedorId, ct);
    }

    public async Task<List<PedidoProveedor>> ObtenerPorSolicitudAsync(int solicitudId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.PedidosProveedor
            .Include(pp => pp.Items)
            .Where(pp => pp.SolicitudId == solicitudId)
            .ToListAsync(ct);
    }

    public async Task<PedidoProveedor?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.PedidosProveedor
            .Include(pp => pp.Items)
            .Include(pp => pp.Proveedor)
            .Include(pp => pp.Solicitud)
            .FirstOrDefaultAsync(pp => pp.Id == id, ct);
    }

    public async Task<PedidoProveedor> CrearAsync(PedidoProveedor documento, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.PedidosProveedor.Add(documento);
        await db.SaveChangesAsync(ct);
        return documento;
    }

    public async Task ActualizarAsync(PedidoProveedor documento, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.PedidosProveedor.Update(documento);
        await db.SaveChangesAsync(ct);
    }
}