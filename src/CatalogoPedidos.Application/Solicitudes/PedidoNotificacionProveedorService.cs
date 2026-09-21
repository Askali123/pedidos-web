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
    IPdfExportService pdfExport,
    IExcelExportService excelExport) : IPedidoNotificacionProveedorService
{
    /// <summary>
    /// Tiempo mínimo entre dos envíos del MISMO pedido al MISMO proveedor. No es
    /// negocio (el gestor puede reenviar si de verdad hace falta), es solo un freno
    /// contra un loop de clics — antes el envío era un log simulado sin costo, ahora es
    /// un correo real a un tercero.
    /// </summary>
    private static readonly TimeSpan CooldownReenvio = TimeSpan.FromMinutes(5);


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
        var (proveedoresConEnvio, productoIdsYaEnviados) = await ObtenerCoberturaPreviaAsync(pedidoId, todasLasAsociaciones, ct);

        return todasLasAsociaciones
            .GroupBy(a => a.ProveedorId)
            .Select(g =>
            {
                // Una línea que ya se le mandó a OTRO proveedor deja de ofrecerse acá —
                // evita duplicar el pedido real por accidente (ver
                // docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md #2). Reenviarle al mismo
                // proveedor que ya la recibió sigue permitido (protegido aparte por el
                // cooldown de EnviarAProveedorAsync), por eso no se filtra en ese caso.
                var lineas = aprobadas
                    .Where(i => g.Any(a => a.ProductoId == i.ProductoId))
                    .Where(i => proveedoresConEnvio.Contains(g.Key) || !productoIdsYaEnviados.Contains(i.ProductoId))
                    .ToList();
                var codigoPorProducto = g.ToDictionary(a => a.ProductoId, a => a.CodigoProveedor);
                return new ProveedorDelPedidoDto
                {
                    ProveedorId = g.Key,
                    ProveedorNombre = g.First().Proveedor!.Nombre,
                    CantidadLineas = lineas.Count,
                    Productos = lineas
                        .Select(i => new ProductoDelPedidoDto
                        {
                            ProductoId = i.ProductoId,
                            Nombre = i.Producto?.Nombre ?? $"Producto #{i.ProductoId}",
                            Cantidad = i.Cantidad,
                            CodigoProveedor = codigoPorProducto[i.ProductoId]
                        })
                        .ToList()
                };
            })
            .Where(p => p.CantidadLineas > 0)
            .OrderBy(p => p.ProveedorNombre)
            .ToList();
    }

    /// <summary>
    /// A qué proveedores ya se les envió algo de este pedido, y qué productos quedaron
    /// cubiertos por esos envíos (sin importar a cuál proveedor específico) — para no
    /// volver a ofrecer/permitir esas líneas a un proveedor DISTINTO del que ya las
    /// recibió. Ver docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md #2.
    /// </summary>
    private async Task<(HashSet<int> ProveedoresConEnvio, HashSet<int> ProductoIdsYaEnviados)> ObtenerCoberturaPreviaAsync(
        int pedidoId, List<ProductoProveedor> todasLasAsociaciones, CancellationToken ct)
    {
        var enviosDelPedido = await envios.ObtenerPorPedidoAsync(pedidoId, ct);
        var proveedoresConEnvio = enviosDelPedido.Select(e => e.ProveedorId).ToHashSet();
        var productoIdsYaEnviados = todasLasAsociaciones
            .Where(a => proveedoresConEnvio.Contains(a.ProveedorId))
            .Select(a => a.ProductoId)
            .ToHashSet();

        return (proveedoresConEnvio, productoIdsYaEnviados);
    }

    public async Task<EnvioProveedorResultadoDto> EnviarAProveedorAsync(int pedidoId, int proveedorId, string gestorId, string gestorNombre, string? gestorEmail = null, bool incluirExcel = false, CancellationToken ct = default)
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

        var (proveedoresConEnvio, productoIdsYaEnviados) = await ObtenerCoberturaPreviaAsync(pedidoId, todasLasAsociaciones, ct);

        // Mismo criterio que ObtenerProveedoresDisponiblesAsync: no se le manda a un
        // proveedor DISTINTO una línea que ya se le envió a otro (ver
        // docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md #2). Última línea de defensa — la UI
        // ya no debería ofrecer este caso, pero el servicio es quien lo garantiza.
        var lineas = aprobadas
            .Where(i => codigoPorProducto.ContainsKey(i.ProductoId))
            .Where(i => proveedoresConEnvio.Contains(proveedorId) || !productoIdsYaEnviados.Contains(i.ProductoId))
            .ToList();

        if (lineas.Count == 0)
        {
            return new EnvioProveedorResultadoDto
            {
                ProveedorId = proveedor.Id,
                ProveedorNombre = proveedor.Nombre,
                CantidadLineas = 0,
                Enviado = false,
                Motivo = $"Las líneas de este pedido asociadas a {proveedor.Nombre} ya se enviaron a otro proveedor."
            };
        }

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

        var envioPrevio = (await envios.ObtenerPorPedidoAsync(pedidoId, ct))
            .Where(e => e.ProveedorId == proveedorId)
            .OrderByDescending(e => e.FechaEnvio)
            .FirstOrDefault();

        if (envioPrevio is not null)
        {
            var transcurrido = DateTime.UtcNow - envioPrevio.FechaEnvio;
            if (transcurrido < CooldownReenvio)
            {
                var restante = CooldownReenvio - transcurrido;
                var minutosRestantes = Math.Max(1, (int)Math.Ceiling(restante.TotalMinutes));
                return new EnvioProveedorResultadoDto
                {
                    ProveedorId = proveedor.Id,
                    ProveedorNombre = proveedor.Nombre,
                    CantidadLineas = lineas.Count,
                    Enviado = false,
                    Motivo = $"Ya se envió este pedido a {proveedor.Nombre} hace poco. Espera {minutosRestantes} minuto(s) antes de reenviarlo."
                };
            }
        }

        try
        {
            var pdf = pdfExport.ExportarSolicitudesPorProveedor(lineas, codigoPorProducto, proveedor.Nombre);
            var adjuntos = new List<EmailAdjunto> { new($"Pedido-{pedido.Id}-{proveedor.Nombre}.pdf", pdf, "application/pdf") };

            if (incluirExcel)
            {
                var excel = excelExport.ExportarSolicitudesPorProveedor(lineas, codigoPorProducto, proveedor.Nombre);
                adjuntos.Add(new EmailAdjunto($"Pedido-{pedido.Id}-{proveedor.Nombre}.xlsx", excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
            }

            var cuerpo = ArmarCuerpo(pedido, proveedor, lineas.Select(l => (l, codigoPorProducto[l.ProductoId])), incluirExcel);
            var asunto = $"Pedido de reabastecimiento #{pedido.Id} — {proveedor.Nombre}";

            await emailSender.EnviarAsync(proveedor.Email, asunto, cuerpo, adjuntos, copiaA: gestorEmail, ct: ct);

            await envios.CrearAsync(new NotificacionProveedor
            {
                PedidoId = pedido.Id,
                ProveedorId = proveedor.Id,
                Email = proveedor.Email,
                CantidadLineas = lineas.Count,
                FechaEnvio = DateTime.UtcNow,
                GestorId = gestorId,
                GestorNombre = gestorNombre
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

    private static string ArmarCuerpo(Pedido pedido, Proveedor proveedor, IEnumerable<(SolicitudProducto Item, string CodigoProveedor)> lineas, bool incluyeExcel)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Pedido de reabastecimiento #{pedido.Id}");
        sb.AppendLine($"Para: {proveedor.Nombre}");
        sb.AppendLine($"Solicitado por: {pedido.SolicitanteNombre}");
        sb.AppendLine($"Fecha del pedido: {pedido.FechaCreacion:dd/MM/yyyy HH:mm}");
        if (!string.IsNullOrWhiteSpace(pedido.Comentario))
            sb.AppendLine($"Comentario: {pedido.Comentario}");
        sb.AppendLine();
        sb.AppendLine(incluyeExcel
            ? "Adjunto el detalle en PDF y en Excel con el código que ustedes le dan a cada producto."
            : "Adjunto el detalle en PDF con el código que ustedes le dan a cada producto.");
        sb.AppendLine();
        sb.AppendLine("Productos:");
        foreach (var (item, codigoProveedor) in lineas)
            sb.AppendLine($"- {item.Producto?.Nombre} · su código: {codigoProveedor} · cantidad: {item.Cantidad}");

        return sb.ToString();
    }

    public Task<List<NotificacionProveedor>> ObtenerEnviosAsync(int pedidoId, CancellationToken ct = default)
        => envios.ObtenerPorPedidoAsync(pedidoId, ct);

    public Task<List<NotificacionProveedor>> BuscarEnviosAsync(FiltroEnviosProveedorDto filtro, CancellationToken ct = default)
        => envios.BuscarAsync(filtro, ct);
}
