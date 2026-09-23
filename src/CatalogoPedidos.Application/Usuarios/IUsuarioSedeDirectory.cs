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
}
