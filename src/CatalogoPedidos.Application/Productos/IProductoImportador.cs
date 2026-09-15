namespace CatalogoPedidos.Application.Productos;

/// <summary>
/// Abstracción para importar productos desde un archivo (CSV, Excel, etc.).
/// La implementación concreta vive en Infrastructure, así se puede
/// cambiar el formato de archivo sin tocar Application ni Web.
///
/// Import en dos pasos (analizar → confirmar) para que el gestor pueda revisar
/// qué se va a crear y qué se va a actualizar antes de tocar la base de datos.
/// </summary>
public interface IProductoImportador
{
    /// <summary>
    /// Lee el archivo y clasifica cada fila (nueva / actualización) SIN escribir en la
    /// base de datos. El código de cada fila es el código PROPIO del proveedor indicado
    /// (independiente del Id interno del producto).
    /// </summary>
    Task<ResultadoAnalisisImportacion> AnalizarAsync(Stream archivo, int proveedorId, CancellationToken ct = default);

    /// <summary>Aplica las filas ya analizadas: crea o actualiza cada producto y su asociación con el proveedor.</summary>
    Task<ImportarProductosResultado> ConfirmarAsync(List<FilaImportacion> filas, int proveedorId, CancellationToken ct = default);
}
