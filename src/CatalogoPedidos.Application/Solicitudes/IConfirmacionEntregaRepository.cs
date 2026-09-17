using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Solicitudes;

public interface IConfirmacionEntregaRepository
{
    Task<ConfirmacionEntrega> CrearAsync(ConfirmacionEntrega confirmacion, CancellationToken ct = default);
    Task<ConfirmacionEntrega?> ObtenerPorPedidoAsync(int pedidoId, CancellationToken ct = default);
}
