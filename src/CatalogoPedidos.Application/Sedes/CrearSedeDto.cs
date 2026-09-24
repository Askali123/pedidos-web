using System.ComponentModel.DataAnnotations;

namespace CatalogoPedidos.Application.Sedes;

public class CrearSedeDto
{
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Pais { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }

    [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
    public string? Email { get; set; }
}
