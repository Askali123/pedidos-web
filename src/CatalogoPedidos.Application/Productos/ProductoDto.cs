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
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public decimal Precio { get; set; }
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
