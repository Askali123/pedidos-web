using CatalogoPedidos.Application.Usuarios;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CatalogoPedidos.Infrastructure.Identity;

/// <summary>
/// Implementación pensada para resolverse desde un scope de DI aislado (ver
/// docs/PLAN_MEJORAS_AUTENTICACION.md #2): usa <see cref="UserManager{TUser}"/>, que por
/// dentro comparte el <c>AppDbContext</c> Scoped de Identity — el caller (la página Razor)
/// es responsable de crear ese scope, no este servicio.
/// </summary>
public class UsuarioRolService(UserManager<ApplicationUser> userManager) : IUsuarioRolService
{
    public async Task<List<UsuarioRolDto>> ObtenerUsuariosAsync(CancellationToken ct = default)
    {
        var usuarios = await userManager.Users.OrderBy(u => u.NombreCompleto).ToListAsync(ct);

        var resultado = new List<UsuarioRolDto>();
        foreach (var usuario in usuarios)
        {
            resultado.Add(new UsuarioRolDto
            {
                Id = usuario.Id,
                Email = usuario.Email ?? usuario.UserName ?? "-",
                NombreCompleto = string.IsNullOrWhiteSpace(usuario.NombreCompleto) ? (usuario.Email ?? usuario.UserName ?? "-") : usuario.NombreCompleto,
                EsGestor = await userManager.IsInRoleAsync(usuario, Roles.Gestor)
            });
        }

        return resultado;
    }

    public async Task AsignarGestorAsync(string usuarioId, CancellationToken ct = default)
    {
        var usuario = await userManager.FindByIdAsync(usuarioId)
            ?? throw new InvalidOperationException("El usuario no existe.");

        if (await userManager.IsInRoleAsync(usuario, Roles.Gestor))
            return;

        var resultado = await userManager.AddToRoleAsync(usuario, Roles.Gestor);
        if (!resultado.Succeeded)
            throw new InvalidOperationException($"No se pudo asignar el rol Gestor: {string.Join(", ", resultado.Errors.Select(e => e.Description))}");
    }

    public async Task QuitarGestorAsync(string usuarioId, CancellationToken ct = default)
    {
        var usuario = await userManager.FindByIdAsync(usuarioId)
            ?? throw new InvalidOperationException("El usuario no existe.");

        if (!await userManager.IsInRoleAsync(usuario, Roles.Gestor))
            return;

        // No dejar la app sin ningún gestor — ni siquiera cuando el que se lo quita a sí
        // mismo es el último (ver docs/PLAN_MEJORAS_AUTENTICACION.md #4).
        var gestoresActuales = await userManager.GetUsersInRoleAsync(Roles.Gestor);
        if (gestoresActuales.Count <= 1)
            throw new InvalidOperationException("No se puede quitar el rol Gestor: la aplicación quedaría sin ningún gestor.");

        var resultado = await userManager.RemoveFromRoleAsync(usuario, Roles.Gestor);
        if (!resultado.Succeeded)
            throw new InvalidOperationException($"No se pudo quitar el rol Gestor: {string.Join(", ", resultado.Errors.Select(e => e.Description))}");
    }
}
