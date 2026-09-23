using System.ComponentModel.DataAnnotations;

namespace CatalogoPedidos.Application.Proveedores;

public class CrearProveedorDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Nit { get; set; }
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }

    [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
    public string? Email { get; set; }
}

public class AsociarProveedorDto
{
    public int ProductoId { get; set; }
    public int ProveedorId { get; set; }
    public string CodigoProveedor { get; set; } = string.Empty;
    public decimal? PrecioProveedor { get; set; }
    public bool EsPreferido { get; set; }
}
