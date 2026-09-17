namespace CatalogoPedidos.Application.Usuarios;

public interface IUsuarioRolService
{
    Task<List<UsuarioRolDto>> ObtenerUsuariosAsync(CancellationToken ct = default);

    /// <summary>Agrega el rol Gestor a un usuario. Idempotente: si ya lo tiene, no hace nada.</summary>
    Task AsignarGestorAsync(string usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Quita el rol Gestor a un usuario. Tira <see cref="InvalidOperationException"/> si
    /// eso dejaría la aplicación sin ningún gestor — ver
    /// docs/PLAN_MEJORAS_AUTENTICACION.md #4.
    /// </summary>
    Task QuitarGestorAsync(string usuarioId, CancellationToken ct = default);
}
