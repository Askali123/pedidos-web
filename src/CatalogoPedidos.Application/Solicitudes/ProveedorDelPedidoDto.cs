namespace CatalogoPedidos.Application.Solicitudes;

/// <summary>Un proveedor asociado a uno o más productos de un pedido — opción del selector "Enviar a proveedor".</summary>
public class ProveedorDelPedidoDto
{
    public int ProveedorId { get; set; }
    public string ProveedorNombre { get; set; } = string.Empty;
    public int CantidadLineas { get; set; }

    /// <summary>
    /// Qué productos de este pedido le corresponden a este proveedor — para que el
    /// selector "Enviar a proveedor" no muestre solo un nombre y un conteo a ciegas,
    /// sobre todo cuando el pedido mezcla productos de varios proveedores distintos.
    /// </summary>
    public List<ProductoDelPedidoDto> Productos { get; set; } = [];
}

public class ProductoDelPedidoDto
{
    public int ProductoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }

    /// <summary>
    /// Código con el que ESE proveedor identifica este producto — para que el selector
    /// "Enviar a proveedor" muestre, además del nombre, la referencia que el proveedor conoce.
    /// </summary>
    public string CodigoProveedor { get; set; } = string.Empty;
}
