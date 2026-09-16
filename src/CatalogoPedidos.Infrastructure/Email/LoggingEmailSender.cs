using CatalogoPedidos.Application.Notificaciones;
using Microsoft.Extensions.Logging;

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
    public Task EnviarAsync(string destinatario, string asunto, string cuerpo, EmailAdjunto? adjunto = null, CancellationToken ct = default)
    {
        if (adjunto is null)
        {
            logger.LogInformation(
                "[Correo simulado — no hay SMTP configurado] Para: {Destinatario} | Asunto: {Asunto}\n{Cuerpo}",
                destinatario, asunto, cuerpo);
        }
        else
        {
            logger.LogInformation(
                "[Correo simulado — no hay SMTP configurado] Para: {Destinatario} | Asunto: {Asunto} | Adjunto: {Adjunto} ({Bytes} bytes)\n{Cuerpo}",
                destinatario, asunto, adjunto.NombreArchivo, adjunto.Contenido.Length, cuerpo);
        }

        return Task.CompletedTask;
    }
}
