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
    Task EnviarAsync(string destinatario, string asunto, string cuerpo, EmailAdjunto? adjunto = null, CancellationToken ct = default);
}
