using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Solicitudes;

public interface IConfirmacionEntregaRepository
{
    Task<ConfirmacionEntrega> CrearAsync(ConfirmacionEntrega confirmacion, CancellationToken ct = default);

    /// <summary>Todas las confirmaciones del pedido — una por proveedor (más la eventual "sin proveedor"), no una sola.</summary>
    Task<List<ConfirmacionEntrega>> ObtenerPorPedidoAsync(int pedidoId, CancellationToken ct = default);
}
