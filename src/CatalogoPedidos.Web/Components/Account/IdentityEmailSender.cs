using CatalogoPedidos.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using AppEmailSender = CatalogoPedidos.Application.Notificaciones.IEmailSender;

namespace CatalogoPedidos.Web.Components.Account;

/// <summary>
/// Delega los correos de Identity (confirmación de cuenta, recuperación de contraseña)
/// en la misma infraestructura SMTP que ya usa el envío a proveedores
/// (<see cref="AppEmailSender"/>) — que a su vez elige solo entre envío real o solo-log
/// según haya o no <c>Smtp:Host</c> configurado (ver <c>DependencyInjection.cs</c>). Antes
/// de esto, Identity usaba un sender no-op fijo que nunca mandaba nada, sin importar la
/// configuración SMTP (ver docs/PLAN_MEJORAS_AUTENTICACION.md #1).
///
/// Best-effort a propósito (a diferencia de "Enviar a proveedor", que si falla se lo
/// muestra al gestor): acá la acción principal (crear la cuenta, generar el token de
/// reseteo) ya se completó antes de llamar a este sender, y las pantallas de Identity
/// siempre muestran el mismo mensaje genérico pase lo que pase con el correo (para no
/// filtrar si una cuenta existe o no) — dejar que <see cref="SmtpEmailSender"/> propague
/// la excepción rompería esa pantalla en vez de solo perderse el correo.
/// </summary>
internal sealed class IdentityEmailSender(AppEmailSender emailSender, ILogger<IdentityEmailSender> logger) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        EnviarAsync(email, "Confirmá tu cuenta — CatalogoPedidos",
            $"Confirmá tu cuenta haciendo clic en el siguiente enlace: <a href='{confirmationLink}'>Confirmar cuenta</a>.");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        EnviarAsync(email, "Restablecer tu contraseña — CatalogoPedidos",
            $"Restablecé tu contraseña haciendo clic en el siguiente enlace: <a href='{resetLink}'>Restablecer contraseña</a>.");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        EnviarAsync(email, "Restablecer tu contraseña — CatalogoPedidos",
            $"Usá este código para restablecer tu contraseña: {resetCode}");

    private async Task EnviarAsync(string email, string asunto, string cuerpo)
    {
        try
        {
            await emailSender.EnviarAsync(email, asunto, cuerpo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el envío del correo de cuenta ({Asunto}) a {Destinatario}", asunto, email);
        }
    }
}
