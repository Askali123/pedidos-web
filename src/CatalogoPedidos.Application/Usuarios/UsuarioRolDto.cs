namespace CatalogoPedidos.Application.Usuarios;

public class UsuarioRolDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public bool EsGestor { get; set; }
}
