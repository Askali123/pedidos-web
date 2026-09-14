using CatalogoPedidos.Application.Productos;
using CatalogoPedidos.Application.Proveedores;
using CatalogoPedidos.Domain.Entities;
using ClosedXML.Excel;

namespace CatalogoPedidos.Infrastructure.Importacion;

/// <summary>
/// Importa productos desde el Excel de requisición del proveedor, con columnas
/// TIPO, CODIGO, PRODUCTOS, UNIDAD (CANTIDAD se ignora: esa columna es para pedir,
/// no describe el catálogo).
///
/// El CODIGO es el código PROPIO del proveedor (su SKU/referencia), independiente
/// del Id interno que el sistema le asigna al producto. Se guarda en
/// ProductoProveedor.CodigoProveedor, ligado al proveedor elegido para esta
/// importación, para poder tramitar cada solicitud ante ese proveedor por SU código.
///
/// Si el código ya existe para ese proveedor, se actualiza el producto asociado
/// en vez de duplicarlo (permite reimportar el mismo formato mes a mes).
/// </summary>
public class ExcelProductoImportador(
    IProductoRepository productos,
    IProductoProveedorRepository asociaciones) : IProductoImportador
{
    public async Task<ImportarProductosResultado> ImportarAsync(Stream archivo, int proveedorId, CancellationToken ct = default)
    {
        var resultado = new ImportarProductosResultado();

        using var libro = new XLWorkbook(archivo);
        var hoja = libro.Worksheets.FirstOrDefault();
        if (hoja is null)
        {
            resultado.Errores.Add("El archivo no tiene hojas.");
            return resultado;
        }

        var columnas = MapearColumnas(hoja);
        if (!columnas.TryGetValue("CODIGO", out var colCodigo) ||
            !columnas.TryGetValue("PRODUCTOS", out var colNombre))
        {
            resultado.Errores.Add("El archivo debe tener columnas 'CODIGO' y 'PRODUCTOS' en la primera fila.");
            return resultado;
        }

        columnas.TryGetValue("TIPO", out var colTipo);
        columnas.TryGetValue("UNIDAD", out var colUnidad);

        var ultimaFila = hoja.LastRowUsed()?.RowNumber() ?? 1;

        for (var fila = 2; fila <= ultimaFila; fila++)
        {
            var codigo = ObtenerTexto(hoja, fila, colCodigo);
            var nombre = ObtenerTexto(hoja, fila, colNombre);
            var tipo = ObtenerTexto(hoja, fila, colTipo);
            var unidad = ObtenerTexto(hoja, fila, colUnidad);

            if (string.IsNullOrWhiteSpace(codigo) && string.IsNullOrWhiteSpace(nombre))
                continue; // fila vacía

            if (string.IsNullOrWhiteSpace(codigo))
            {
                resultado.Errores.Add($"Fila {fila}: falta el CODIGO del proveedor.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(nombre))
            {
                resultado.Errores.Add($"Fila {fila}: falta el nombre del producto.");
                continue;
            }

            try
            {
                var existente = await asociaciones.ObtenerPorProveedorYCodigoAsync(proveedorId, codigo, ct);
                if (existente?.Producto is not null)
                {
                    existente.Producto.Nombre = nombre;
                    existente.Producto.Categoria = tipo;
                    existente.Producto.UnidadMedida = unidad;
                    await productos.ActualizarAsync(existente.Producto, ct);
                    resultado.Actualizados++;
                }
                else
                {
                    var producto = await productos.CrearAsync(new Producto
                    {
                        Nombre = nombre,
                        Categoria = tipo,
                        UnidadMedida = unidad,
                        Activo = true
                    }, ct);

                    await asociaciones.CrearAsync(new ProductoProveedor
                    {
                        ProductoId = producto.Id,
                        ProveedorId = proveedorId,
                        CodigoProveedor = codigo
                    }, ct);

                    resultado.Importados++;
                }
            }
            catch (Exception ex)
            {
                resultado.Errores.Add($"Fila {fila}: {ex.Message}");
            }
        }

        return resultado;
    }

    private static Dictionary<string, int> MapearColumnas(IXLWorksheet hoja)
    {
        var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var filaEncabezado = hoja.Row(1);
        var ultimaColumna = hoja.LastColumnUsed()?.ColumnNumber() ?? 0;

        for (var col = 1; col <= ultimaColumna; col++)
        {
            var titulo = filaEncabezado.Cell(col).GetString().Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(titulo) && !mapa.ContainsKey(titulo))
                mapa[titulo] = col;
        }

        return mapa;
    }

    private static string ObtenerTexto(IXLWorksheet hoja, int fila, int columna)
        => columna <= 0 ? string.Empty : hoja.Cell(fila, columna).GetString().Trim();
}
