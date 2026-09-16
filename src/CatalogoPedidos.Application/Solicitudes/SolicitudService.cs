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
    IProductoProveedorRepository asociaciones) : ISolicitudService
{
    private const int MensajeMaxLength = 500;

    /// <summary>Tiempo pendiente a partir del cual un pedido se considera urgente (ver también Bandeja.razor).</summary>
    private static readonly TimeSpan UmbralPendienteUrgente = TimeSpan.FromHours(48);

    public async Task<Pedido> CrearPedidoAsync(string solicitanteId, string solicitanteNombre, string? comentario, List<CrearSolicitudDto> items, string? direccionEntrega = null, CancellationToken ct = default)
    {
        if (items.Count == 0)
            throw new InvalidOperationException("Selecciona al menos un producto antes de enviar la solicitud.");

        var pedido = new Pedido
        {
            SolicitanteId = solicitanteId,
            SolicitanteNombre = solicitanteNombre,
            Comentario = comentario,
            DireccionEntrega = string.IsNullOrWhiteSpace(direccionEntrega) ? null : direccionEntrega.Trim(),
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

            pedido.Items.Add(new SolicitudProducto
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

        var creado = await repositorio.CrearPedidoAsync(pedido, ct);

        await NotificarGestoresAsync(solicitanteNombre, ResumirNombres(nombres), ct);

        return creado;
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
                await NotificarAsync(gestorId, "Nuevo pedido", mensaje, "/solicitudes/bandeja", TipoNotificacion.SolicitudCreada, ct);
        }
        catch
        {
            // Best-effort: el pedido ya existe aunque la notificación falle.
        }
    }

    public Task<List<SolicitudProducto>> ObtenerMisSolicitudesAsync(string solicitanteId, CancellationToken ct = default)
        => repositorio.ObtenerPorSolicitanteAsync(solicitanteId, ct);

    public Task<List<SolicitudProducto>> ObtenerBandejaAsync(CancellationToken ct = default)
        => repositorio.ObtenerPendientesAsync(ct);

    public Task<SolicitudProducto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
        => repositorio.ObtenerPorIdAsync(id, ct);

    public Task<Pedido?> ObtenerPedidoAsync(int pedidoId, CancellationToken ct = default)
        => repositorio.ObtenerPedidoAsync(pedidoId, ct);

    public async Task EnviarRecordatoriosPendientesAsync(CancellationToken ct = default)
    {
        var pendientes = await repositorio.ObtenerPendientesAsync(ct);
        var limite = DateTime.UtcNow - UmbralPendienteUrgente;

        var pedidosVencidos = pendientes
            .Where(s => s.Pedido is not null && !s.Pedido.RecordatorioEnviado && s.Pedido.FechaCreacion <= limite)
            .GroupBy(s => s.Pedido!)
            .ToList();

        foreach (var grupo in pedidosVencidos)
        {
            var pedido = grupo.Key;
            await NotificarRecordatorioAsync(pedido, grupo.Count(), ct);

            pedido.RecordatorioEnviado = true;
            await repositorio.ActualizarPedidoAsync(pedido, ct);
        }
    }

    private async Task NotificarRecordatorioAsync(Pedido pedido, int cantidadProductos, CancellationToken ct)
    {
        try
        {
            var horas = (int)UmbralPendienteUrgente.TotalHours;
            var mensaje = $"El pedido #{pedido.Id} de {pedido.SolicitanteNombre} lleva más de {horas}h pendiente ({cantidadProductos} producto{(cantidadProductos == 1 ? "" : "s")}).";
            var idsGestores = await gestores.ObtenerIdsGestoresAsync(ct);
            foreach (var gestorId in idsGestores)
                await NotificarAsync(gestorId, "Pedido pendiente hace tiempo", mensaje, "/solicitudes/bandeja", TipoNotificacion.RecordatorioPendiente, ct);
        }
        catch
        {
            // Best-effort: no bloquea marcar el pedido como recordado aunque falle el aviso.
        }
    }

    public async Task ResolverAsync(int solicitudId, string gestorId, string gestorNombre, ResolverSolicitudDto dto, CancellationToken ct = default)
    {
        var solicitud = await ResolverSinNotificarAsync(solicitudId, gestorId, gestorNombre, dto, ct);
        var aprobada = solicitud.Estado == EstadoSolicitud.Aprobada;

        // Título/mensaje en singular y hablando de la SOLICITUD, no del pedido — un pedido
        // puede tener varias líneas con estados independientes, y acá se resolvió solo esta
        // (ver docs/PLAN_TRAZABILIDAD_ENTREGAS.md #1). "Pedido aprobado/rechazado" queda
        // reservado para cuando de verdad se resuelve el pedido completo (ResolverPedidoAsync).
        var titulo = aprobada ? "Solicitud aprobada" : "Solicitud rechazada";
        var mensaje = $"Tu solicitud de \"{solicitud.Producto?.Nombre}\" fue {(aprobada ? "aprobada" : "rechazada")} por {gestorNombre}.";

        await NotificarResolucionAsync(solicitud.SolicitanteId, titulo, mensaje, ct);
    }

    public async Task<List<SolicitudProducto>> ResolverPedidoAsync(int pedidoId, string gestorId, string gestorNombre, ResolverSolicitudDto dto, CancellationToken ct = default)
    {
        var pedido = await repositorio.ObtenerPedidoAsync(pedidoId, ct)
            ?? throw new InvalidOperationException("El pedido no existe.");

        var pendientes = pedido.Items.Where(i => i.Estado == EstadoSolicitud.Pendiente).ToList();
        if (pendientes.Count == 0)
            throw new InvalidOperationException("Este pedido ya no tiene productos pendientes.");

        var resueltas = new List<SolicitudProducto>();
        var nombres = new List<string>();
        foreach (var item in pendientes)
        {
            var resuelta = await ResolverSinNotificarAsync(item.Id, gestorId, gestorNombre, dto, ct);
            resueltas.Add(resuelta);
            nombres.Add($"{resuelta.Cantidad}x {resuelta.Producto?.Nombre}");
        }

        var resumen = ResumirNombres(nombres);
        var titulo = dto.Aprobar ? "Pedido aprobado" : "Pedido rechazado";
        var mensaje = $"Tu pedido ({resumen}) fue {(dto.Aprobar ? "aprobado" : "rechazado")} por {gestorNombre}.";
        await NotificarResolucionAsync(pedido.SolicitanteId, titulo, mensaje, ct);

        return resueltas;
    }

    private async Task<SolicitudProducto> ResolverSinNotificarAsync(int solicitudId, string gestorId, string gestorNombre, ResolverSolicitudDto dto, CancellationToken ct)
    {
        var solicitud = await repositorio.ObtenerPorIdAsync(solicitudId, ct)
            ?? throw new InvalidOperationException("La solicitud no existe.");

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
            throw new InvalidOperationException("Esta solicitud ya fue resuelta.");

        solicitud.Estado = dto.Aprobar ? EstadoSolicitud.Aprobada : EstadoSolicitud.Rechazada;
        solicitud.GestorId = gestorId;
        solicitud.GestorNombre = gestorNombre;
        solicitud.ComentarioGestor = dto.ComentarioGestor;
        solicitud.FechaResolucion = DateTime.UtcNow;

        await repositorio.ActualizarAsync(solicitud, ct);

        if (dto.Aprobar)
            await DescontarStockAsync(solicitud.ProductoId, solicitud.Cantidad, ct);

        return solicitud;
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
            // "Administrar pedidos" filtrado por ese producto Y ese proveedor — al gestor le
            // queda a un clic el botón "Enviar a proveedor" ya existente (ver tarea 15 del
            // plan). Sin proveedor asociado, igual filtra por producto para ubicar rápido
            // qué pedidos lo tienen pendiente/aprobado.
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

    private async Task NotificarResolucionAsync(string solicitanteId, string titulo, string mensaje, CancellationToken ct)
    {
        try
        {
            await NotificarAsync(solicitanteId, titulo, mensaje, "/solicitudes/mis-solicitudes", TipoNotificacion.SolicitudResuelta, ct);
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

    public Task<List<SolicitudProducto>> BuscarAsync(FiltroSolicitudesDto filtro, CancellationToken ct = default)
        => repositorio.BuscarAsync(filtro, ct);

    public Task<List<SolicitanteResumenDto>> ObtenerSolicitantesAsync(CancellationToken ct = default)
        => repositorio.ObtenerSolicitantesAsync(ct);
}
