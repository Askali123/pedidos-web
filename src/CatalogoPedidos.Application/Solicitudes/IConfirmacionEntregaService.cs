using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Solicitudes;

public interface IConfirmacionEntregaService
{
    Task<ConfirmacionEntrega?> ObtenerPorPedidoAsync(int pedidoId, CancellationToken ct = default);

    /// <summary>
    /// Deja registrada la entrega de los insumos aprobados del pedido. Un pedido solo se
    /// puede confirmar una vez — si ya tiene una <see cref="ConfirmacionEntrega"/>, tira
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    Task<ConfirmacionEntrega> ConfirmarAsync(int pedidoId, string gestorId, string gestorNombre, ConfirmarEntregaDto dto, CancellationToken ct = default);
}
