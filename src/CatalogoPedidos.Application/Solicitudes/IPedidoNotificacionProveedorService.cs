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
    /// puede volver a llamar para reenviar, sujeto al límite de tasa (ver
    /// <see cref="EnvioProveedorResultadoDto.Motivo"/> si se rechaza por eso).
    /// <paramref name="gestorEmail"/> es opcional: si viene, va en copia (CC) para que el
    /// gestor tenga el envío en su propia bandeja como respaldo. <paramref name="incluirExcel"/>
    /// agrega, además del PDF (siempre incluido), el mismo detalle en Excel — apagado por
    /// defecto porque la primera versión de esta funcionalidad solo mandaba PDF.
    /// </summary>
    Task<EnvioProveedorResultadoDto> EnviarAProveedorAsync(int pedidoId, int proveedorId, string gestorId, string gestorNombre, string? gestorEmail = null, bool incluirExcel = false, CancellationToken ct = default);

    Task<List<NotificacionProveedor>> ObtenerEnviosAsync(int pedidoId, CancellationToken ct = default);

    /// <summary>Historial completo de envíos a proveedores, filtrable — para la pantalla de historial.</summary>
    Task<List<NotificacionProveedor>> BuscarEnviosAsync(FiltroEnviosProveedorDto filtro, CancellationToken ct = default);
}
