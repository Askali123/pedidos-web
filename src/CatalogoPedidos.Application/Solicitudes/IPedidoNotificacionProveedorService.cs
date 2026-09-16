using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Solicitudes;

public interface IPedidoNotificacionProveedorService
{
    /// <summary>
    /// Proveedores asociados a los productos de este pedido (uno puede tener varios) — para
    /// que el gestor elija a cuál enviárselo desde el selector "Enviar a proveedor".
    /// </summary>
    Task<List<ProveedorDelPedidoDto>> ObtenerProveedoresDisponiblesAsync(int pedidoId, CancellationToken ct = default);

    /// <summary>
    /// Le manda al proveedor elegido un correo con el detalle EN PDF adjunto (solo sus
    /// líneas del pedido, con el código que ese proveedor le da a cada producto) y deja un
    /// <see cref="NotificacionProveedor"/> como historial. Acción manual del gestor — se
    /// puede volver a llamar para reenviar (no bloquea reenvíos).
    /// </summary>
    Task<EnvioProveedorResultadoDto> EnviarAProveedorAsync(int pedidoId, int proveedorId, CancellationToken ct = default);

    Task<List<NotificacionProveedor>> ObtenerEnviosAsync(int pedidoId, CancellationToken ct = default);
}
