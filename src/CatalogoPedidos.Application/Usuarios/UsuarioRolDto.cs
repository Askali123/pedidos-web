namespace CatalogoPedidos.Application.Usuarios;

public class UsuarioRolDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public bool EsGestor { get; set; }

    /// <summary>Sede (de una Empresa filial de Auropaq) a la que pertenece este usuario — null si no tiene ninguna asignada.</summary>
    public int? SedeId { get; set; }
    public string? SedeNombre { get; set; }
    public string? EmpresaNombre { get; set; }
}
