using System.Text;
using CatalogoPedidos.Application.Notificaciones;
using CatalogoPedidos.Application.Proveedores;
using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Solicitudes;

public class PedidoNotificacionProveedorService(
    ISolicitudRepository solicitudes,
    IProductoProveedorRepository asociaciones,
    INotificacionProveedorRepository envios,
    IEmailSender emailSender) : IPedidoNotificacionProveedorService
{
    public async Task<List<EnvioProveedorResultadoDto>> EnviarAProveedoresAsync(int pedidoId, CancellationToken ct = default)
    {
        var pedido = await solicitudes.ObtenerPedidoAsync(pedidoId, ct)
            ?? throw new InvalidOperationException("El pedido no existe.");

        if (pedido.Items.Count == 0)
            throw new InvalidOperationException("Este pedido no tiene productos.");

        var productoIds = pedido.Items.Select(i => i.ProductoId).Distinct();
        var preferidos = await asociaciones.ObtenerPreferidosPorProductosAsync(productoIds, ct);
        var proveedorPorProducto = preferidos.ToDictionary(pp => pp.ProductoId);

        var resultados = new List<EnvioProveedorResultadoDto>();

        var lineasConProveedor = pedido.Items
            .Where(i => proveedorPorProducto.ContainsKey(i.ProductoId))
            .Select(i => (Item: i, Asociacion: proveedorPorProducto[i.ProductoId]))
            .ToList();

        foreach (var grupo in lineasConProveedor.GroupBy(x => x.Asociacion.ProveedorId))
        {
            var lineas = grupo.ToList();
            var proveedor = lineas[0].Asociacion.Proveedor!;

            if (string.IsNullOrWhiteSpace(proveedor.Email))
            {
                resultados.Add(new EnvioProveedorResultadoDto
                {
                    ProveedorId = proveedor.Id,
                    ProveedorNombre = proveedor.Nombre,
                    CantidadLineas = lineas.Count,
                    Enviado = false,
                    Motivo = "El proveedor no tiene correo registrado."
                });
                continue;
            }

            try
            {
                var cuerpo = ArmarCuerpo(pedido, proveedor, lineas.Select(l => (l.Item, l.Asociacion.CodigoProveedor)));
                var asunto = $"Pedido de reabastecimiento #{pedido.Id} — {proveedor.Nombre}";

                await emailSender.EnviarAsync(proveedor.Email, asunto, cuerpo, ct);

                await envios.CrearAsync(new NotificacionProveedor
                {
                    PedidoId = pedido.Id,
                    ProveedorId = proveedor.Id,
                    Email = proveedor.Email,
                    CantidadLineas = lineas.Count,
                    FechaEnvio = DateTime.UtcNow
                }, ct);

                resultados.Add(new EnvioProveedorResultadoDto
                {
                    ProveedorId = proveedor.Id,
                    ProveedorNombre = proveedor.Nombre,
                    CantidadLineas = lineas.Count,
                    Enviado = true
                });
            }
            catch (Exception ex)
            {
                // El envío es la acción que pidió el gestor (no un efecto secundario best-effort
                // como las notificaciones internas) — si falla, se lo mostramos, no lo tragamos.
                resultados.Add(new EnvioProveedorResultadoDto
                {
                    ProveedorId = proveedor.Id,
                    ProveedorNombre = proveedor.Nombre,
                    CantidadLineas = lineas.Count,
                    Enviado = false,
                    Motivo = $"No se pudo enviar el correo: {ex.Message}"
                });
            }
        }

        var sinProveedor = pedido.Items.Count - lineasConProveedor.Count;
        if (sinProveedor > 0)
        {
            resultados.Add(new EnvioProveedorResultadoDto
            {
                ProveedorId = 0,
                ProveedorNombre = "(sin proveedor asociado)",
                CantidadLineas = sinProveedor,
                Enviado = false,
                Motivo = "Estos productos no tienen ningún proveedor asociado en el catálogo."
            });
        }

        return resultados;
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
        sb.AppendLine("Productos:");
        foreach (var (item, codigoProveedor) in lineas)
            sb.AppendLine($"- {item.Producto?.Nombre} · su código: {codigoProveedor} · cantidad: {item.Cantidad}");

        return sb.ToString();
    }

    public Task<List<NotificacionProveedor>> ObtenerEnviosAsync(int pedidoId, CancellationToken ct = default)
        => envios.ObtenerPorPedidoAsync(pedidoId, ct);
}
