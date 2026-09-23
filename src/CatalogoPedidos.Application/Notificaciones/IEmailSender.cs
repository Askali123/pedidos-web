namespace CatalogoPedidos.Application.Notificaciones;

/// <summary>Adjunto de un correo saliente (p. ej. el PDF del pedido a un proveedor).</summary>
public record EmailAdjunto(string NombreArchivo, byte[] Contenido, string ContentType);

/// <summary>
/// Envío de correo saliente (a proveedores, por ahora). Abstracto a propósito: según cómo
/// esté configurado <c>Smtp:Host</c>, Infrastructure registra o bien
/// <c>SmtpEmailSender</c> (envío real) o <c>LoggingEmailSender</c> (solo deja constancia en
/// el log, para cuando todavía no hay credenciales SMTP). Nada en Application ni en la UI
/// depende de cuál esté activo.
/// </summary>
public interface IEmailSender
{
    /// <param name="adjuntos">Cero o más adjuntos (p. ej. el PDF y, opcionalmente, un Excel del mismo pedido).</param>
    /// <param name="copiaA">
    /// Opcional — típicamente el correo de quien dispara el envío (el gestor). Se usa para
    /// dos cosas a la vez: va en copia (CC, visible para el destinatario) y como Reply-To,
    /// para que si el destinatario responde, la respuesta le llegue a esta persona y no al
    /// remitente genérico configurado en <c>Smtp:RemitenteEmail</c>.
    /// </param>
    Task EnviarAsync(string destinatario, string asunto, string cuerpo, IReadOnlyList<EmailAdjunto>? adjuntos = null, string? copiaA = null, CancellationToken ct = default);
}
