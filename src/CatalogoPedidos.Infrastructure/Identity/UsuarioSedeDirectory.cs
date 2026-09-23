using CatalogoPedidos.Application.Sedes;
using CatalogoPedidos.Application.Usuarios;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CatalogoPedidos.Infrastructure.Identity;

public class UsuarioSedeDirectory(UserManager<ApplicationUser> userManager, ISedeRepository sedes) : IUsuarioSedeDirectory
{
    public async Task<UsuarioSedeDto?> ObtenerSedeAsync(string usuarioId, CancellationToken ct = default)
    {
        var usuario = await userManager.FindByIdAsync(usuarioId);
        if (usuario?.SedeId is null)
            return null;

        var sede = await sedes.ObtenerPorIdAsync(usuario.SedeId.Value, ct);
        if (sede is null)
            return null;

        return new UsuarioSedeDto
        {
            SedeId = sede.Id,
            SedeNombre = sede.Nombre,
            EmpresaId = sede.EmpresaId,
            EmpresaNombre = sede.Empresa?.Nombre ?? string.Empty
        };
    }

    public Task<int> ContarUsuariosPorSedeAsync(int sedeId, CancellationToken ct = default)
        => userManager.Users.CountAsync(u => u.SedeId == sedeId, ct);

    public Task<int> ContarUsuariosPorEmpresaAsync(int empresaId, CancellationToken ct = default)
        => userManager.Users.CountAsync(u => u.Sede != null && u.Sede.EmpresaId == empresaId, ct);
}
