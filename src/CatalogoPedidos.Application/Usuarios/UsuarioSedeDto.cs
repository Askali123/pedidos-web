namespace CatalogoPedidos.Application.Usuarios;

/// <summary>Snapshot de a qué Sede/Empresa pertenece un usuario, resuelto en el momento de la consulta.</summary>
public class UsuarioSedeDto
{
    public int SedeId { get; set; }
    public string SedeNombre { get; set; } = string.Empty;
    public int EmpresaId { get; set; }
    public string EmpresaNombre { get; set; } = string.Empty;
}
