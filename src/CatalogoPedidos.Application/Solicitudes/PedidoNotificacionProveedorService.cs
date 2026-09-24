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
    IPedidoProveedorRepository pedidosProveedor,
    IEmailSender emailSender,
    IPdfExportService pdfExport,
    IExcelExportService excelExport) : IPedidoNotificacionProveedorService
{
    /// <summary>
    /// Tiempo mínimo entre dos envíos de la MISMA solicitud al MISMO proveedor. No es
    /// negocio (el gestor puede reenviar si de verdad hace falta), es solo un freno
    /// contra un loop de clics — antes el envío era un log simulado sin costo, ahora es
    /// un correo real a un tercero.
    /// </summary>
    private static readonly TimeSpan CooldownReenvio = TimeSpan.FromMinutes(5);


    public async Task<List<ProveedorDelPedidoDto>> ObtenerProveedoresDisponiblesAsync(int solicitudId, CancellationToken ct = default)
    {
        var solicitud = await solicitudes.ObtenerSolicitudAsync(solicitudId, ct)
            ?? throw new InvalidOperationException("La solicitud no existe.");

        // Solo lo aprobado tiene sentido pedirle al proveedor — lo pendiente todavía no se
        // decidió, y lo rechazado es justo lo que NO se necesita.
        var aprobadas = solicitud.Items.Where(i => i.Estado == EstadoSolicitud.Aprobada).ToList();
        if (aprobadas.Count == 0)
            return [];

        var productoIds = aprobadas.Select(i => i.ProductoId).Distinct();
        var todasLasAsociaciones = await asociaciones.ObtenerPorProductosAsync(productoIds, ct);
        var (proveedoresConEnvio, productoIdsYaEnviados) = await ObtenerCoberturaPreviaAsync(solicitudId, ct);

        // Línea que el gestor EXCLUYÓ de este proveedor (Excluido=true en el doc) no se
        // vuelve a ofrecer ni a sumar al conteo, para que el selector refleje lo pendiente.
        var excluidosPorProveedor = (await pedidosProveedor.ObtenerPorSolicitudAsync(solicitudId, ct))
            .ToDictionary(
                d => d.ProveedorId,
                d => d.Items.Where(i => i.Excluido).Select(i => i.ProductoId).ToHashSet());

        // Solo se ofrecen proveedores con asociación ACTIVA Y proveedor ACTIVO (un proveedor
        // desactivado no debe poder recibir pedidos nuevos, sin importar si su asociación de
        // catálogo quedó activa por algún motivo — chequeo defensivo, no solo confiar en la
        // cascada de ProveedorService.DesactivarAsync; ver docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md,
        // tarea 9). La cobertura previa sigue estando cubierta aunque esa asociación o el
        // proveedor se desactiven después, porque se calcula desde el documento REALMENTE
        // enviado (PedidoProveedor.Items), no desde el catálogo vivo.
        return todasLasAsociaciones
            .Where(a => a.Activo && a.Proveedor!.Activo)
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
                    .Where(i => !excluidosPorProveedor.TryGetValue(g.Key, out var excluidos) || !excluidos.Contains(i.ProductoId))
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
    /// A qué proveedores ya se les envió esta solicitud, y qué productos quedaron
    /// REALMENTE cubiertos por esos envíos (sin importar a cuál proveedor específico) —
    /// para no volver a ofrecer/permitir esas líneas a un proveedor DISTINTO del que ya
    /// las recibió. Ver docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md #2.
    ///
    /// Se calcula desde <see cref="PedidoProveedor.Items"/> (el documento con snapshots
    /// que de verdad se mandó), no desde el catálogo — 2026-09-23: usar
    /// "cualquier asociación de catálogo con un proveedor que ya recibió algo" bloqueaba
    /// productos que NUNCA se le mandaron a ese proveedor (bastaba con tener una
    /// asociación vieja/inactiva con él para quedar marcados como "ya cubiertos" y no
    /// poder ofrecerse a un proveedor nuevo). Las líneas <c>Excluido</c> tampoco cuentan
    /// como cubiertas: quitar una línea de ESTE proveedor la deja libre para otro.
    /// </summary>
    private async Task<(HashSet<int> ProveedoresConEnvio, HashSet<int> ProductoIdsYaEnviados)> ObtenerCoberturaPreviaAsync(
        int solicitudId, CancellationToken ct)
    {
        var documentos = await pedidosProveedor.ObtenerPorSolicitudAsync(solicitudId, ct);
        var proveedoresConEnvio = documentos.Select(d => d.ProveedorId).ToHashSet();
        var productoIdsYaEnviados = documentos
            .SelectMany(d => d.Items.Where(i => !i.Excluido))
            .Select(i => i.ProductoId)
            .ToHashSet();

        return (proveedoresConEnvio, productoIdsYaEnviados);
    }

    public async Task<EnvioProveedorResultadoDto> EnviarAProveedorAsync(int solicitudId, int proveedorId, string gestorId, string gestorNombre, string? gestorEmail = null, bool incluirExcel = false, IEnumerable<int>? productoIdsSeleccionados = null, CancellationToken ct = default)
    {
        var solicitud = await solicitudes.ObtenerSolicitudAsync(solicitudId, ct)
            ?? throw new InvalidOperationException("La solicitud no existe.");

        // Mismo criterio que ObtenerProveedoresDisponiblesAsync: nunca se le manda al
        // proveedor una línea que no esté Aprobada (ver docs/PLAN_FLUJO_PEDIDO_PROVEEDOR.md #1).
        var aprobadas = solicitud.Items.Where(i => i.Estado == EstadoSolicitud.Aprobada).ToList();

        var productoIds = aprobadas.Select(i => i.ProductoId).Distinct();
        var todasLasAsociaciones = await asociaciones.ObtenerPorProductosAsync(productoIds, ct);
        // Chequeo defensivo del proveedor mismo, no solo de la asociación — ver
        // docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md, tarea 9.
        var deEsteProveedor = todasLasAsociaciones.Where(a => a.ProveedorId == proveedorId && a.Activo && a.Proveedor!.Activo).ToList();

        if (deEsteProveedor.Count == 0)
            throw new InvalidOperationException("Ninguna línea aprobada de esta solicitud está asociada a ese proveedor (o el proveedor está desactivado).");

        var proveedor = deEsteProveedor[0].Proveedor!;
        var codigoPorProducto = deEsteProveedor.ToDictionary(a => a.ProductoId, a => a.CodigoProveedor);

        var (proveedoresConEnvio, productoIdsYaEnviados) = await ObtenerCoberturaPreviaAsync(solicitudId, ct);

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
                Motivo = $"Las líneas de esta solicitud asociadas a {proveedor.Nombre} ya se enviaron a otro proveedor."
            };
        }

        // La selección del gestor en la confirmación (checkboxes): las líneas que quedan
        // desmarcadas se EXCLUYEN de este proveedor de forma persistente (Excluido=true en
        // el documento), así no se vuelven a ofrecer ni a sumar en reenvíos. Las que ya
        // formaban parte de envíos anteriores no se tocan (esas ya se pidieron de verdad).
        var seleccionadas = productoIdsSeleccionados is null
            ? lineas
            : lineas.Where(l => productoIdsSeleccionados.Contains(l.ProductoId)).ToList();

        if (seleccionadas.Count == 0)
        {
            return new EnvioProveedorResultadoDto
            {
                ProveedorId = proveedor.Id,
                ProveedorNombre = proveedor.Nombre,
                CantidadLineas = 0,
                Enviado = false,
                Motivo = "No quedó ninguna línea seleccionada para este proveedor."
            };
        }

        var excluidasDeEsteEnvio = lineas.Except(seleccionadas).ToList();

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

        var envioPrevio = (await envios.ObtenerPorSolicitudAsync(solicitudId, ct))
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
                    Motivo = $"Ya se envió esta solicitud a {proveedor.Nombre} hace poco. Espera {minutosRestantes} minuto(s) antes de reenviarlo."
                };
            }
        }

        try
        {
            // Snapshot de cada línea en el momento del envío: el nombre, el código del
            // proveedor, la unidad y el precio que el proveedor VE quedan congelados en el
            // documento (ver DetallePedidoProveedor y docs/PLAN_PEDIDO_PROVEEDOR.md §5) — un
            // cambio posterior del catálogo no altera pedidos ya emitidos.
            var detallesEnviados = ConstruirSnapshots(seleccionadas, deEsteProveedor, excluido: false);
            var detallesExcluidos = ConstruirSnapshots(excluidasDeEsteEnvio, deEsteProveedor, excluido: true);
            var aPersistir = detallesEnviados.Concat(detallesExcluidos).ToList();

            var pdf = pdfExport.ExportarPedidoProveedor(CrearDocumentoTransitorio(solicitud, proveedor, detallesEnviados), proveedor.Nombre);
            var adjuntos = new List<EmailAdjunto> { new($"Pedido-{solicitud.Id}-{proveedor.Nombre}.pdf", pdf, "application/pdf") };

            if (incluirExcel)
            {
                var excel = excelExport.ExportarPedidoProveedor(CrearDocumentoTransitorio(solicitud, proveedor, detallesEnviados), proveedor.Nombre);
                adjuntos.Add(new EmailAdjunto($"Pedido-{solicitud.Id}-{proveedor.Nombre}.xlsx", excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
            }

            var cuerpo = ArmarCuerpo(solicitud, proveedor, detallesEnviados, incluirExcel);
            var asunto = $"Pedido de reabastecimiento #{solicitud.Id} — {proveedor.Nombre}";

            await emailSender.EnviarAsync(proveedor.Email, asunto, cuerpo, adjuntos, copiaA: gestorEmail, ct: ct);

            var documento = await ObtenerOCrearDocumentoAsync(solicitud.Id, proveedor.Id, gestorId, gestorNombre, aPersistir, ct);

            await envios.CrearAsync(new NotificacionProveedor
            {
                SolicitudId = solicitud.Id,
                ProveedorId = proveedor.Id,
                Email = proveedor.Email,
                CantidadLineas = seleccionadas.Count,
                FechaEnvio = DateTime.UtcNow,
                GestorId = gestorId,
                GestorNombre = gestorNombre,
                PedidoProveedorId = documento.Id
            }, ct);

            return new EnvioProveedorResultadoDto
            {
                ProveedorId = proveedor.Id,
                ProveedorNombre = proveedor.Nombre,
                CantidadLineas = seleccionadas.Count,
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
                CantidadLineas = seleccionadas.Count,
                Enviado = false,
                Motivo = $"No se pudo enviar el correo: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Convierte las líneas aprobadas (para este proveedor) en snapshots congelados,
    /// cruzando con la asociación <see cref="ProductoProveedor"/> para sacar el código del
    /// proveedor y su precio, y con el producto para la unidad de medida del catálogo.
    /// </summary>
    private static List<DetallePedidoProveedor> ConstruirSnapshots(List<DetalleSolicitud> lineas, List<ProductoProveedor> deEsteProveedor, bool excluido)
    {
        var precioPorProducto = deEsteProveedor.ToDictionary(a => a.ProductoId, a => a.PrecioProveedor);
        var codigoPorProducto = deEsteProveedor.ToDictionary(a => a.ProductoId, a => a.CodigoProveedor);

        return lineas.Select(l => new DetallePedidoProveedor
        {
            ProductoId = l.ProductoId,
            ProductoNombre = l.Producto?.Nombre ?? $"Producto #{l.ProductoId}",
            Cantidad = l.Cantidad,
            CodigoProveedor = codigoPorProducto[l.ProductoId],
            UnidadMedida = l.Producto?.UnidadMedida,
            PrecioProveedor = precioPorProducto[l.ProductoId],
            Categoria = l.Producto?.Categoria,
            Excluido = excluido
        }).ToList();
    }

    private static PedidoProveedor CrearDocumentoTransitorio(Solicitud solicitud, Proveedor proveedor, List<DetallePedidoProveedor> detalles) => new()
    {
        SolicitudId = solicitud.Id,
        Solicitud = solicitud,
        Proveedor = proveedor,
        Items = detalles
    };

    /// <summary>
    /// Busca el documento persistido de esta (Solicitud, Proveedor). Si no existe (primer
    /// envío) lo crea con las líneas de este envío; si ya existe (reenvío) anexa SOLO las
    /// líneas nuevas (las ya presentes no se duplican — el índice único
    /// (PedidoProveedorId, ProductoId) lo haría saltar) y guarda. Devuelve el documento
    /// con su Id para que la <see cref="NotificacionProveedor"/> lo referencie.
    /// </summary>
    private async Task<PedidoProveedor> ObtenerOCrearDocumentoAsync(
        int solicitudId, int proveedorId, string? gestorId, string gestorNombre,
        List<DetallePedidoProveedor> detalles, CancellationToken ct)
    {
        var existente = await pedidosProveedor.ObtenerPorSolicitudYProveedorAsync(solicitudId, proveedorId, ct);
        if (existente is null)
        {
            return await pedidosProveedor.CrearAsync(new PedidoProveedor
            {
                SolicitudId = solicitudId,
                ProveedorId = proveedorId,
                FechaCreacion = DateTime.UtcNow,
                GestorId = gestorId,
                GestorNombre = gestorNombre,
                Items = detalles
            }, ct);
        }

        var yaPresentes = existente.Items.Select(i => i.ProductoId).ToHashSet();
        existente.Items = existente.Items.Concat(detalles.Where(d => !yaPresentes.Contains(d.ProductoId))).ToList();
        await pedidosProveedor.ActualizarAsync(existente, ct);
        return existente;
    }

    private static string ArmarCuerpo(Solicitud solicitud, Proveedor proveedor, IEnumerable<DetallePedidoProveedor> lineas, bool incluyeExcel)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Pedido de reabastecimiento #{solicitud.Id}");
        sb.AppendLine($"Para: {proveedor.Nombre}");
        sb.AppendLine($"Solicitado por: {solicitud.SolicitanteNombre}");
        sb.AppendLine($"Fecha de la solicitud: {solicitud.FechaCreacion:dd/MM/yyyy HH:mm}");
        if (!string.IsNullOrWhiteSpace(solicitud.Comentario))
            sb.AppendLine($"Comentario: {solicitud.Comentario}");
        sb.AppendLine();
        sb.AppendLine(incluyeExcel
            ? "Adjunto el detalle en PDF y en Excel con el código que ustedes le dan a cada producto."
            : "Adjunto el detalle en PDF con el código que ustedes le dan a cada producto.");
        sb.AppendLine();
        sb.AppendLine("Productos:");
        foreach (var d in lineas)
            sb.AppendLine($"- {d.ProductoNombre} · su código: {d.CodigoProveedor} · cantidad: {d.Cantidad}");

        return sb.ToString();
    }

    public Task<List<NotificacionProveedor>> ObtenerEnviosAsync(int solicitudId, CancellationToken ct = default)
        => envios.ObtenerPorSolicitudAsync(solicitudId, ct);

    public Task<List<NotificacionProveedor>> BuscarEnviosAsync(FiltroEnviosProveedorDto filtro, CancellationToken ct = default)
        => envios.BuscarAsync(filtro, ct);
}
