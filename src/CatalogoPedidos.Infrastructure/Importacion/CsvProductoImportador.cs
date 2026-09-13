using System.Globalization;
using CatalogoPedidos.Application.Productos;
using CatalogoPedidos.Domain.Entities;
using CsvHelper;
using CsvHelper.Configuration;

namespace CatalogoPedidos.Infrastructure.Importacion;

/// <summary>
/// Importa productos desde un archivo CSV con columnas:
/// Nombre,Descripcion,Categoria,Precio,Stock
/// </summary>
public class CsvProductoImportador(IProductoRepository repositorio) : IProductoImportador
{
    private sealed class FilaCsv
    {
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public int Stock { get; set; }
    }

    public async Task<ImportarProductosResultado> ImportarAsync(Stream archivo, CancellationToken ct = default)
    {
        var resultado = new ImportarProductosResultado();
        var productos = new List<Producto>();

        using var reader = new StreamReader(archivo);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null
        });

        List<FilaCsv> filas;
        try
        {
            filas = csv.GetRecords<FilaCsv>().ToList();
        }
        catch (Exception ex)
        {
            resultado.Errores.Add($"No se pudo leer el archivo: {ex.Message}");
            return resultado;
        }

        var numeroFila = 1;
        foreach (var fila in filas)
        {
            numeroFila++;

            if (string.IsNullOrWhiteSpace(fila.Nombre))
            {
                resultado.Errores.Add($"Fila {numeroFila}: el nombre es obligatorio.");
                continue;
            }

            if (fila.Precio < 0)
            {
                resultado.Errores.Add($"Fila {numeroFila}: el precio no puede ser negativo.");
                continue;
            }

            productos.Add(new Producto
            {
                Nombre = fila.Nombre,
                Descripcion = fila.Descripcion,
                Categoria = fila.Categoria,
                Precio = fila.Precio,
                Stock = fila.Stock,
                Activo = true
            });
        }

        if (productos.Count > 0)
        {
            await repositorio.AgregarRangoAsync(productos, ct);
            resultado.Importados = productos.Count;
        }

        return resultado;
    }
}
