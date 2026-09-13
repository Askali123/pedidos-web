using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Infrastructure.Identity;
using CatalogoPedidos.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace CatalogoPedidos.Web;

public static class Seed
{
    public static async Task EjecutarAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<AppDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");

        foreach (var rol in Roles.Todos)
        {
            if (!await roleManager.RoleExistsAsync(rol))
                await roleManager.CreateAsync(new IdentityRole(rol));
        }

        const string gestorEmail = "gestor@catalogo.local";
        const string gestorPassword = "Gestor123!";
        var gestor = await userManager.FindByEmailAsync(gestorEmail);
        if (gestor is null)
        {
            gestor = new ApplicationUser
            {
                UserName = gestorEmail,
                Email = gestorEmail,
                EmailConfirmed = true,
                NombreCompleto = "Gestor de Catálogo"
            };
            var resultado = await userManager.CreateAsync(gestor, gestorPassword);
            if (resultado.Succeeded)
            {
                await userManager.AddToRoleAsync(gestor, Roles.Gestor);
                logger.LogInformation("Usuario gestor de ejemplo creado: {Email} / {Password}", gestorEmail, gestorPassword);
            }
        }

        const string usuarioEmail = "usuario@catalogo.local";
        const string usuarioPassword = "Usuario123!";
        var usuarioDemo = await userManager.FindByEmailAsync(usuarioEmail);
        if (usuarioDemo is null)
        {
            usuarioDemo = new ApplicationUser
            {
                UserName = usuarioEmail,
                Email = usuarioEmail,
                EmailConfirmed = true,
                NombreCompleto = "Usuario de Prueba"
            };
            var resultado = await userManager.CreateAsync(usuarioDemo, usuarioPassword);
            if (resultado.Succeeded)
            {
                await userManager.AddToRoleAsync(usuarioDemo, Roles.Usuario);
                logger.LogInformation("Usuario de prueba creado: {Email} / {Password}", usuarioEmail, usuarioPassword);
            }
        }

        if (!db.Productos.Any())
        {
            db.Productos.AddRange(
                new Producto { Nombre = "Laptop 14\"", Descripcion = "Laptop para oficina, 16GB RAM", Categoria = "Cómputo", Precio = 3200000, Stock = 8 },
                new Producto { Nombre = "Monitor 24\"", Descripcion = "Monitor Full HD IPS", Categoria = "Cómputo", Precio = 650000, Stock = 15 },
                new Producto { Nombre = "Silla ergonómica", Descripcion = "Silla de oficina ajustable", Categoria = "Mobiliario", Precio = 890000, Stock = 5 },
                new Producto { Nombre = "Teclado mecánico", Descripcion = "Teclado mecánico switches rojos", Categoria = "Cómputo", Precio = 280000, Stock = 20 },
                new Producto { Nombre = "Escritorio ajustable", Descripcion = "Escritorio de altura regulable", Categoria = "Mobiliario", Precio = 1450000, Stock = 3 }
            );
            await db.SaveChangesAsync();
            logger.LogInformation("Productos de ejemplo sembrados en el catálogo.");
        }
    }
}
