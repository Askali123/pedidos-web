namespace CatalogoPedidos.Infrastructure.Identity;

public static class ApplicationUserExtensions
{
    /// <summary>
    /// Nombre a mostrar para una persona: su NombreCompleto si lo tiene cargado
    /// (obligatorio desde que el registro lo pide), o el nombre de usuario/email
    /// como respaldo para cuentas creadas antes de ese cambio.
    /// </summary>
    public static string NombreParaMostrar(this ApplicationUser? usuario, string? nombreAlterno = null)
        => !string.IsNullOrWhiteSpace(usuario?.NombreCompleto)
            ? usuario.NombreCompleto
            : nombreAlterno ?? usuario?.UserName ?? "Desconocido";
}
