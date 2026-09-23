using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Empresas;

public interface IEmpresaService
{
    Task<List<Empresa>> ObtenerTodasAsync(CancellationToken ct = default);
    Task<Empresa?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Empresa> CrearAsync(CrearEmpresaDto dto, CancellationToken ct = default);
    Task ActualizarAsync(int id, CrearEmpresaDto dto, CancellationToken ct = default);

    /// <summary>Baja lógica — no borra la Empresa ni sus Sedes/histórico, solo deja de ofrecerse para asociaciones nuevas.</summary>
    Task DesactivarAsync(int id, CancellationToken ct = default);
}
