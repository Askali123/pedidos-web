namespace CatalogoPedidos.Infrastructure.Email;

/// <summary>
/// Configuración del servidor SMTP saliente, leída de la sección "Smtp" — el host/puerto/
/// remitente pueden vivir en <c>appsettings.json</c> (no son secretos), pero <see cref="Usuario"/>
/// y sobre todo <see cref="Password"/> deben configurarse con <c>dotnet user-secrets</c> en
/// desarrollo (nunca en un archivo versionado). Ver README para el paso a paso con Gmail.
/// </summary>
public class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Usuario { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string RemitenteEmail { get; set; } = string.Empty;
    public string RemitenteNombre { get; set; } = "CatalogoPedidos";
}
