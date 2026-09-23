using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Sedes;

public interface ISedeService
{
    Task<List<Sede>> ObtenerTodasAsync(int? empresaId = null, CancellationToken ct = default);
    Task<Sede?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Sede> CrearAsync(CrearSedeDto dto, CancellationToken ct = default);
    Task ActualizarAsync(int id, CrearSedeDto dto, CancellationToken ct = default);

    /// <summary>Baja lógica — no borra la Sede ni el histórico de sus usuarios/solicitudes, solo deja de ofrecerse para asociaciones nuevas.</summary>
    Task DesactivarAsync(int id, CancellationToken ct = default);
}
