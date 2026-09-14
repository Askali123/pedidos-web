using CatalogoPedidos.Domain.Enums;

namespace CatalogoPedidos.Web.Components.UI;

public static class EstadoBadgeHelper
{
    public static BadgeVariant VariantePara(EstadoSolicitud estado) => estado switch
    {
        EstadoSolicitud.Aprobada => BadgeVariant.Success,
        EstadoSolicitud.Rechazada => BadgeVariant.Danger,
        _ => BadgeVariant.Warning
    };
}
