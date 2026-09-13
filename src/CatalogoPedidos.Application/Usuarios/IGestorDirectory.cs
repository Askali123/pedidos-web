namespace CatalogoPedidos.Application.Usuarios;

/// <summary>
/// Abstracción para resolver qué usuarios tienen el rol Gestor, sin que Application
/// dependa directamente de ASP.NET Identity (eso vive en Infrastructure).
/// </summary>
public interface IGestorDirectory
{
    Task<List<string>> ObtenerIdsGestoresAsync(CancellationToken ct = default);
}
