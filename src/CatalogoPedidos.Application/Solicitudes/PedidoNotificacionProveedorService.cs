using System.Text;
using CatalogoPedidos.Application.Exportacion;
using CatalogoPedidos.Application.Notificaciones;
using CatalogoPedidos.Application.Proveedores;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Domain.Enums;

namespace CatalogoPedidos.Application.Solicitudes;

public class PedidoNotificacionProveedorService(
    ISolicitudRepository solicitudes,
    IProductoProveedorRepository asociaciones,
    INotificacionProveedorRepository envios,
    IEmailSender emailSender,
    IPdfExportService pdfExport) : IPedidoNotificacionProveedorService
{
    public async Task<List<ProveedorDelPedidoDto>> ObtenerProveedoresDisponiblesAsync(int pedidoId, CancellationToken ct = default)
    {
        var pedido = await solicitudes.ObtenerPedidoAsync(pedidoId, ct)
            ?? throw new InvalidOperationException("El pedido no existe.");

        // Solo lo aprobado tiene sentido pedirle al proveedor — lo pendiente todavía no se
        // decidió, y lo rechazado es justo lo que NO se necesita.
        var aprobadas = pedido.Items.Where(i => i.Estado == EstadoSolicitud.Aprobada).ToList();
        if (aprobadas.Count == 0)
            return [];

        var productoIds = aprobadas.Select(i => i.ProductoId).Distinct();
        var todasLasAsociaciones = await asociaciones.ObtenerPorProductosAsync(productoIds, ct);

        return todasLasAsociaciones
            .GroupBy(a => a.ProveedorId)
            .Select(g => new ProveedorDelPedidoDto
            {
                ProveedorId = g.Key,
                ProveedorNombre = g.First().Proveedor!.Nombre,
                CantidadLineas = aprobadas.Count(i => g.Any(a => a.ProductoId == i.ProductoId))
            })
            .Where(p => p.CantidadLineas > 0)
            .OrderBy(p => p.ProveedorNombre)
            .ToList();
    }

    public async Task<EnvioProveedorResultadoDto> EnviarAProveedorAsync(int pedidoId, int proveedorId, CancellationToken ct = default)
    {
        var pedido = await solicitudes.ObtenerPedidoAsync(pedidoId, ct)
            ?? throw new InvalidOperationException("El pedido no existe.");

        // Mismo criterio que ObtenerProveedoresDisponiblesAsync: nunca se le manda al
        // proveedor una línea que no esté Aprobada (ver docs/PLAN_FLUJO_PEDIDO_PROVEEDOR.md #1).
        var aprobadas = pedido.Items.Where(i => i.Estado == EstadoSolicitud.Aprobada).ToList();

        var productoIds = aprobadas.Select(i => i.ProductoId).Distinct();
        var todasLasAsociaciones = await asociaciones.ObtenerPorProductosAsync(productoIds, ct);
        var deEsteProveedor = todasLasAsociaciones.Where(a => a.ProveedorId == proveedorId).ToList();

        if (deEsteProveedor.Count == 0)
            throw new InvalidOperationException("Ninguna línea aprobada de este pedido está asociada a ese proveedor.");

        var proveedor = deEsteProveedor[0].Proveedor!;
        var codigoPorProducto = deEsteProveedor.ToDictionary(a => a.ProductoId, a => a.CodigoProveedor);
        var lineas = aprobadas.Where(i => codigoPorProducto.ContainsKey(i.ProductoId)).ToList();

        if (string.IsNullOrWhiteSpace(proveedor.Email))
        {
            return new EnvioProveedorResultadoDto
            {
                ProveedorId = proveedor.Id,
                ProveedorNombre = proveedor.Nombre,
                CantidadLineas = lineas.Count,
                Enviado = false,
                Motivo = "El proveedor no tiene correo registrado."
            };
        }

        try
        {
            var pdf = pdfExport.ExportarSolicitudesPorProveedor(lineas, codigoPorProducto, proveedor.Nombre);
            var adjunto = new EmailAdjunto($"Pedido-{pedido.Id}-{proveedor.Nombre}.pdf", pdf, "application/pdf");
            var cuerpo = ArmarCuerpo(pedido, proveedor, lineas.Select(l => (l, codigoPorProducto[l.ProductoId])));
            var asunto = $"Pedido de reabastecimiento #{pedido.Id} — {proveedor.Nombre}";

            await emailSender.EnviarAsync(proveedor.Email, asunto, cuerpo, adjunto, ct);

            await envios.CrearAsync(new NotificacionProveedor
            {
                PedidoId = pedido.Id,
                ProveedorId = proveedor.Id,
                Email = proveedor.Email,
                CantidadLineas = lineas.Count,
                FechaEnvio = DateTime.UtcNow
            }, ct);

            return new EnvioProveedorResultadoDto
            {
                ProveedorId = proveedor.Id,
                ProveedorNombre = proveedor.Nombre,
                CantidadLineas = lineas.Count,
                Enviado = true
            };
        }
        catch (Exception ex)
        {
            // El envío es la acción que pidió el gestor (no un efecto secundario best-effort
            // como las notificaciones internas) — si falla, se lo mostramos, no lo tragamos.
            return new EnvioProveedorResultadoDto
            {
                ProveedorId = proveedor.Id,
                ProveedorNombre = proveedor.Nombre,
                CantidadLineas = lineas.Count,
                Enviado = false,
                Motivo = $"No se pudo enviar el correo: {ex.Message}"
            };
        }
    }

    private static string ArmarCuerpo(Pedido pedido, Proveedor proveedor, IEnumerable<(SolicitudProducto Item, string CodigoProveedor)> lineas)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Pedido de reabastecimiento #{pedido.Id}");
        sb.AppendLine($"Para: {proveedor.Nombre}");
        sb.AppendLine($"Solicitado por: {pedido.SolicitanteNombre}");
        sb.AppendLine($"Fecha del pedido: {pedido.FechaCreacion:dd/MM/yyyy HH:mm}");
        if (!string.IsNullOrWhiteSpace(pedido.Comentario))
            sb.AppendLine($"Comentario: {pedido.Comentario}");
        sb.AppendLine();
        sb.AppendLine("Adjunto el detalle en PDF con el código que ustedes le dan a cada producto.");
        sb.AppendLine();
        sb.AppendLine("Productos:");
        foreach (var (item, codigoProveedor) in lineas)
            sb.AppendLine($"- {item.Producto?.Nombre} · su código: {codigoProveedor} · cantidad: {item.Cantidad}");

        return sb.ToString();
    }

    public Task<List<NotificacionProveedor>> ObtenerEnviosAsync(int pedidoId, CancellationToken ct = default)
        => envios.ObtenerPorPedidoAsync(pedidoId, ct);
}
