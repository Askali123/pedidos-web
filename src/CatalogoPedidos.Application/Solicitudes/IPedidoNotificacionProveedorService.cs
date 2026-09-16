using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Solicitudes;

public interface IPedidoNotificacionProveedorService
{
    /// <summary>
    /// Agrupa las líneas del pedido por el proveedor preferido de cada producto y le manda
    /// un correo a CADA proveedor distinto, solo con SUS líneas (un pedido puede tener
    /// productos de varios proveedores). Deja un <see cref="NotificacionProveedor"/> por
    /// cada envío exitoso. Es una acción manual del gestor — se puede volver a llamar para
    /// reenviar (no bloquea reenvíos).
    /// </summary>
    Task<List<EnvioProveedorResultadoDto>> EnviarAProveedoresAsync(int pedidoId, CancellationToken ct = default);

    Task<List<NotificacionProveedor>> ObtenerEnviosAsync(int pedidoId, CancellationToken ct = default);
}
