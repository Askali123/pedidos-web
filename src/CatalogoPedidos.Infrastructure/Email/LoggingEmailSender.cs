using CatalogoPedidos.Application.Notificaciones;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace CatalogoPedidos.Infrastructure.Email;

/// <summary>
/// Implementación "simulada" de <see cref="IEmailSender"/>: no hay ningún SMTP/proveedor de
/// correo configurado todavía, así que en vez de enviar nada de verdad, registra el correo
/// completo en el log de la aplicación (visible en la consola/salida de <c>dotnet run</c>).
/// El resto del sistema (asociar el pedido al proveedor, guardar el envío, mostrarlo en la
/// UI) funciona igual que con un envío real — el día que se conecte un SMTP de verdad, esta
/// es la única clase que hay que reemplazar (ver <c>docs/MEJORAS_PROPUESTAS.md</c>).
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task EnviarAsync(string destinatario, string asunto, string cuerpo, IReadOnlyList<EmailAdjunto>? adjuntos = null, string? copiaA = null, CancellationToken ct = default)
    {
        // Mismo correo sirve de CC y de Reply-To (ver IEmailSender.EnviarAsync) — un envío
        // real lo aplicaría en ambos campos del MailMessage.
        var cc = string.IsNullOrWhiteSpace(copiaA) ? "(ninguna)" : copiaA;

        if (adjuntos is null || adjuntos.Count == 0)
        {
            logger.LogInformation(
                "[Correo simulado — no hay SMTP configurado] Para: {Destinatario} | CC: {Cc} | Reply-To: {ReplyTo} | Asunto: {Asunto}\n{Cuerpo}",
                destinatario, cc, cc, asunto, cuerpo);
        }
        else
        {
            var detalleAdjuntos = string.Join(", ", adjuntos.Select(a => $"{a.NombreArchivo} ({a.Contenido.Length} bytes)"));
            logger.LogInformation(
                "[Correo simulado — no hay SMTP configurado] Para: {Destinatario} | CC: {Cc} | Reply-To: {ReplyTo} | Asunto: {Asunto} | Adjuntos: {Adjuntos}\n{Cuerpo}",
                destinatario, cc, cc, asunto, detalleAdjuntos, cuerpo);
        }

        return Task.CompletedTask;
    }
}
