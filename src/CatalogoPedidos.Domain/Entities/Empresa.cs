namespace CatalogoPedidos.Domain.Entities;

/// <summary>
/// Filial del holding Auropaq — nivel agregador. Cada usuario Solicitante pertenece a una
/// <see cref="Sede"/> concreta de una Empresa (ver <see cref="Sede"/>), nunca directo a la
/// Empresa: es en la Sede donde efectivamente se necesitan los insumos (ver
/// docs/PLAN_EMPRESAS_FILIALES.md).
/// </summary>
public class Empresa
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Nit { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<Sede> Sedes { get; set; } = new List<Sede>();
}
