namespace CatalogoPedidos.Application.Solicitudes;

/// <summary>Un proveedor asociado a uno o más productos de un pedido — opción del selector "Enviar a proveedor".</summary>
public class ProveedorDelPedidoDto
{
    public int ProveedorId { get; set; }
    public string ProveedorNombre { get; set; } = string.Empty;
    public int CantidadLineas { get; set; }
}
