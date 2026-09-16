namespace CatalogoPedidos.Application.Notificaciones;

/// <summary>
/// Envío de correo saliente (a proveedores, por ahora). Abstracto a propósito: la
/// implementación de Infrastructure de hoy solo registra el correo (no hay SMTP real
/// configurado todavía — ver <c>docs/MEJORAS_PROPUESTAS.md</c>). El día que se conecte un
/// proveedor de correo real (SMTP, SendGrid, etc.), solo hace falta cambiar esa
/// implementación; nada en Application ni en la UI debería tener que cambiar.
/// </summary>
public interface IEmailSender
{
    Task EnviarAsync(string destinatario, string asunto, string cuerpo, CancellationToken ct = default);
}
