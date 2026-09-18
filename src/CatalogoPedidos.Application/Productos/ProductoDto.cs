using System.ComponentModel.DataAnnotations;

namespace CatalogoPedidos.Application.Productos;

public class ProductoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public bool Activo { get; set; } = true;
}

public class CrearProductoDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre {2} y {1} caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "El precio no puede ser negativo.")]
    public decimal Precio { get; set; }

    // Sin [Range] a propósito: el stock negativo es un estado real que ya usa la app
    // (aprobaciones que superan lo disponible) — bloquearlo acá rompería poder editar
    // el nombre/precio de un producto que ya quedó en negativo sin forzar antes a
    // corregir el stock. Ver docs/PLAN_MEJORAS_UI_UX.md #1.
    public int Stock { get; set; }

    /// <summary>Umbral opcional para avisarle al gestor cuando el stock quede en o por debajo de este número.</summary>
    public int? StockMinimo { get; set; }

    /// <summary>Proveedor al que se le asocia el producto al crearlo (opcional).</summary>
    public int? ProveedorId { get; set; }

    /// <summary>Código PROPIO de ese proveedor para este producto (obligatorio si se eligió ProveedorId).</summary>
    public string? CodigoProveedor { get; set; }
}

public class ImportarProductosResultado
{
    public int Importados { get; set; }
    public int Actualizados { get; set; }
    public List<string> Errores { get; set; } = new();
}

/// <summary>Una fila ya leída del Excel del proveedor, clasificada antes de tocar la base de datos.</summary>
public class FilaImportacion
{
    public int Fila { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;

    /// <summary>Precio del proveedor para este producto, si el Excel trae una columna PRECIO (opcional).</summary>
    public decimal? PrecioProveedor { get; set; }

    /// <summary>True si ya existe un producto de ese proveedor con ese código (esta fila lo actualizaría en vez de crear uno nuevo).</summary>
    public bool EsActualizacion { get; set; }

    /// <summary>Nombre actual del producto que se actualizaría, para que la vista previa muestre el cambio (solo si EsActualizacion).</summary>
    public string? NombreActual { get; set; }
}

/// <summary>Resultado de leer y clasificar el Excel, previo a pedirle confirmación al gestor.</summary>
public class ResultadoAnalisisImportacion
{
    public List<FilaImportacion> Filas { get; set; } = new();
    public List<string> Errores { get; set; } = new();

    public int TotalNuevos => Filas.Count(f => !f.EsActualizacion);
    public int TotalActualizaciones => Filas.Count(f => f.EsActualizacion);
}
