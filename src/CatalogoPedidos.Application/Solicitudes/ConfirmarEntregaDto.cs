namespace CatalogoPedidos.Application.Solicitudes;

public class ConfirmarEntregaDto
{
    public string? DireccionEntregada { get; set; }
    public bool CoincideConDireccionIndicada { get; set; }
    public string? Observaciones { get; set; }
}
