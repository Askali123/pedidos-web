using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Productos;

public interface IProductoService
{
    /// <param name="incluirInactivos">
    /// Por defecto solo trae productos activos (lo que ve un Usuario al armar su carrito).
    /// El Gestor la pone en <c>true</c> en la pantalla "Ver desactivados" del catálogo —
    /// única forma de encontrar un producto dado de baja, ya que no aparece en ningún otro
    /// lado (ver <see cref="DesactivarAsync"/>).
    /// </param>
    Task<List<Producto>> ObtenerCatalogoAsync(string? texto = null, string? categoria = null, bool incluirInactivos = false, CancellationToken ct = default);
    Task<List<string>> ObtenerCategoriasAsync(CancellationToken ct = default);
    Task<Producto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Producto> CrearAsync(CrearProductoDto dto, CancellationToken ct = default);
    Task ActualizarAsync(int id, CrearProductoDto dto, CancellationToken ct = default);
    Task ActualizarStockMinimoAsync(int id, int? stockMinimo, CancellationToken ct = default);

    /// <summary>
    /// Baja lógica (no se borra el producto: conserva su historial de solicitudes). Deja de
    /// aparecer en el catálogo y en los selectores de nuevas solicitudes; sus solicitudes ya
    /// hechas no se ven afectadas.
    /// </summary>
    Task DesactivarAsync(int id, CancellationToken ct = default);

    /// <summary>Revierte <see cref="DesactivarAsync"/>: vuelve a aparecer en el catálogo y en los selectores de nuevas solicitudes.</summary>
    Task ReactivarAsync(int id, CancellationToken ct = default);
    Task<ResultadoAnalisisImportacion> AnalizarImportacionAsync(Stream archivo, int proveedorId, CancellationToken ct = default);
    Task<ImportarProductosResultado> ConfirmarImportacionAsync(List<FilaImportacion> filas, int proveedorId, CancellationToken ct = default);
}
