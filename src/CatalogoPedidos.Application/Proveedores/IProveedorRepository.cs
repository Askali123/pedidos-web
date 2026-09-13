using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Proveedores;

public interface IProveedorRepository
{
    Task<List<Proveedor>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Proveedor?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Proveedor> CrearAsync(Proveedor proveedor, CancellationToken ct = default);
    Task ActualizarAsync(Proveedor proveedor, CancellationToken ct = default);
}
