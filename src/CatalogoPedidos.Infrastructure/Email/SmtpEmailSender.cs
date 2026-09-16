using System.Net;
using System.Net.Mail;
using CatalogoPedidos.Application.Notificaciones;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CatalogoPedidos.Infrastructure.Email;

/// <summary>
/// Envío real por SMTP. Se registra en <c>DependencyInjection</c> solo cuando
/// <c>Smtp:Host</c> está configurado (ver <see cref="SmtpOptions"/>) — si no, la app sigue
/// usando <see cref="LoggingEmailSender"/> para no romper el flujo de "enviar a proveedor"
/// mientras nadie haya configurado credenciales todavía.
/// </summary>
public class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task EnviarAsync(string destinatario, string asunto, string cuerpo, EmailAdjunto? adjunto = null, CancellationToken ct = default)
    {
        var smtp = options.Value;

        using var mensaje = new MailMessage
        {
            From = new MailAddress(smtp.RemitenteEmail, smtp.RemitenteNombre),
            Subject = asunto,
            Body = cuerpo,
            IsBodyHtml = false
        };
        mensaje.To.Add(destinatario);

        // El MemoryStream lo cierra el propio Attachment al disponerse (y ese, al disponerse
        // mensaje.Attachments más abajo con el "using var mensaje").
        if (adjunto is not null)
            mensaje.Attachments.Add(new Attachment(new MemoryStream(adjunto.Contenido), adjunto.NombreArchivo, adjunto.ContentType));

        using var cliente = new SmtpClient(smtp.Host, smtp.Port)
        {
            Credentials = new NetworkCredential(smtp.Usuario, smtp.Password),
            EnableSsl = smtp.EnableSsl
        };

        try
        {
            await cliente.SendMailAsync(mensaje, ct);
            logger.LogInformation("Correo enviado a {Destinatario} — {Asunto}", destinatario, asunto);
        }
        catch (Exception ex)
        {
            // A diferencia de las notificaciones internas (best-effort), este envío SÍ es la
            // acción que pidió el gestor — no lo tragamos: PedidoNotificacionProveedorService
            // captura esta excepción y se lo muestra como "no enviado" con el motivo.
            logger.LogError(ex, "Falló el envío de correo a {Destinatario}", destinatario);
            throw;
        }
    }
}
