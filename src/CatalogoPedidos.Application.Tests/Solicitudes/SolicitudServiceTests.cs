using CatalogoPedidos.Application.Notificaciones;
using CatalogoPedidos.Application.Productos;
using CatalogoPedidos.Application.Proveedores;
using CatalogoPedidos.Application.Solicitudes;
using CatalogoPedidos.Application.Usuarios;
using CatalogoPedidos.Domain.Entities;
using Moq;

namespace CatalogoPedidos.Application.Tests.Solicitudes;

/// <summary>
/// Cubre el autocompletado de Sede/Empresa al crear una Solicitud (snapshot, mismo
/// criterio que DireccionEntrega) — ver docs/PLAN_EMPRESAS_FILIALES.md, Etapa 2.
/// </summary>
public class SolicitudServiceTests
{
    private static SolicitudService CrearServicio(
        out List<Solicitud> creadas,
        UsuarioSedeDto? sedeDelSolicitante)
    {
        var producto = new Producto { Id = 1, Nombre = "Papel", Activo = true };

        var productosMock = new Mock<IProductoRepository>();
        productosMock.Setup(p => p.ObtenerPorIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(producto);

        var solicitudesLocal = new List<Solicitud>();
        var repositorioMock = new Mock<ISolicitudRepository>();
        repositorioMock.Setup(r => r.CrearSolicitudAsync(It.IsAny<Solicitud>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Solicitud s, CancellationToken _) =>
            {
                s.Id = solicitudesLocal.Count + 1;
                solicitudesLocal.Add(s);
                return s;
            });

        var gestoresMock = new Mock<IGestorDirectory>();
        gestoresMock.Setup(g => g.ObtenerIdsGestoresAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var usuarioSedesMock = new Mock<IUsuarioSedeDirectory>();
        usuarioSedesMock.Setup(u => u.ObtenerSedeAsync("user-1", It.IsAny<CancellationToken>())).ReturnsAsync(sedeDelSolicitante);

        creadas = solicitudesLocal;

        return new SolicitudService(
            repositorioMock.Object,
            productosMock.Object,
            Mock.Of<INotificacionRepository>(),
            Mock.Of<INotificacionBroadcaster>(),
            gestoresMock.Object,
            Mock.Of<IProductoProveedorRepository>(),
            usuarioSedesMock.Object);
    }

    [Fact]
    public async Task CrearSolicitudAsync_SolicitanteConSedeAsignada_CopiaSedeYEmpresaComoSnapshot()
    {
        var sede = new UsuarioSedeDto { SedeId = 10, SedeNombre = "Sede Norte", EmpresaId = 5, EmpresaNombre = "Auropaq Colombia" };
        var servicio = CrearServicio(out var creadas, sede);

        var items = new List<CrearSolicitudDto> { new() { ProductoId = 1, Cantidad = 2 } };
        var resultado = await servicio.CrearSolicitudAsync("user-1", "Usuario de Prueba", null, items);

        Assert.Equal(10, resultado.SedeId);
        Assert.Equal("Sede Norte", resultado.SedeNombre);
        Assert.Equal(5, resultado.EmpresaId);
        Assert.Equal("Auropaq Colombia", resultado.EmpresaNombre);
        Assert.Single(creadas);
    }

    [Fact]
    public async Task CrearSolicitudAsync_SolicitanteSinSedeAsignada_NoBloqueaYDejaCamposEnNull()
    {
        var servicio = CrearServicio(out _, sedeDelSolicitante: null);

        var items = new List<CrearSolicitudDto> { new() { ProductoId = 1, Cantidad = 1 } };
        var resultado = await servicio.CrearSolicitudAsync("user-1", "Usuario de Prueba", null, items);

        Assert.Null(resultado.SedeId);
        Assert.Null(resultado.SedeNombre);
        Assert.Null(resultado.EmpresaId);
        Assert.Null(resultado.EmpresaNombre);
    }
}
