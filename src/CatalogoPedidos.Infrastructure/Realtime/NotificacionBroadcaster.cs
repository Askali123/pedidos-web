using CatalogoPedidos.Application.Notificaciones;
using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Infrastructure.Realtime;

/// <summary>
/// Bus en memoria (singleton) para empujar notificaciones en tiempo real a los
/// componentes Blazor conectados. Cada componente se suscribe a "Recibida" y
/// filtra por su propio usuario. Suficiente para un solo servidor; en un
/// despliegue con varias instancias habría que cambiarlo por algo distribuido
/// (Redis pub/sub, Azure SignalR, etc.).
/// </summary>
public class NotificacionBroadcaster : INotificacionBroadcaster
{
    public event Action<Notificacion>? Recibida;

    public void Publicar(Notificacion notificacion) => Recibida?.Invoke(notificacion);
}
