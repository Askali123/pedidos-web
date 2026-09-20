namespace CatalogoPedidos.Application.Solicitudes;

/// <summary>
/// Un "grupo de entrega" dentro de un pedido: las líneas Aprobadas que realmente se le
/// enviaron a un proveedor puntual (hay un <see cref="NotificacionProveedor"/> que las
/// cubre), o las que quedaron sin cubrir por ningún envío (<see cref="ProveedorId"/> es
/// <c>null</c> — compra local/manual, o todavía no se le mandó nada a nadie). Cada grupo
/// se confirma por separado con su propia <see cref="Domain.Entities.ConfirmacionEntrega"/>
/// (ver docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md, tareas 3 y 4).
/// </summary>
public class GrupoEntregaDto
{
    public int? ProveedorId { get; set; }

    /// <summary><c>null</c> cuando <see cref="ProveedorId"/> es <c>null</c> (grupo "sin proveedor asociado").</summary>
    public string? ProveedorNombre { get; set; }

    public int CantidadLineas { get; set; }

    /// <summary>Si este grupo ya tiene una <see cref="Domain.Entities.ConfirmacionEntrega"/> registrada.</summary>
    public bool Confirmado { get; set; }

    /// <summary>
    /// Qué productos (y cuánto de cada uno) cubre este grupo — mismo DTO que ya usa
    /// <see cref="ProveedorDelPedidoDto"/> para el selector "Enviar a proveedor". Lo usa
    /// <see cref="IConfirmacionEntregaService.ConfirmarAsync"/> para saber cuánto
    /// reponerle al stock de cada producto al confirmar (ver
    /// docs/PLAN_CALIDAD_INGENIERIA.md, tarea 1).
    /// </summary>
    public List<ProductoDelPedidoDto> Productos { get; set; } = [];
}

/// <summary>Cuántos grupos de entrega tiene un pedido y cuántos ya están confirmados — para mostrar un progreso agregado en vez de un único ✓/nada.</summary>
public class EntregaProgresoDto
{
    public int TotalGrupos { get; set; }
    public int GruposConfirmados { get; set; }

    public bool TodoEntregado => TotalGrupos > 0 && GruposConfirmados == TotalGrupos;
    public bool AlgoEntregado => GruposConfirmados > 0;
}
