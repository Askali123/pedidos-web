using CatalogoPedidos.Application.Notificaciones;
using CatalogoPedidos.Application.Productos;
using CatalogoPedidos.Application.Proveedores;
using CatalogoPedidos.Application.Usuarios;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Domain.Enums;

namespace CatalogoPedidos.Application.Solicitudes;

public class SolicitudService(
    ISolicitudRepository repositorio,
    IProductoRepository productos,
    INotificacionRepository notificaciones,
    INotificacionBroadcaster broadcaster,
    IGestorDirectory gestores,
    IProductoProveedorRepository asociaciones,
    IUsuarioSedeDirectory usuarioSedes) : ISolicitudService
{
    private const int MensajeMaxLength = 500;

    /// <summary>Tiempo pendiente a partir del cual una solicitud se considera urgente (ver también Bandeja.razor).</summary>
    private static readonly TimeSpan UmbralPendienteUrgente = TimeSpan.FromHours(48);

    public async Task<Solicitud> CrearSolicitudAsync(string solicitanteId, string solicitanteNombre, string? comentario, List<CrearSolicitudDto> items, string? direccionEntrega = null, CancellationToken ct = default)
    {
        if (items.Count == 0)
            throw new InvalidOperationException("Selecciona al menos un producto antes de enviar la solicitud.");

        // Snapshot de Sede/Empresa del solicitante — no es editable por el usuario (a
        // diferencia de DireccionEntrega), se resuelve siempre desde su asociación real en
        // ese momento (ver docs/PLAN_EMPRESAS_FILIALES.md). Null si todavía no tiene sede
        // asignada — no bloquea la creación de la solicitud.
        var sede = await usuarioSedes.ObtenerSedeAsync(solicitanteId, ct);

        var solicitud = new Solicitud
        {
            SolicitanteId = solicitanteId,
            SolicitanteNombre = solicitanteNombre,
            Comentario = comentario,
            DireccionEntrega = string.IsNullOrWhiteSpace(direccionEntrega) ? null : direccionEntrega.Trim(),
            SedeId = sede?.SedeId,
            SedeNombre = sede?.SedeNombre,
            EmpresaId = sede?.EmpresaId,
            EmpresaNombre = sede?.EmpresaNombre,
            FechaCreacion = DateTime.UtcNow
        };

        var nombres = new List<string>();
        foreach (var item in items)
        {
            var producto = await productos.ObtenerPorIdAsync(item.ProductoId, ct)
                ?? throw new InvalidOperationException("El producto seleccionado no existe.");

            // El catálogo ya oculta los productos inactivos, pero si uno quedaba en el
            // carrito de una sesión anterior y se desactivó mientras tanto, no debe poder
            // pedirse igual solo porque el ProductoId todavía es válido.
            if (!producto.Activo)
                throw new InvalidOperationException($"\"{producto.Nombre}\" ya no está disponible en el catálogo.");

            if (item.Cantidad <= 0)
                throw new InvalidOperationException($"La cantidad de \"{producto.Nombre}\" debe ser mayor a cero.");

            solicitud.Items.Add(new DetalleSolicitud
            {
                ProductoId = producto.Id,
                Cantidad = item.Cantidad,
                SolicitanteId = solicitanteId,
                SolicitanteNombre = solicitanteNombre,
                Estado = EstadoSolicitud.Pendiente,
                FechaSolicitud = DateTime.UtcNow
            });

            nombres.Add($"{item.Cantidad}x {producto.Nombre}");
        }

        var creada = await repositorio.CrearSolicitudAsync(solicitud, ct);

        await NotificarGestoresAsync(solicitanteNombre, ResumirNombres(nombres), ct);

        return creada;
    }

    /// <summary>
    /// Arma un resumen legible que quepa en el límite de 500 caracteres de
    /// Notificacion.Mensaje (ver AppDbContext): lista unos pocos productos y
    /// resume el resto como "y N más" en vez de listar todos sin límite.
    /// </summary>
    private static string ResumirNombres(List<string> nombres)
    {
        if (nombres.Count == 1)
            return nombres[0];

        const int maxListados = 5;
        var listados = nombres.Take(maxListados).ToList();
        var restantes = nombres.Count - listados.Count;

        var detalle = string.Join(", ", listados);
        if (restantes > 0)
            detalle += $" y {restantes} más";

        return $"{nombres.Count} productos: {detalle}";
    }

    private async Task NotificarGestoresAsync(string solicitanteNombre, string resumen, CancellationToken ct)
    {
        var mensaje = $"{solicitanteNombre} solicitó {resumen}.";

        try
        {
            var idsGestores = await gestores.ObtenerIdsGestoresAsync(ct);
            foreach (var gestorId in idsGestores)
                await NotificarAsync(gestorId, "Nueva solicitud", mensaje, "/solicitudes/bandeja", TipoNotificacion.SolicitudCreada, ct);
        }
        catch
        {
            // Best-effort: la solicitud ya existe aunque la notificación falle.
        }
    }

    public Task<List<DetalleSolicitud>> ObtenerMisSolicitudesAsync(string solicitanteId, CancellationToken ct = default)
        => repositorio.ObtenerPorSolicitanteAsync(solicitanteId, ct);

    public Task<List<DetalleSolicitud>> ObtenerBandejaAsync(CancellationToken ct = default)
        => repositorio.ObtenerPendientesAsync(ct);

    public Task<DetalleSolicitud?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
        => repositorio.ObtenerPorIdAsync(id, ct);

    public Task<Solicitud?> ObtenerSolicitudAsync(int solicitudId, CancellationToken ct = default)
        => repositorio.ObtenerSolicitudAsync(solicitudId, ct);

    public async Task EnviarRecordatoriosPendientesAsync(CancellationToken ct = default)
    {
        var pendientes = await repositorio.ObtenerPendientesAsync(ct);
        var limite = DateTime.UtcNow - UmbralPendienteUrgente;

        var solicitudesVencidas = pendientes
            .Where(s => s.Solicitud is not null && !s.Solicitud.RecordatorioEnviado && s.Solicitud.FechaCreacion <= limite)
            .GroupBy(s => s.Solicitud!)
            .ToList();

        foreach (var grupo in solicitudesVencidas)
        {
            var solicitud = grupo.Key;
            await NotificarRecordatorioAsync(solicitud, grupo.Count(), ct);

            solicitud.RecordatorioEnviado = true;
            await repositorio.ActualizarSolicitudAsync(solicitud, ct);
        }
    }

    private async Task NotificarRecordatorioAsync(Solicitud solicitud, int cantidadProductos, CancellationToken ct)
    {
        try
        {
            var horas = (int)UmbralPendienteUrgente.TotalHours;
            var mensaje = $"La solicitud #{solicitud.Id} de {solicitud.SolicitanteNombre} lleva más de {horas}h pendiente ({cantidadProductos} producto{(cantidadProductos == 1 ? "" : "s")}).";
            var idsGestores = await gestores.ObtenerIdsGestoresAsync(ct);
            foreach (var gestorId in idsGestores)
                await NotificarAsync(gestorId, "Solicitud pendiente hace tiempo", mensaje, "/solicitudes/bandeja", TipoNotificacion.RecordatorioPendiente, ct);
        }
        catch
        {
            // Best-effort: no bloquea marcar la solicitud como recordada aunque falle el aviso.
        }
    }

    public async Task ResolverAsync(int detalleId, string gestorId, string gestorNombre, ResolverSolicitudDto dto, CancellationToken ct = default)
    {
        var detalle = await ResolverSinNotificarAsync(detalleId, gestorId, gestorNombre, dto, ct);
        var aprobada = detalle.Estado == EstadoSolicitud.Aprobada;

        // Título/mensaje en singular y hablando de la LÍNEA, no de la solicitud completa —
        // una solicitud puede tener varias líneas con estados independientes, y acá se
        // resolvió solo esta (ver docs/PLAN_TRAZABILIDAD_ENTREGAS.md #1). "Solicitud
        // aprobada/rechazada" en plural queda reservado para cuando de verdad se resuelve
        // la solicitud completa (ResolverSolicitudAsync).
        var titulo = aprobada ? "Solicitud aprobada" : "Solicitud rechazada";
        var mensaje = $"Tu solicitud de \"{detalle.Producto?.Nombre}\" fue {(aprobada ? "aprobada" : "rechazada")} por {gestorNombre}.";

        await NotificarResolucionAsync(detalle.SolicitanteId, detalle.SolicitudId, titulo, mensaje, ct);
    }

    public async Task<List<DetalleSolicitud>> ResolverSolicitudAsync(int solicitudId, string gestorId, string gestorNombre, ResolverSolicitudDto dto, CancellationToken ct = default)
    {
        var solicitud = await repositorio.ObtenerSolicitudAsync(solicitudId, ct)
            ?? throw new InvalidOperationException("La solicitud no existe.");

        var pendientes = solicitud.Items.Where(i => i.Estado == EstadoSolicitud.Pendiente).ToList();
        if (pendientes.Count == 0)
            throw new InvalidOperationException("Esta solicitud ya no tiene productos pendientes.");

        // "Aprobar/rechazar todo" es el caso particular de ResolverVariasAsync donde
        // todas las líneas comparten la misma decisión — se delega ahí para que ambos
        // caminos manden la misma notificación resumen (ver #10 del plan).
        var decisiones = pendientes
            .Select(i => new DecisionSolicitudDto { DetalleId = i.Id, Aprobar = dto.Aprobar })
            .ToList();
        var dtoVarias = new ResolverVariasDto { Decisiones = decisiones, ComentarioGestor = dto.ComentarioGestor };

        return await ResolverVariasAsync(solicitudId, gestorId, gestorNombre, dtoVarias, ct);
    }

    public async Task<List<DetalleSolicitud>> ResolverVariasAsync(int solicitudId, string gestorId, string gestorNombre, ResolverVariasDto dto, CancellationToken ct = default)
    {
        if (dto.Decisiones.Count == 0)
            throw new InvalidOperationException("Selecciona al menos una línea para resolver.");

        var solicitud = await repositorio.ObtenerSolicitudAsync(solicitudId, ct)
            ?? throw new InvalidOperationException("La solicitud no existe.");

        var resueltas = new List<DetalleSolicitud>();
        var aprobadas = new List<string>();
        var rechazadas = new List<string>();
        foreach (var decision in dto.Decisiones)
        {
            var itemDto = new ResolverSolicitudDto { Aprobar = decision.Aprobar, ComentarioGestor = dto.ComentarioGestor };
            var resuelta = await ResolverSinNotificarAsync(decision.DetalleId, gestorId, gestorNombre, itemDto, ct);
            resueltas.Add(resuelta);
            (decision.Aprobar ? aprobadas : rechazadas).Add($"{resuelta.Cantidad}x {resuelta.Producto?.Nombre}");
        }

        var (titulo, mensaje) = ArmarResolucionSolicitud(gestorNombre, aprobadas, rechazadas);
        await NotificarResolucionAsync(solicitud.SolicitanteId, solicitud.Id, titulo, mensaje, ct);

        return resueltas;
    }

    /// <summary>
    /// Arma el título/mensaje de la notificación agrupada que recibe el solicitante
    /// cuando se resuelven varias líneas de una solicitud de un tirón, sea con la misma
    /// decisión ("aprobar/rechazar todo") o con una mezcla de aprobaciones y rechazos.
    /// </summary>
    private static (string Titulo, string Mensaje) ArmarResolucionSolicitud(string gestorNombre, List<string> aprobadas, List<string> rechazadas)
    {
        if (rechazadas.Count == 0)
            return ("Solicitud aprobada", $"Tu solicitud ({ResumirNombres(aprobadas)}) fue aprobada por {gestorNombre}.");

        if (aprobadas.Count == 0)
            return ("Solicitud rechazada", $"Tu solicitud ({ResumirNombres(rechazadas)}) fue rechazada por {gestorNombre}.");

        var mensaje = $"Tu solicitud fue resuelta por {gestorNombre}: {aprobadas.Count} aprobado(s) ({ResumirNombres(aprobadas)}), {rechazadas.Count} rechazado(s) ({ResumirNombres(rechazadas)}).";
        return ("Solicitud resuelta", mensaje);
    }

    private async Task<DetalleSolicitud> ResolverSinNotificarAsync(int detalleId, string gestorId, string gestorNombre, ResolverSolicitudDto dto, CancellationToken ct)
    {
        var detalle = await repositorio.ObtenerPorIdAsync(detalleId, ct)
            ?? throw new InvalidOperationException("La solicitud no existe.");

        if (detalle.Estado != EstadoSolicitud.Pendiente)
            throw new InvalidOperationException("Esta solicitud ya fue resuelta.");

        detalle.Estado = dto.Aprobar ? EstadoSolicitud.Aprobada : EstadoSolicitud.Rechazada;
        detalle.GestorId = gestorId;
        detalle.GestorNombre = gestorNombre;
        detalle.ComentarioGestor = dto.ComentarioGestor;
        detalle.FechaResolucion = DateTime.UtcNow;

        await repositorio.ActualizarAsync(detalle, ct);

        if (dto.Aprobar)
            await DescontarStockAsync(detalle.ProductoId, detalle.Cantidad, ct);

        return detalle;
    }

    /// <summary>
    /// Descuenta la cantidad aprobada del stock del producto. No bloquea la aprobación
    /// si no alcanza: el stock puede quedar en negativo como señal de que hay que reponer
    /// (decisión de negocio — ver docs/MEJORAS_PROPUESTAS.md).
    /// </summary>
    private async Task DescontarStockAsync(int productoId, int cantidad, CancellationToken ct)
    {
        var producto = await productos.ObtenerPorIdAsync(productoId, ct);
        if (producto is null)
            return;

        var stockAnterior = producto.Stock;
        producto.Stock -= cantidad;
        await productos.ActualizarAsync(producto, ct);

        // Alerta solo en la TRANSICIÓN hacia stock bajo, no en cada aprobación posterior
        // mientras siga bajo — evita spamear al gestor con la misma alerta una y otra vez.
        if (producto.StockMinimo is int minimo && stockAnterior > minimo && producto.Stock <= minimo)
            await AlertarStockBajoAsync(producto, ct);
    }

    private async Task AlertarStockBajoAsync(Producto producto, CancellationToken ct)
    {
        try
        {
            var mensaje = $"\"{producto.Nombre}\" quedó con stock {producto.Stock} (mínimo configurado: {producto.StockMinimo}).";

            // Si el producto ya tiene un proveedor preferido, la alerta lleva directo a
            // "Administrar solicitudes" filtrado por ese producto Y ese proveedor — al
            // gestor le queda a un clic el botón "Enviar a proveedor" ya existente (ver
            // tarea 15 del plan). Sin proveedor asociado, igual filtra por producto para
            // ubicar rápido qué solicitudes lo tienen pendiente/aprobado.
            var preferidos = await asociaciones.ObtenerPreferidosPorProductosAsync([producto.Id], ct);
            var url = preferidos.Count > 0
                ? $"/solicitudes/administrar?productoId={producto.Id}&proveedorId={preferidos[0].ProveedorId}"
                : $"/solicitudes/administrar?productoId={producto.Id}";

            var idsGestores = await gestores.ObtenerIdsGestoresAsync(ct);
            foreach (var gestorId in idsGestores)
                await NotificarAsync(gestorId, "Stock bajo", mensaje, url, TipoNotificacion.StockBajo, ct);
        }
        catch
        {
            // Best-effort: el descuento de stock ya quedó guardado aunque la notificación falle.
        }
    }

    private async Task NotificarResolucionAsync(string solicitanteId, int solicitudId, string titulo, string mensaje, CancellationToken ct)
    {
        try
        {
            // Ancla a la solicitud específica (Mis solicitudes lista todas sin paginar ni
            // filtrar — ver MisSolicitudes.razor) en vez de mandar siempre a la lista
            // genérica, mismo criterio de deep link que ya usa la alerta de stock bajo
            // (ver AlertarStockBajoAsync).
            var url = $"/solicitudes/mis-solicitudes#pedido-{solicitudId}";
            await NotificarAsync(solicitanteId, titulo, mensaje, url, TipoNotificacion.SolicitudResuelta, ct);
        }
        catch
        {
            // Best-effort: la resolución ya quedó guardada aunque la notificación falle.
        }
    }

    private async Task NotificarAsync(string usuarioDestinoId, string titulo, string mensaje, string url, TipoNotificacion tipo, CancellationToken ct)
    {
        if (mensaje.Length > MensajeMaxLength)
            mensaje = string.Concat(mensaje.AsSpan(0, MensajeMaxLength - 1), "…");

        var notificacion = new Notificacion
        {
            UsuarioDestinoId = usuarioDestinoId,
            Tipo = tipo,
            Titulo = titulo,
            Mensaje = mensaje,
            Url = url
        };

        var guardada = await notificaciones.CrearAsync(notificacion, ct);
        broadcaster.Publicar(guardada);
    }

    public Task<List<DetalleSolicitud>> BuscarAsync(FiltroSolicitudesDto filtro, CancellationToken ct = default)
        => repositorio.BuscarAsync(filtro, ct);

    public Task<List<SolicitanteResumenDto>> ObtenerSolicitantesAsync(CancellationToken ct = default)
        => repositorio.ObtenerSolicitantesAsync(ct);

    public Task<List<DetalleSolicitud>> BuscarResueltasAsync(FiltroHistorialResolucionesDto filtro, CancellationToken ct = default)
        => repositorio.BuscarResueltasAsync(filtro, ct);

    public Task<List<GestorResumenDto>> ObtenerGestoresAsync(CancellationToken ct = default)
        => repositorio.ObtenerGestoresAsync(ct);
}
