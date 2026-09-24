namespace CatalogoPedidos.Domain.Entities;

/// <summary>
/// Sede física de una <see cref="Empresa"/> — nivel operativo donde efectivamente se
/// necesitan los insumos. Una Empresa puede tener varias Sedes (docs/PLAN_EMPRESAS_FILIALES.md);
/// el usuario Solicitante se asocia a la Sede, no directo a la Empresa.
/// </summary>
public class Sede
{
    public int Id { get; set; }

    public int EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Geolocalización de la sede — obligatorios: es lo que la identifica físicamente.</summary>
    public string Pais { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
    public string? Direccion { get; set; }

    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
