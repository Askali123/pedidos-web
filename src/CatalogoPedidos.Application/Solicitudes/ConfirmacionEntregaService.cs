using CatalogoPedidos.Application.Notificaciones;
using CatalogoPedidos.Application.Productos;
using CatalogoPedidos.Application.Proveedores;
using CatalogoPedidos.Application.Usuarios;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Domain.Enums;

namespace CatalogoPedidos.Application.Solicitudes;

public class ConfirmacionEntregaService(
    ISolicitudRepository solicitudes,
    IConfirmacionEntregaRepository confirmaciones,
    IProductoProveedorRepository asociaciones,
    INotificacionProveedorRepository envios,
    IProductoRepository productos,
    INotificacionRepository notificaciones,
    INotificacionBroadcaster broadcaster,
    IGestorDirectory gestores) : IConfirmacionEntregaService
{
    private const int MensajeMaxLength = 500;

    /// <summary>Tiempo aprobado sin confirmar entrega a partir del cual se manda el recordatorio.</summary>
    private static readonly TimeSpan UmbralEntregaPendiente = TimeSpan.FromHours(72);

    public Task<List<ConfirmacionEntrega>> ObtenerPorPedidoAsync(int pedidoId, CancellationToken ct = default)
        => confirmaciones.ObtenerPorPedidoAsync(pedidoId, ct);

    public async Task<List<GrupoEntregaDto>> ObtenerGruposDeEntregaAsync(int pedidoId, CancellationToken ct = default)
    {
        var pedido = await solicitudes.ObtenerPedidoAsync(pedidoId, ct)
            ?? throw new InvalidOperationException("El pedido no existe.");

        var confirmadas = await confirmaciones.ObtenerPorPedidoAsync(pedidoId, ct);
        return await ObtenerGruposDeEntregaAsync(pedido, confirmadas, ct);
    }

    /// <summary>
    /// Divide las líneas Aprobadas del pedido en grupos de entrega: uno por cada
    /// proveedor al que REALMENTE se le envió algo (hay un
    /// <see cref="NotificacionProveedor"/> que lo cubre), más un grupo "sin proveedor"
    /// (<c>ProveedorId = null</c>) con lo que quede sin cubrir — ya sea porque el
    /// producto no tiene ningún proveedor asociado, o porque tiene pero todavía no se le
    /// mandó nada a nadie (compra local/manual, o un envío que sigue pendiente). Mismo
    /// criterio de "cobertura" que ya usa <see cref="PedidoNotificacionProveedorService"/>
    /// para evitar mandarle la misma línea a dos proveedores (ver
    /// docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md, tareas 2 y 4).
    /// </summary>
    private async Task<List<GrupoEntregaDto>> ObtenerGruposDeEntregaAsync(Pedido pedido, List<ConfirmacionEntrega> confirmadas, CancellationToken ct)
    {
        var aprobadas = pedido.Items.Where(i => i.Estado == EstadoSolicitud.Aprobada).ToList();
        if (aprobadas.Count == 0)
            return [];

        var productoIds = aprobadas.Select(i => i.ProductoId).Distinct();
        var todasLasAsociaciones = await asociaciones.ObtenerPorProductosAsync(productoIds, ct);
        var enviosDelPedido = await envios.ObtenerPorPedidoAsync(pedido.Id, ct);
        var proveedorIdsConfirmados = confirmadas.Select(c => c.ProveedorId).ToHashSet();

        var grupos = new List<GrupoEntregaDto>();
        var productoIdsCubiertos = new HashSet<int>();

        foreach (var enviosDeUnProveedor in enviosDelPedido.GroupBy(e => e.ProveedorId))
        {
            var productoIdsDeEsteProveedor = todasLasAsociaciones
                .Where(a => a.ProveedorId == enviosDeUnProveedor.Key)
                .Select(a => a.ProductoId)
                .ToHashSet();

            var lineas = aprobadas.Where(i => productoIdsDeEsteProveedor.Contains(i.ProductoId)).ToList();
            if (lineas.Count == 0)
                continue;

            grupos.Add(new GrupoEntregaDto
            {
                ProveedorId = enviosDeUnProveedor.Key,
                ProveedorNombre = enviosDeUnProveedor.First().Proveedor?.Nombre,
                CantidadLineas = lineas.Count,
                Confirmado = proveedorIdsConfirmados.Contains(enviosDeUnProveedor.Key),
                Productos = AProductosDelPedido(lineas)
            });
            foreach (var linea in lineas)
                productoIdsCubiertos.Add(linea.ProductoId);
        }

        var sinCubrir = aprobadas.Where(i => !productoIdsCubiertos.Contains(i.ProductoId)).ToList();
        if (sinCubrir.Count > 0)
        {
            grupos.Add(new GrupoEntregaDto
            {
                ProveedorId = null,
                ProveedorNombre = null,
                CantidadLineas = sinCubrir.Count,
                Confirmado = proveedorIdsConfirmados.Contains(null),
                Productos = AProductosDelPedido(sinCubrir)
            });
        }

        // Los grupos con proveedor primero (alfabético); "sin proveedor" al final.
        return grupos.OrderBy(g => g.ProveedorId is null).ThenBy(g => g.ProveedorNombre).ToList();
    }

    private static List<ProductoDelPedidoDto> AProductosDelPedido(IEnumerable<SolicitudProducto> lineas) =>
        lineas.Select(i => new ProductoDelPedidoDto
        {
            ProductoId = i.ProductoId,
            Nombre = i.Producto?.Nombre ?? $"Producto #{i.ProductoId}",
            Cantidad = i.Cantidad
        }).ToList();

    public async Task<EntregaProgresoDto> ObtenerProgresoAsync(int pedidoId, CancellationToken ct = default)
    {
        var grupos = await ObtenerGruposDeEntregaAsync(pedidoId, ct);
        return new EntregaProgresoDto
        {
            TotalGrupos = grupos.Count,
            GruposConfirmados = grupos.Count(g => g.Confirmado)
        };
    }

    public async Task<ConfirmacionEntrega> ConfirmarAsync(int pedidoId, int? proveedorId, string gestorId, string gestorNombre, ConfirmarEntregaDto dto, CancellationToken ct = default)
    {
        var pedido = await solicitudes.ObtenerPedidoAsync(pedidoId, ct)
            ?? throw new InvalidOperationException("El pedido no existe.");

        var confirmadas = await confirmaciones.ObtenerPorPedidoAsync(pedidoId, ct);

        // La UI ya oculta el botón para un grupo que no existe o que ya está confirmado
        // (ver Bandeja/Administrar), pero el servicio es la última línea de defensa.
        var grupos = await ObtenerGruposDeEntregaAsync(pedido, confirmadas, ct);
        var grupo = grupos.FirstOrDefault(g => g.ProveedorId == proveedorId)
            ?? throw new InvalidOperationException("Este pedido no tiene líneas aprobadas pendientes de entrega para ese proveedor.");

        if (grupo.Confirmado)
            throw new InvalidOperationException("La entrega de este proveedor para este pedido ya fue confirmada.");

        var confirmacion = new ConfirmacionEntrega
        {
            PedidoId = pedido.Id,
            ProveedorId = proveedorId,
            FechaEntrega = DateTime.UtcNow,
            ConfirmadoPorId = gestorId,
            ConfirmadoPorNombre = gestorNombre,
            DireccionEntregada = string.IsNullOrWhiteSpace(dto.DireccionEntregada) ? null : dto.DireccionEntregada.Trim(),
            CoincideConDireccionIndicada = dto.CoincideConDireccionIndicada,
            RecibidoPorNombre = string.IsNullOrWhiteSpace(dto.RecibidoPorNombre) ? null : dto.RecibidoPorNombre.Trim(),
            Observaciones = string.IsNullOrWhiteSpace(dto.Observaciones) ? null : dto.Observaciones.Trim()
        };

        var creada = await confirmaciones.CrearAsync(confirmacion, ct);
        await ReponerStockAsync(grupo.Productos, ct);
        await NotificarEntregaAsync(pedido, creada, grupo, grupos, ct);

        return creada;
    }

    /// <summary>
    /// Repone el stock de cada producto del grupo recién confirmado — contraparte de
    /// <see cref="SolicitudService.DescontarStockAsync"/>, que resta al aprobar pero nunca
    /// sumaba de vuelta al confirmarse la entrega (ver docs/PLAN_CALIDAD_INGENIERIA.md,
    /// tarea 1 — causa raíz del stock negativo crónico en el catálogo). Mismo patrón no
    /// transaccional que ese método: si un producto ya no existe, se lo salta en vez de
    /// fallar toda la confirmación.
    /// </summary>
    private async Task ReponerStockAsync(List<ProductoDelPedidoDto> productosDelGrupo, CancellationToken ct)
    {
        foreach (var item in productosDelGrupo)
        {
            var producto = await productos.ObtenerPorIdAsync(item.ProductoId, ct);
            if (producto is null)
                continue;

            producto.Stock += item.Cantidad;
            await productos.ActualizarAsync(producto, ct);
        }
    }

    private async Task NotificarEntregaAsync(Pedido pedido, ConfirmacionEntrega confirmacion, GrupoEntregaDto grupo, List<GrupoEntregaDto> grupos, CancellationToken ct)
    {
        try
        {
            // grupos todavía no incluye esta confirmación recién creada (se calculó antes
            // de guardarla), así que "quedan pendientes" mira a los DEMÁS grupos tal cual
            // estaban — ver docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md #8.
            var quedanPendientes = grupos.Any(g => g.ProveedorId != grupo.ProveedorId && !g.Confirmado);

            string mensaje;
            if (grupos.Count <= 1)
            {
                mensaje = $"Tu pedido #{pedido.Id} fue entregado el {confirmacion.FechaEntrega:dd/MM/yyyy HH:mm}";
            }
            else if (quedanPendientes)
            {
                var deQuien = grupo.ProveedorNombre is not null ? $"correspondiente a {grupo.ProveedorNombre}" : "sin proveedor asociado";
                mensaje = $"Ya te entregaron la parte de tu pedido #{pedido.Id} {deQuien}, el {confirmacion.FechaEntrega:dd/MM/yyyy HH:mm}. Todavía falta el resto";
            }
            else
            {
                mensaje = $"Tu pedido #{pedido.Id} ya fue entregado por completo — lo último llegó el {confirmacion.FechaEntrega:dd/MM/yyyy HH:mm}";
            }

            mensaje += string.IsNullOrWhiteSpace(confirmacion.DireccionEntregada)
                ? "."
                : $" en \"{confirmacion.DireccionEntregada}\".";

            if (!confirmacion.CoincideConDireccionIndicada)
                mensaje += " La dirección de entrega no coincidió con la que indicaste.";

            var url = $"/solicitudes/mis-solicitudes#pedido-{pedido.Id}";
            await NotificarAsync(pedido.SolicitanteId, "Entrega confirmada", mensaje, url, TipoNotificacion.EntregaConfirmada, ct);
        }
        catch
        {
            // Best-effort: la entrega ya quedó confirmada aunque la notificación falle.
        }
    }

    public async Task EnviarRecordatoriosEntregaPendienteAsync(CancellationToken ct = default)
    {
        var candidatos = await solicitudes.ObtenerAprobadosSinEntregaAsync(ct);
        var limite = DateTime.UtcNow - UmbralEntregaPendiente;

        foreach (var pedido in candidatos)
        {
            // El reloj arranca desde la ÚLTIMA aprobación del pedido, no desde que se
            // creó — un pedido puede quedar con líneas pendientes mucho tiempo antes de
            // que se apruebe lo que sí hay que entregar (ver #19 del plan).
            var ultimaAprobacion = pedido.Items
                .Where(i => i.Estado == EstadoSolicitud.Aprobada)
                .Max(i => i.FechaResolucion);

            if (ultimaAprobacion is null || ultimaAprobacion > limite)
                continue;

            // Con confirmación por proveedor, "sin entrega" ya no alcanza con mirar
            // Pedido.ConfirmacionesEntrega — hay que ver si queda algún GRUPO sin
            // confirmar (un pedido con una confirmación parcial igual puede tener algo
            // pendiente) — ver docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md #8.
            var grupos = await ObtenerGruposDeEntregaAsync(pedido.Id, ct);
            var gruposPendientes = grupos.Where(g => !g.Confirmado).ToList();
            if (gruposPendientes.Count == 0)
                continue;

            await NotificarRecordatorioEntregaAsync(pedido, gruposPendientes, ct);

            pedido.RecordatorioEntregaEnviado = true;
            await solicitudes.ActualizarPedidoAsync(pedido, ct);
        }
    }

    private async Task NotificarRecordatorioEntregaAsync(Pedido pedido, List<GrupoEntregaDto> gruposPendientes, CancellationToken ct)
    {
        try
        {
            var horas = (int)UmbralEntregaPendiente.TotalHours;
            var pendientes = gruposPendientes.Sum(g => g.CantidadLineas);
            var mensaje = $"El pedido #{pedido.Id} de {pedido.SolicitanteNombre} tiene {pendientes} producto{(pendientes == 1 ? "" : "s")} aprobado{(pendientes == 1 ? "" : "s")} hace más de {horas}h sin confirmar la entrega.";

            var idsGestores = await gestores.ObtenerIdsGestoresAsync(ct);
            foreach (var gestorId in idsGestores)
                await NotificarAsync(gestorId, "Entrega pendiente de confirmar", mensaje, "/solicitudes/administrar", TipoNotificacion.RecordatorioEntregaPendiente, ct);
        }
        catch
        {
            // Best-effort: no bloquea marcar el pedido como recordado aunque falle el aviso.
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
}
