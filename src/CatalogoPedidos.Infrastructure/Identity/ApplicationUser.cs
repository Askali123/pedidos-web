using CatalogoPedidos.Domain.Entities;
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

    /// <summary>
    /// Sede (de una Empresa filial de Auropaq) a la que pertenece este usuario Solicitante.
    /// Nullable: las cuentas existentes no tienen sede hasta que un Gestor las asocie
    /// manualmente. La Empresa del usuario se deriva de <c>Sede.EmpresaId</c> — no se
    /// duplica acá para no tener dos fuentes de verdad (docs/PLAN_EMPRESAS_FILIALES.md).
    /// </summary>
    public int? SedeId { get; set; }
    public Sede? Sede { get; set; }
}
