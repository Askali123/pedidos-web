using CatalogoPedidos.Application.Usuarios;
using Microsoft.AspNetCore.Identity;

namespace CatalogoPedidos.Infrastructure.Identity;

public class GestorDirectory(UserManager<ApplicationUser> userManager) : IGestorDirectory
{
    public async Task<List<string>> ObtenerIdsGestoresAsync(CancellationToken ct = default)
    {
        var gestores = await userManager.GetUsersInRoleAsync(Roles.Gestor);
        return gestores.Select(u => u.Id).ToList();
    }
}
