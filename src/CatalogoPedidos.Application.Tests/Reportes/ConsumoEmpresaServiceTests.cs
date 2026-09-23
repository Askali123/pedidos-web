using CatalogoPedidos.Application.Proveedores;
using CatalogoPedidos.Application.Reportes;
using CatalogoPedidos.Application.Solicitudes;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Domain.Enums;
using Moq;

namespace CatalogoPedidos.Application.Tests.Reportes;

/// <summary>
/// Cubre la regla de "qué código de proveedor mostrar" del dashboard de consumo por
/// empresa/sede: el snapshot congelado si la línea ya se envió, el preferido del catálogo
/// como referencia si no — ver docs/PLAN_EMPRESAS_FILIALES.md, Etapa 6, §2.
/// </summary>
public class ConsumoEmpresaServiceTests
{
    private static DetalleSolicitud CrearLinea(int detalleId, int solicitudId, int productoId, string productoNombre, string categoria)
    {
        var solicitud = new Solicitud
        {
            Id = solicitudId,
            SolicitanteId = "user-1",
            SolicitanteNombre = "Usuario de Prueba",
            EmpresaId = 5,
            EmpresaNombre = "Auropaq Colombia",
            SedeId = 10,
            SedeNombre = "Sede Norte"
        };

        return new DetalleSolicitud
        {
            Id = detalleId,
            SolicitudId = solicitudId,
            Solicitud = solicitud,
            ProductoId = productoId,
            Producto = new Producto { Id = productoId, Nombre = productoNombre, Categoria = categoria },
            Cantidad = 3,
            SolicitanteId = "user-1",
            SolicitanteNombre = "Usuario de Prueba",
            Estado = EstadoSolicitud.Aprobada,
            FechaSolicitud = DateTime.UtcNow
        };
    }

    private static ConsumoEmpresaService CrearServicio(List<DetalleSolicitud> items, List<PedidoProveedor> documentos, Dictionary<int, ProductoProveedor> preferidos)
    {
        var solicitudesMock = new Mock<ISolicitudRepository>();
        solicitudesMock.Setup(s => s.BuscarAsync(It.IsAny<FiltroSolicitudesDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(items);

        var documentosMock = new Mock<IPedidoProveedorRepository>();
        documentosMock.Setup(d => d.ObtenerPorSolicitudesAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync(documentos);

        var proveedoresMock = new Mock<IProveedorService>();
        proveedoresMock.Setup(p => p.ObtenerCodigosPreferidosAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync(preferidos);

        return new ConsumoEmpresaService(solicitudesMock.Object, documentosMock.Object, proveedoresMock.Object);
    }

    [Fact]
    public async Task ObtenerConsumoAsync_LineaYaEnviada_UsaElCodigoSnapshotDelEnvioNoElPreferido()
    {
        var linea = CrearLinea(detalleId: 1, solicitudId: 100, productoId: 1, "Papel", "Aseo");

        var pedidoProveedor = new PedidoProveedor
        {
            SolicitudId = 100,
            ProveedorId = 9,
            Items = [new DetallePedidoProveedor { ProductoId = 1, ProductoNombre = "Papel", CodigoProveedor = "COD-ENVIADO", Cantidad = 3 }]
        };

        var preferidos = new Dictionary<int, ProductoProveedor>
        {
            [1] = new ProductoProveedor { ProductoId = 1, CodigoProveedor = "COD-PREFERIDO" }
        };

        var servicio = CrearServicio([linea], [pedidoProveedor], preferidos);

        var resultado = await servicio.ObtenerConsumoAsync(new FiltroSolicitudesDto());

        Assert.Single(resultado);
        Assert.Equal("COD-ENVIADO", resultado[0].CodigoProveedor);
    }

    [Fact]
    public async Task ObtenerConsumoAsync_LineaSinEnviar_UsaElCodigoPreferidoComoReferencia()
    {
        var linea = CrearLinea(detalleId: 1, solicitudId: 100, productoId: 1, "Papel", "Aseo");
        var preferidos = new Dictionary<int, ProductoProveedor>
        {
            [1] = new ProductoProveedor { ProductoId = 1, CodigoProveedor = "COD-PREFERIDO" }
        };

        var servicio = CrearServicio([linea], documentos: [], preferidos);

        var resultado = await servicio.ObtenerConsumoAsync(new FiltroSolicitudesDto());

        Assert.Equal("COD-PREFERIDO", resultado[0].CodigoProveedor);
    }

    [Fact]
    public async Task ObtenerConsumoAsync_SinNingunProveedorAsociado_CodigoProveedorQuedaNull()
    {
        var linea = CrearLinea(detalleId: 1, solicitudId: 100, productoId: 1, "Papel", "Aseo");

        var servicio = CrearServicio([linea], documentos: [], preferidos: []);

        var resultado = await servicio.ObtenerConsumoAsync(new FiltroSolicitudesDto());

        Assert.Null(resultado[0].CodigoProveedor);
    }

    [Fact]
    public async Task ObtenerConsumoAsync_CopiaElSnapshotDeEmpresaYSedeDesdeLaSolicitud()
    {
        var linea = CrearLinea(detalleId: 1, solicitudId: 100, productoId: 1, "Papel", "Aseo");

        var servicio = CrearServicio([linea], documentos: [], preferidos: []);

        var resultado = await servicio.ObtenerConsumoAsync(new FiltroSolicitudesDto());

        Assert.Equal("Auropaq Colombia", resultado[0].EmpresaNombre);
        Assert.Equal("Sede Norte", resultado[0].SedeNombre);
        Assert.Equal(1, resultado[0].CodigoInterno);
        Assert.Equal("Aseo", resultado[0].Categoria);
    }
}
