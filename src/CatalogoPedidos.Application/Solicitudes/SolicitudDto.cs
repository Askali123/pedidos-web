namespace CatalogoPedidos.Application.Solicitudes;

public class CrearSolicitudDto
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; } = 1;
}

public class ResolverSolicitudDto
{
    public bool Aprobar { get; set; }
    public string? ComentarioGestor { get; set; }
}
