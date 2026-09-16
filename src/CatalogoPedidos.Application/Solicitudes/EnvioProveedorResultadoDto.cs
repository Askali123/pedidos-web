namespace CatalogoPedidos.Application.Solicitudes;

/// <summary>Resultado de intentar notificar a un proveedor sobre un pedido, para mostrarle al gestor qué se logró enviar y qué no (y por qué).</summary>
public class EnvioProveedorResultadoDto
{
    public int ProveedorId { get; set; }
    public string ProveedorNombre { get; set; } = string.Empty;
    public int CantidadLineas { get; set; }
    public bool Enviado { get; set; }

    /// <summary>Por qué no se pudo enviar (sin correo registrado, sin proveedor asociado, error al enviar). Null si sí se envió.</summary>
    public string? Motivo { get; set; }
}
