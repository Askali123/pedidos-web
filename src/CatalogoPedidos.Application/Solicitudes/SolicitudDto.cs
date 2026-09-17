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

public class DecisionSolicitudDto
{
    public int SolicitudId { get; set; }
    public bool Aprobar { get; set; }
}

public class ResolverVariasDto
{
    public List<DecisionSolicitudDto> Decisiones { get; set; } = [];
    public string? ComentarioGestor { get; set; }
}
