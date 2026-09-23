namespace CatalogoPedidos.Domain.Entities;

/// <summary>
/// Relación muchos-a-muchos entre Producto y Proveedor. Existe como entidad propia
/// (en vez de una simple tabla puente) porque cada proveedor identifica el mismo
/// producto con SU PROPIO código (SKU/referencia), distinto del Id interno del catálogo.
/// </summary>
public class ProductoProveedor
{
    public int Id { get; set; }

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public int ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }

    /// <summary>Código/SKU con el que ESE proveedor identifica el producto (no el Id interno).</summary>
    public string CodigoProveedor { get; set; } = string.Empty;

    public decimal? PrecioProveedor { get; set; }
    public bool EsPreferido { get; set; }

    /// <summary>
    /// Asociación activa o desactivada. Desactivar (en vez de borrar) conserva el histórico:
    /// los <see cref="PedidoProveedor"/> ya emitidos guardan sus propios snapshots, así que
    /// esta desactivación solo afecta pedidos nuevos (ver docs/PLAN_PEDIDO_PROVEEDOR.md, reglas).
    /// </summary>
    public bool Activo { get; set; } = true;

    public DateTime FechaAsociacion { get; set; } = DateTime.UtcNow;
}
