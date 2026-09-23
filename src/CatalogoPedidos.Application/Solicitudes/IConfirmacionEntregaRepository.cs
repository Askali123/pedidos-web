using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Solicitudes;

public interface IConfirmacionEntregaRepository
{
    Task<ConfirmacionEntrega> CrearAsync(ConfirmacionEntrega confirmacion, CancellationToken ct = default);

    /// <summary>Todas las confirmaciones de la solicitud — una por proveedor (más la eventual "sin proveedor"), no una sola.</summary>
    Task<List<ConfirmacionEntrega>> ObtenerPorSolicitudAsync(int solicitudId, CancellationToken ct = default);
}
