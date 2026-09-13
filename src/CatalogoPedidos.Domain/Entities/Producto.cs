namespace CatalogoPedidos.Domain.Entities;

public class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<SolicitudProducto> Solicitudes { get; set; } = new List<SolicitudProducto>();
    public ICollection<ProductoProveedor> Proveedores { get; set; } = new List<ProductoProveedor>();
}
