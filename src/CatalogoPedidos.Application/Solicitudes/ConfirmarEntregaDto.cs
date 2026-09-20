namespace CatalogoPedidos.Application.Solicitudes;

public class ConfirmarEntregaDto
{
    public string? DireccionEntregada { get; set; }
    public bool CoincideConDireccionIndicada { get; set; }

    /// <summary>Quién recibió físicamente los insumos — distinto de quién confirma la entrega desde la app.</summary>
    public string? RecibidoPorNombre { get; set; }

    public string? Observaciones { get; set; }
}
