namespace CatalogoPedidos.Domain.Entities;

/// <summary>
/// Línea de un <see cref="PedidoProveedor"/>. Guarda los datos como SNAPSHOT
/// (<see cref="ProductoNombre"/>, <see cref="Cantidad"/>, <see cref="CodigoProveedor"/>,
/// <see cref="UnidadMedida"/>, <see cref="PrecioProveedor"/>) para que un cambio posterior
/// en el catálogo — renombrar/desactivar el producto o modificar la asociación
/// <see cref="ProductoProveedor"/> (p.ej. cambiar el código del proveedor) — NO altere
/// pedidos ya emitidos. El código con el que se le pidió al proveedor queda congelado en el
/// momento del envío (ver docs/PLAN_PEDIDO_PROVEEDOR.md, sección 5).
/// </summary>
public class DetallePedidoProveedor
{
    public int Id { get; set; }

    public int PedidoProveedorId { get; set; }
    public PedidoProveedor? PedidoProveedor { get; set; }

    /// <summary>Referencia al producto (para joins/badges), el nombre/descripción vivos NO se usan acá.</summary>
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public string ProductoNombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public string CodigoProveedor { get; set; } = string.Empty;
    public string? UnidadMedida { get; set; }
    public decimal? PrecioProveedor { get; set; }
    public string? Categoria { get; set; }

    /// <summary>
    /// True cuando el gestor quitó manualmente esta línea de ESTE pedido a este proveedor.
    /// No afecta la asociación ProductoProveedor del catálogo (ver
    /// docs/PLAN_PEDIDO_PROVEEDOR.md, regla de negocio "quitar de un pedido ≠ tocar el catálogo").
    /// </summary>
    public bool Excluido { get; set; }
}