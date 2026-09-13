namespace CatalogoPedidos.Application.Productos;

/// <summary>
/// Abstracción para importar productos desde un archivo (CSV, Excel, etc.).
/// La implementación concreta vive en Infrastructure, así se puede
/// cambiar el formato de archivo sin tocar Application ni Web.
/// </summary>
public interface IProductoImportador
{
    Task<ImportarProductosResultado> ImportarAsync(Stream archivo, CancellationToken ct = default);
}
