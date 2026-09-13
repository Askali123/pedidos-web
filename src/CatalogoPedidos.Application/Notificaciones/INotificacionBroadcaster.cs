using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Notificaciones;

/// <summary>
/// Bus en memoria para empujar notificaciones a los componentes Blazor conectados
/// en tiempo real, sin necesidad de refrescar la página. Vive como singleton.
/// </summary>
public interface INotificacionBroadcaster
{
    void Publicar(Notificacion notificacion);
    event Action<Notificacion>? Recibida;
}
