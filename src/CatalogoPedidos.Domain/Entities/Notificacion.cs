using CatalogoPedidos.Domain.Enums;

namespace CatalogoPedidos.Domain.Entities;

public class Notificacion
{
    public int Id { get; set; }

    /// <summary>A quién le llega — el Id del usuario de Identity (gestor o solicitante).</summary>
    public string UsuarioDestinoId { get; set; } = string.Empty;

    public TipoNotificacion Tipo { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;

    /// <summary>Ruta a la que navegar al hacer clic (p.ej. /solicitudes/bandeja).</summary>
    public string? Url { get; set; }

    public bool Leida { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
