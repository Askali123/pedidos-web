using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Empresas;

public interface IEmpresaRepository
{
    Task<List<Empresa>> ObtenerTodasAsync(CancellationToken ct = default);
    Task<Empresa?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Empresa> CrearAsync(Empresa empresa, CancellationToken ct = default);
    Task ActualizarAsync(Empresa empresa, CancellationToken ct = default);
}
