namespace CatalogoPedidos.Application.Productos;

/// <summary>
/// Abstracción para importar productos desde un archivo (CSV, Excel, etc.).
/// La implementación concreta vive en Infrastructure, así se puede
/// cambiar el formato de archivo sin tocar Application ni Web.
/// </summary>
public interface IProductoImportador
{
    /// <summary>
    /// Importa productos asociándolos al proveedor indicado: el código de cada fila
    /// es el código PROPIO de ese proveedor (independiente del Id interno del producto).
    /// </summary>
    Task<ImportarProductosResultado> ImportarAsync(Stream archivo, int proveedorId, CancellationToken ct = default);
}
