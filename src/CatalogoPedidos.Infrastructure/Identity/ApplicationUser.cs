using Microsoft.AspNetCore.Identity;

namespace CatalogoPedidos.Infrastructure.Identity;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    public string NombreCompleto { get; set; } = string.Empty;

    /// <summary>
    /// Dirección a la que el usuario suele pedir que se le entreguen los insumos. Solo un
    /// default para autocompletar el carrito (ver tarea 4 del plan) — lo que realmente
    /// queda asociado a cada pedido es un snapshot en <c>Solicitud.DireccionEntrega</c>, así
    /// que cambiar esto no altera pedidos ya creados.
    /// </summary>
    public string? DireccionPredeterminada { get; set; }
}
