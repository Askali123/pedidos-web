using CatalogoPedidos.Application.Empresas;
using CatalogoPedidos.Application.Sedes;
using CatalogoPedidos.Domain.Entities;
using Moq;

namespace CatalogoPedidos.Application.Tests.Empresas;

/// <summary>
/// Cubre la cascada Empresa→Sedes al desactivar — ver docs/PLAN_EMPRESAS_FILIALES.md,
/// Etapa 9: una Sede no puede seguir "activa" con su Empresa inactiva.
/// </summary>
public class EmpresaServiceTests
{
    [Fact]
    public async Task DesactivarAsync_ConSedesActivas_LasDesactivaEnCascada()
    {
        var empresa = new Empresa { Id = 1, Nombre = "Auropaq Colombia", Activo = true };
        var sedeNorte = new Sede { Id = 10, EmpresaId = 1, Nombre = "Sede Norte", Activo = true };
        var sedeSur = new Sede { Id = 11, EmpresaId = 1, Nombre = "Sede Sur", Activo = true };

        var empresasMock = new Mock<IEmpresaRepository>();
        empresasMock.Setup(e => e.ObtenerPorIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(empresa);
        empresasMock.Setup(e => e.ActualizarAsync(It.IsAny<Empresa>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sedesActualizadas = new List<Sede>();
        var sedesMock = new Mock<ISedeRepository>();
        sedesMock.Setup(s => s.ObtenerTodasAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([sedeNorte, sedeSur]);
        sedesMock.Setup(s => s.ActualizarAsync(It.IsAny<Sede>(), It.IsAny<CancellationToken>()))
            .Callback<Sede, CancellationToken>((sede, _) => sedesActualizadas.Add(sede))
            .Returns(Task.CompletedTask);

        var servicio = new EmpresaService(empresasMock.Object, sedesMock.Object);

        await servicio.DesactivarAsync(1);

        Assert.False(empresa.Activo);
        Assert.Equal(2, sedesActualizadas.Count);
        Assert.All(sedesActualizadas, s => Assert.False(s.Activo));
    }

    [Fact]
    public async Task DesactivarAsync_SinSedesActivas_NoLlamaActualizarSedes()
    {
        var empresa = new Empresa { Id = 1, Nombre = "Auropaq Colombia", Activo = true };

        var empresasMock = new Mock<IEmpresaRepository>();
        empresasMock.Setup(e => e.ObtenerPorIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(empresa);
        empresasMock.Setup(e => e.ActualizarAsync(It.IsAny<Empresa>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sedesMock = new Mock<ISedeRepository>();
        sedesMock.Setup(s => s.ObtenerTodasAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var servicio = new EmpresaService(empresasMock.Object, sedesMock.Object);

        await servicio.DesactivarAsync(1);

        sedesMock.Verify(s => s.ActualizarAsync(It.IsAny<Sede>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
