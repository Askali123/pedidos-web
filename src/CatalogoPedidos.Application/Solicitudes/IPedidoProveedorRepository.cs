using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Solicitudes;

public interface IPedidoProveedorRepository
{
    /// <summary>
    /// Documento persistido de un (Solicitud, Proveedor) con sus líneas (no incluye las
    /// notificaciones asociadas — ver <see cref="INotificacionProveedorRepository"/>).
    /// Devuelve null si aún no se le mandó nada de ese pedido a ese proveedor.
    /// </summary>
    Task<PedidoProveedor?> ObtenerPorSolicitudYProveedorAsync(int solicitudId, int proveedorId, CancellationToken ct = default);

    /// <summary>Todos los documentos de un pedido (con sus líneas), uno por proveedor.</summary>
    Task<List<PedidoProveedor>> ObtenerPorSolicitudAsync(int solicitudId, CancellationToken ct = default);

    /// <summary>
    /// Un documento por su Id, con líneas + Proveedor + Solicitud cargados — para regenerar
    /// el PDF/Excel de un envío puntual desde el historial (mismos snapshots que se le
    /// mandaron al proveedor, no el catálogo vivo).
    /// </summary>
    Task<PedidoProveedor?> ObtenerPorIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Persiste un documento NUEVO (primera vez que ese (Solicitud, Proveedor) se notifica).
    /// Idempotente por el índice único (SolicitudId, ProveedorId): si ya existe, no hay que
    /// volver a crear — usar <see cref="ActualizarAsync"/> para anexar líneas.
    /// </summary>
    Task<PedidoProveedor> CrearAsync(PedidoProveedor documento, CancellationToken ct = default);

    /// <summary>Anexa/ajusta las líneas de un documento existente (reenvío) y guarda.</summary>
    Task ActualizarAsync(PedidoProveedor documento, CancellationToken ct = default);
}