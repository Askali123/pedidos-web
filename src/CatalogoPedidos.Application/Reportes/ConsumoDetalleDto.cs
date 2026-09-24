using CatalogoPedidos.Domain.Enums;

namespace CatalogoPedidos.Application.Reportes;

/// <summary>
/// Una línea de consumo para el dashboard de solicitudes por empresa/sede — ver
/// docs/PLAN_EMPRESAS_FILIALES.md, Etapa 6.
/// </summary>
public class ConsumoDetalleDto
{
    public int SolicitudId { get; set; }
    public int DetalleId { get; set; }
    public DateTime Fecha { get; set; }
    public string SolicitanteNombre { get; set; } = string.Empty;
    public string? EmpresaNombre { get; set; }
    public string? SedeNombre { get; set; }
    public string ProductoNombre { get; set; } = string.Empty;
    public string? Categoria { get; set; }

    /// <summary>Código interno del catálogo — hoy es <c>Producto.Id</c> (ver Tarea 6 de PLAN_FUNCIONALIDADES_NEGOCIO.md, todavía no hay un SKU propio).</summary>
    public int CodigoInterno { get; set; }

    /// <summary>
    /// El que se usó al enviarle esta línea a un proveedor (snapshot congelado), o el del
    /// proveedor preferido como referencia si todavía no se envió — null si el producto no
    /// tiene ningún proveedor asociado.
    /// </summary>
    public string? CodigoProveedor { get; set; }

    public int Cantidad { get; set; }
    public EstadoSolicitud Estado { get; set; }
}
