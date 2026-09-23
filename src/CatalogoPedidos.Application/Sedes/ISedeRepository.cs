using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Sedes;

public interface ISedeRepository
{
    /// <summary>Sedes activas, opcionalmente filtradas por Empresa. Incluye la Empresa dueña.</summary>
    Task<List<Sede>> ObtenerTodasAsync(int? empresaId = null, CancellationToken ct = default);
    Task<Sede?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Sede> CrearAsync(Sede sede, CancellationToken ct = default);
    Task ActualizarAsync(Sede sede, CancellationToken ct = default);

    /// <summary>
    /// Trae Sedes por Id sin filtrar por Activo (incluye Empresa) — para poder mostrar el
    /// nombre de la sede/empresa de un usuario aunque esa sede se haya desactivado después.
    /// </summary>
    Task<List<Sede>> ObtenerPorIdsAsync(IEnumerable<int> ids, CancellationToken ct = default);
}
