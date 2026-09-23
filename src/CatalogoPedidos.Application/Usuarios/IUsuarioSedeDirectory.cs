namespace CatalogoPedidos.Application.Usuarios;

/// <summary>
/// Abstracción para resolver a qué Sede/Empresa pertenece un usuario, sin que Application
/// dependa directamente de ASP.NET Identity (eso vive en Infrastructure) — mismo criterio
/// que <see cref="IGestorDirectory"/>.
/// </summary>
public interface IUsuarioSedeDirectory
{
    /// <summary>Null si el usuario no existe o no tiene sede asignada.</summary>
    Task<UsuarioSedeDto?> ObtenerSedeAsync(string usuarioId, CancellationToken ct = default);

    /// <summary>Cuántos usuarios tienen esta Sede asignada — para avisar antes de desactivarla (ver docs/PLAN_EMPRESAS_FILIALES.md, Etapa 9).</summary>
    Task<int> ContarUsuariosPorSedeAsync(int sedeId, CancellationToken ct = default);

    /// <summary>Cuántos usuarios tienen alguna Sede de esta Empresa asignada — para avisar antes de desactivarla (la desactivación cascadea a sus Sedes).</summary>
    Task<int> ContarUsuariosPorEmpresaAsync(int empresaId, CancellationToken ct = default);
}
