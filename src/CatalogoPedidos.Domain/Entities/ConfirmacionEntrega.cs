namespace CatalogoPedidos.Domain.Entities;

/// <summary>
/// Registro de que el gestor confirmó la entrega física de los insumos aprobados del
/// pedido <see cref="PedidoId"/>, para el proveedor <see cref="ProveedorId"/> (o para las
/// líneas sin proveedor asociado, cuando es <c>null</c>). Es una entidad separada — no un
/// valor más de <c>EstadoSolicitud</c> ni un estado único del <see cref="Pedido"/> —
/// porque describe un evento posterior y distinto a la resolución: se entrega lo que ya
/// se aprobó, con su propia fecha, quién la confirmó, a qué dirección y si coincidió con
/// la indicada por el solicitante (ver docs/PLAN_TRAZABILIDAD_ENTREGAS.md, decisión de
/// diseño de la tarea 11).
///
/// Es 1:muchos con <see cref="Pedido"/> — un pedido con productos de varios proveedores
/// puede recibir sus entregas en fechas distintas, así que cada proveedor (más el
/// eventual "sin proveedor asociado") tiene su propia confirmación en vez de una sola
/// para todo el pedido (ver docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md, tarea 3). La
/// unicidad — un proveedor no se confirma dos veces en el mismo pedido — se valida en el
/// servicio, no con un índice único de base de datos, para no pelear con la semántica de
/// NULL de SQL Server en índices únicos (mismo criterio que ya usa el resto de la app,
/// ej. NIT de proveedor).
/// </summary>
public class ConfirmacionEntrega
{
    public int Id { get; set; }

    public int PedidoId { get; set; }
    public Pedido? Pedido { get; set; }

    /// <summary>
    /// A qué proveedor corresponde esta entrega. <c>null</c> significa "líneas aprobadas
    /// que nunca se asociaron a ningún proveedor" (compra local/manual, sin correo de por
    /// medio) — sigue siendo confirmable como antes de esta migración, no se pierde esa
    /// posibilidad.
    /// </summary>
    public int? ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }

    public DateTime FechaEntrega { get; set; } = DateTime.UtcNow;

    public string ConfirmadoPorId { get; set; } = string.Empty;
    public string ConfirmadoPorNombre { get; set; } = string.Empty;

    /// <summary>
    /// Dónde se entregó en la práctica. Se prellena con <c>Pedido.DireccionEntrega</c> al
    /// abrir el formulario de confirmación, pero es editable en ese momento — puede
    /// terminar entregándose en un lugar distinto al indicado originalmente.
    /// </summary>
    public string? DireccionEntregada { get; set; }

    /// <summary>Si <see cref="DireccionEntregada"/> coincide con lo que pidió el solicitante.</summary>
    public bool CoincideConDireccionIndicada { get; set; }

    /// <summary>
    /// Quién recibió físicamente los insumos — texto libre y opcional, distinto de
    /// <see cref="ConfirmadoPorNombre"/> (que es siempre el gestor que llena este
    /// formulario desde la app, no necesariamente quien los recibió).
    /// </summary>
    public string? RecibidoPorNombre { get; set; }

    public string? Observaciones { get; set; }
}
