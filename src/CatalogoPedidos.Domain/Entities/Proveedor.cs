namespace CatalogoPedidos.Domain.Entities;

public class Proveedor
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Nit { get; set; }
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<ProductoProveedor> Productos { get; set; } = new List<ProductoProveedor>();
}
