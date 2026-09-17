namespace CatalogoPedidos.Domain.Entities;

/// <summary>
/// Registro de que el gestor confirmó la entrega física de los insumos aprobados del
/// pedido <see cref="PedidoId"/>. Es una entidad separada — no un valor más de
/// <c>EstadoSolicitud</c> ni un estado del <see cref="Pedido"/> — porque describe un
/// evento posterior y distinto a la resolución: se entrega lo que ya se aprobó, con su
/// propia fecha, quién la confirmó, a qué dirección y si coincidió con la indicada por
/// el solicitante (ver docs/PLAN_TRAZABILIDAD_ENTREGAS.md, decisión de diseño de la
/// tarea 11). Un pedido tiene como máximo una.
/// </summary>
public class ConfirmacionEntrega
{
    public int Id { get; set; }

    public int PedidoId { get; set; }
    public Pedido? Pedido { get; set; }

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

    public string? Observaciones { get; set; }
}
