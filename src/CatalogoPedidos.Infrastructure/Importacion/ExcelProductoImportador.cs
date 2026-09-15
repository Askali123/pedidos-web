using System.Globalization;
using CatalogoPedidos.Application.Productos;
using CatalogoPedidos.Application.Proveedores;
using CatalogoPedidos.Domain.Entities;
using ClosedXML.Excel;

namespace CatalogoPedidos.Infrastructure.Importacion;

/// <summary>
/// Importa productos desde el Excel de requisición del proveedor, con columnas
/// TIPO, CODIGO, PRODUCTOS, UNIDAD y PRECIO (opcional; CANTIDAD se ignora: esa
/// columna es para pedir, no describe el catálogo).
///
/// El CODIGO es el código PROPIO del proveedor (su SKU/referencia), independiente
/// del Id interno que el sistema le asigna al producto. Se guarda en
/// ProductoProveedor.CodigoProveedor, ligado al proveedor elegido para esta
/// importación, para poder tramitar cada solicitud ante ese proveedor por SU código.
///
/// Si el código ya existe para ese proveedor, se actualiza el producto asociado
/// en vez de duplicarlo (permite reimportar el mismo formato mes a mes). El proceso
/// va en dos pasos — AnalizarAsync (solo lectura) y ConfirmarAsync (escribe) — para
/// que el gestor revise una vista previa antes de tocar la base de datos.
/// </summary>
public class ExcelProductoImportador(
    IProductoRepository productos,
    IProductoProveedorRepository asociaciones) : IProductoImportador
{
    public async Task<ResultadoAnalisisImportacion> AnalizarAsync(Stream archivo, int proveedorId, CancellationToken ct = default)
    {
        var resultado = new ResultadoAnalisisImportacion();

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
        columnas.TryGetValue("PRECIO", out var colPrecio);

        var ultimaFila = hoja.LastRowUsed()?.RowNumber() ?? 1;

        // Si el proveedor repite un CODIGO en dos filas, se detecta aquí en vez de dejar
        // que la segunda pise silenciosamente lo que dejó la primera: se avisa y se
        // conserva solo una fila por código (con los datos de la ÚLTIMA aparición, que es
        // lo que ConfirmarAsync terminaría guardando de todos modos).
        var indicePorCodigo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var primeraFilaPorCodigo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var fila = 2; fila <= ultimaFila; fila++)
        {
            var codigo = ObtenerTexto(hoja, fila, colCodigo);
            var nombre = ObtenerTexto(hoja, fila, colNombre);
            var tipo = ObtenerTexto(hoja, fila, colTipo);
            var unidad = ObtenerTexto(hoja, fila, colUnidad);
            var precio = ObtenerDecimalOpcional(hoja, fila, colPrecio);

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

            var existente = await asociaciones.ObtenerPorProveedorYCodigoAsync(proveedorId, codigo, ct);

            var filaImportacion = new FilaImportacion
            {
                Fila = fila,
                Codigo = codigo,
                Nombre = nombre,
                Categoria = tipo,
                UnidadMedida = unidad,
                PrecioProveedor = precio,
                EsActualizacion = existente?.Producto is not null,
                NombreActual = existente?.Producto?.Nombre
            };

            if (indicePorCodigo.TryGetValue(codigo, out var indiceExistente))
            {
                resultado.Errores.Add($"Fila {fila}: el código '{codigo}' está repetido en este archivo (también en la fila {primeraFilaPorCodigo[codigo]}); se usarán los datos de la fila {fila}, la más reciente.");
                resultado.Filas[indiceExistente] = filaImportacion;
            }
            else
            {
                primeraFilaPorCodigo[codigo] = fila;
                indicePorCodigo[codigo] = resultado.Filas.Count;
                resultado.Filas.Add(filaImportacion);
            }
        }

        return resultado;
    }

    public async Task<ImportarProductosResultado> ConfirmarAsync(List<FilaImportacion> filas, int proveedorId, CancellationToken ct = default)
    {
        var resultado = new ImportarProductosResultado();

        foreach (var fila in filas)
        {
            try
            {
                var existente = await asociaciones.ObtenerPorProveedorYCodigoAsync(proveedorId, fila.Codigo, ct);
                if (existente?.Producto is not null)
                {
                    existente.Producto.Nombre = fila.Nombre;
                    existente.Producto.Categoria = fila.Categoria;
                    existente.Producto.UnidadMedida = fila.UnidadMedida;
                    await productos.ActualizarAsync(existente.Producto, ct);

                    // Solo se toca el precio si el Excel realmente trae uno para esta fila:
                    // si la columna PRECIO no existe o la celda está vacía, no se borra un
                    // precio que el gestor ya hubiera cargado a mano para este proveedor.
                    if (fila.PrecioProveedor.HasValue && existente.PrecioProveedor != fila.PrecioProveedor)
                    {
                        existente.PrecioProveedor = fila.PrecioProveedor;
                        await asociaciones.ActualizarAsync(existente, ct);
                    }

                    resultado.Actualizados++;
                }
                else
                {
                    var producto = await productos.CrearAsync(new Producto
                    {
                        Nombre = fila.Nombre,
                        Categoria = fila.Categoria,
                        UnidadMedida = fila.UnidadMedida,
                        Activo = true
                    }, ct);

                    await asociaciones.CrearAsync(new ProductoProveedor
                    {
                        ProductoId = producto.Id,
                        ProveedorId = proveedorId,
                        CodigoProveedor = fila.Codigo,
                        PrecioProveedor = fila.PrecioProveedor
                    }, ct);

                    resultado.Importados++;
                }
            }
            catch (Exception ex)
            {
                resultado.Errores.Add($"Fila {fila.Fila}: {ex.Message}");
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

    /// <summary>
    /// Lee un precio opcional. Si la celda ya es numérica (lo normal en un Excel real) se
    /// toma tal cual; si viene como texto, se intenta con coma o punto decimal para no
    /// depender de qué cultura tenía configurado quien armó el archivo.
    /// </summary>
    private static decimal? ObtenerDecimalOpcional(IXLWorksheet hoja, int fila, int columna)
    {
        if (columna <= 0)
            return null;

        var celda = hoja.Cell(fila, columna);
        if (celda.IsEmpty())
            return null;

        if (celda.TryGetValue(out decimal numero))
            return numero;

        var texto = celda.GetString().Trim();
        if (string.IsNullOrEmpty(texto))
            return null;

        if (decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariante))
            return invariante;

        if (decimal.TryParse(texto, NumberStyles.Number, CultureInfo.GetCultureInfo("es-CO"), out var local))
            return local;

        return null;
    }
}
