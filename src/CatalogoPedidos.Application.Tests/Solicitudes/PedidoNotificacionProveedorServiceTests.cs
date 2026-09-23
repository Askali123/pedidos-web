using CatalogoPedidos.Application.Exportacion;
using CatalogoPedidos.Application.Notificaciones;
using CatalogoPedidos.Application.Proveedores;
using CatalogoPedidos.Application.Solicitudes;
using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Domain.Enums;
using Moq;

namespace CatalogoPedidos.Application.Tests.Solicitudes;

/// <summary>
/// Casos de prueba de la sección 9 de docs/PLAN_PEDIDO_PROVEEDOR.md, contra
/// <see cref="PedidoNotificacionProveedorService"/> — el corazón de la regla de negocio
/// "un producto solicitado no puede terminar repartido/duplicado entre dos proveedores".
/// Los repositorios se simulan con Moq respaldado por listas en memoria (no mocks
/// "stateless") porque varios casos necesitan leer después de escribir (enviar y volver a
/// consultar disponibilidad) dentro del mismo test.
/// </summary>
public class PedidoNotificacionProveedorServiceTests
{
    private sealed class Entorno
    {
        public required PedidoNotificacionProveedorService Servicio { get; init; }
        public required List<NotificacionProveedor> Envios { get; init; }
        public required List<PedidoProveedor> Documentos { get; init; }
        public required Mock<IEmailSender> EmailSender { get; init; }
    }

    private static Producto CrearProducto(int id, string nombre = "Producto") => new() { Id = id, Nombre = $"{nombre} {id}" };

    private static Proveedor CrearProveedor(int id, string nombre) => new() { Id = id, Nombre = nombre, Email = $"{nombre.ToLowerInvariant().Replace(" ", "")}@test.com" };

    private static DetalleSolicitud Aprobada(int id, int productoId, int cantidad, Producto producto) => new()
    {
        Id = id,
        ProductoId = productoId,
        Producto = producto,
        Cantidad = cantidad,
        Estado = EstadoSolicitud.Aprobada,
        SolicitanteId = "user-1",
        SolicitanteNombre = "Usuario de Prueba"
    };

    private static ProductoProveedor Asociacion(int productoId, Proveedor proveedor, string codigo, bool activo = true) => new()
    {
        ProductoId = productoId,
        ProveedorId = proveedor.Id,
        Proveedor = proveedor,
        CodigoProveedor = codigo,
        Activo = activo
    };

    private static Solicitud CrearSolicitud(int id, params DetalleSolicitud[] items)
    {
        var solicitud = new Solicitud
        {
            Id = id,
            SolicitanteId = "user-1",
            SolicitanteNombre = "Usuario de Prueba",
            FechaCreacion = DateTime.UtcNow
        };
        foreach (var item in items)
            solicitud.Items.Add(item);
        return solicitud;
    }

    /// <summary>
    /// Arma el servicio con repositorios simulados: lecturas resuelven contra
    /// <paramref name="solicitud"/>/<paramref name="asociaciones"/> (mutables — los tests
    /// de snapshot cambian sus propiedades después del envío), y las escrituras de
    /// documentos/envíos quedan en listas en memoria para poder volver a consultarlas.
    /// </summary>
    private static Entorno CrearEntorno(Solicitud solicitud, List<ProductoProveedor> asociaciones)
    {
        var solicitudesMock = new Mock<ISolicitudRepository>();
        solicitudesMock.Setup(s => s.ObtenerSolicitudAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => id == solicitud.Id ? solicitud : null);

        var asociacionesMock = new Mock<IProductoProveedorRepository>();
        asociacionesMock.Setup(a => a.ObtenerPorProductosAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<int> ids, CancellationToken _) => asociaciones.Where(a => ids.Contains(a.ProductoId)).ToList());

        var envios = new List<NotificacionProveedor>();
        var enviosMock = new Mock<INotificacionProveedorRepository>();
        enviosMock.Setup(e => e.ObtenerPorSolicitudAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => envios.Where(e => e.SolicitudId == id).ToList());
        enviosMock.Setup(e => e.CrearAsync(It.IsAny<NotificacionProveedor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificacionProveedor n, CancellationToken _) =>
            {
                n.Id = envios.Count + 1;
                envios.Add(n);
                return n;
            });

        var documentos = new List<PedidoProveedor>();
        var documentosMock = new Mock<IPedidoProveedorRepository>();
        documentosMock.Setup(p => p.ObtenerPorSolicitudYProveedorAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int solId, int provId, CancellationToken _) => documentos.FirstOrDefault(d => d.SolicitudId == solId && d.ProveedorId == provId));
        documentosMock.Setup(p => p.ObtenerPorSolicitudAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int solId, CancellationToken _) => documentos.Where(d => d.SolicitudId == solId).ToList());
        documentosMock.Setup(p => p.CrearAsync(It.IsAny<PedidoProveedor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PedidoProveedor doc, CancellationToken _) =>
            {
                doc.Id = documentos.Count + 1;
                documentos.Add(doc);
                return doc;
            });
        documentosMock.Setup(p => p.ActualizarAsync(It.IsAny<PedidoProveedor>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var emailMock = new Mock<IEmailSender>();
        emailMock.Setup(e => e.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<EmailAdjunto>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var pdfMock = new Mock<IPdfExportService>();
        pdfMock.Setup(p => p.ExportarPedidoProveedor(It.IsAny<PedidoProveedor>(), It.IsAny<string>())).Returns([]);

        var excelMock = new Mock<IExcelExportService>();
        excelMock.Setup(e => e.ExportarPedidoProveedor(It.IsAny<PedidoProveedor>(), It.IsAny<string>())).Returns([]);

        var servicio = new PedidoNotificacionProveedorService(
            solicitudesMock.Object,
            asociacionesMock.Object,
            enviosMock.Object,
            documentosMock.Object,
            emailMock.Object,
            pdfMock.Object,
            excelMock.Object);

        return new Entorno { Servicio = servicio, Envios = envios, Documentos = documentos, EmailSender = emailMock };
    }

    [Fact]
    public async Task ObtenerProveedoresDisponibles_SoloIncluyeProductosAsociadosAEseProveedor()
    {
        // Solicitud con A, B, C aprobados; Proveedor X asociado solo a A y B.
        var productoA = CrearProducto(1, "Papel higiénico");
        var productoB = CrearProducto(2, "Jabón de manos");
        var productoC = CrearProducto(3, "Toallas");
        var solicitud = CrearSolicitud(10,
            Aprobada(1, productoA.Id, 5, productoA),
            Aprobada(2, productoB.Id, 3, productoB),
            Aprobada(3, productoC.Id, 7, productoC));

        var proveedorX = CrearProveedor(100, "ProveedorX");
        var asociaciones = new List<ProductoProveedor>
        {
            Asociacion(productoA.Id, proveedorX, "COD-A"),
            Asociacion(productoB.Id, proveedorX, "COD-B")
            // C no tiene asociación con ningún proveedor.
        };

        var entorno = CrearEntorno(solicitud, asociaciones);

        var resultado = await entorno.Servicio.ObtenerProveedoresDisponiblesAsync(solicitud.Id);

        var opcion = Assert.Single(resultado);
        Assert.Equal(proveedorX.Id, opcion.ProveedorId);
        Assert.Equal(2, opcion.CantidadLineas);
        Assert.Equal([productoA.Id, productoB.Id], opcion.Productos.Select(p => p.ProductoId).OrderBy(x => x));
        Assert.Equal("COD-A", opcion.Productos.Single(p => p.ProductoId == productoA.Id).CodigoProveedor);
        Assert.Equal("COD-B", opcion.Productos.Single(p => p.ProductoId == productoB.Id).CodigoProveedor);
    }

    [Fact]
    public async Task EnviarAProveedor_ConProductoDeVariosProveedores_SoloAfectaAlElegidoYElOtroQuedaLibreDeLaTransaccion()
    {
        var producto = CrearProducto(1, "Papel higiénico");
        var solicitud = CrearSolicitud(20, Aprobada(1, producto.Id, 10, producto));

        var proveedorX = CrearProveedor(100, "ProveedorX");
        var proveedorY = CrearProveedor(200, "ProveedorY");
        var asociacionX = Asociacion(producto.Id, proveedorX, "X-1");
        var asociacionY = Asociacion(producto.Id, proveedorY, "Y-1");
        var asociaciones = new List<ProductoProveedor> { asociacionX, asociacionY };

        var entorno = CrearEntorno(solicitud, asociaciones);

        // Antes de enviar, ambos proveedores compiten por la misma línea.
        var disponiblesAntes = await entorno.Servicio.ObtenerProveedoresDisponiblesAsync(solicitud.Id);
        Assert.Equal(2, disponiblesAntes.Count);

        var resultado = await entorno.Servicio.EnviarAProveedorAsync(solicitud.Id, proveedorX.Id, "gestor-1", "Gestor Uno");
        Assert.True(resultado.Enviado);

        // Se generó documento SOLO para X.
        Assert.Single(entorno.Documentos, d => d.ProveedorId == proveedorX.Id);
        Assert.DoesNotContain(entorno.Documentos, d => d.ProveedorId == proveedorY.Id);

        // La asociación de catálogo de Y no se tocó (sigue activa, mismo código).
        Assert.True(asociacionY.Activo);
        Assert.Equal("Y-1", asociacionY.CodigoProveedor);

        // Y deja de competir por esta línea (ya se le pidió a X) — el otro proveedor
        // "sigue libre" en el sentido de que no quedó nada pendiente de mandarle.
        var disponiblesDespues = await entorno.Servicio.ObtenerProveedoresDisponiblesAsync(solicitud.Id);
        Assert.DoesNotContain(disponiblesDespues, p => p.ProveedorId == proveedorY.Id);
    }

    [Fact]
    public async Task ObtenerProveedoresDisponibles_ProductoSinProveedor_QuedaExcluidoYElRestoSigue()
    {
        var productoConProveedor = CrearProducto(1, "Con proveedor");
        var productoSinProveedor = CrearProducto(2, "Sin proveedor");
        var solicitud = CrearSolicitud(30,
            Aprobada(1, productoConProveedor.Id, 4, productoConProveedor),
            Aprobada(2, productoSinProveedor.Id, 1, productoSinProveedor));

        var proveedorX = CrearProveedor(100, "ProveedorX");
        var asociaciones = new List<ProductoProveedor> { Asociacion(productoConProveedor.Id, proveedorX, "COD-1") };

        var entorno = CrearEntorno(solicitud, asociaciones);

        var resultado = await entorno.Servicio.ObtenerProveedoresDisponiblesAsync(solicitud.Id);

        // El resto del pedido (el producto CON proveedor) sigue ofreciéndose normalmente.
        var opcion = Assert.Single(resultado);
        Assert.Equal(1, opcion.CantidadLineas);
        Assert.DoesNotContain(opcion.Productos, p => p.ProductoId == productoSinProveedor.Id);
        // El aviso visual ("badge sin proveedor") es de la UI (Bandeja/Administrar.razor),
        // no de este servicio — acá solo se verifica que no rompe el resto ni se ofrece.
    }

    [Fact]
    public async Task EnviarAProveedor_CambioDeCodigoDespues_NoAlteraElDocumentoYaEmitido()
    {
        var producto = CrearProducto(1, "Tornillo 10mm");
        var solicitud = CrearSolicitud(40, Aprobada(1, producto.Id, 20, producto));

        var proveedorX = CrearProveedor(100, "ProveedorX");
        var asociacion = Asociacion(producto.Id, proveedorX, "COD-VIEJO");
        var asociaciones = new List<ProductoProveedor> { asociacion };

        var entorno = CrearEntorno(solicitud, asociaciones);

        var resultado = await entorno.Servicio.EnviarAProveedorAsync(solicitud.Id, proveedorX.Id, "gestor-1", "Gestor Uno");
        Assert.True(resultado.Enviado);

        var documento = Assert.Single(entorno.Documentos);
        Assert.Equal("COD-VIEJO", documento.Items.Single().CodigoProveedor);

        // El código cambia en el catálogo DESPUÉS del envío...
        asociacion.CodigoProveedor = "COD-NUEVO";

        // ...pero el documento ya emitido conserva el snapshot viejo.
        Assert.Equal("COD-VIEJO", documento.Items.Single().CodigoProveedor);
    }

    [Fact]
    public async Task EnviarAProveedor_DesactivarAsociacionDespues_NoAlteraHistoricoYExcluyeNuevosEnvios()
    {
        var productoA = CrearProducto(1, "Producto A");
        var productoB = CrearProducto(2, "Producto B");
        var proveedorX = CrearProveedor(100, "ProveedorX");
        var asociacionA = Asociacion(productoA.Id, proveedorX, "COD-A");
        var asociacionB = Asociacion(productoB.Id, proveedorX, "COD-B");
        var asociaciones = new List<ProductoProveedor> { asociacionA, asociacionB };

        var solicitud1 = CrearSolicitud(50, Aprobada(1, productoA.Id, 5, productoA));
        var entorno = CrearEntorno(solicitud1, asociaciones);

        var resultado1 = await entorno.Servicio.EnviarAProveedorAsync(solicitud1.Id, proveedorX.Id, "gestor-1", "Gestor Uno");
        Assert.True(resultado1.Enviado);
        var documentoHistorico = Assert.Single(entorno.Documentos);

        // Se desactiva la asociación del proveedor con el producto B (y, para este caso,
        // también con A — lo que importa es que deja de poder recibir pedidos nuevos).
        asociacionA.Activo = false;
        asociacionB.Activo = false;

        // El histórico de la solicitud 1 no se alteró.
        Assert.Equal("COD-A", documentoHistorico.Items.Single().CodigoProveedor);
        Assert.Single(entorno.Documentos);

        // Una solicitud NUEVA con el producto B ya no puede mandarse a este proveedor.
        var solicitud2 = CrearSolicitud(51, Aprobada(2, productoB.Id, 2, productoB));
        var entorno2 = CrearEntorno(solicitud2, asociaciones);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => entorno2.Servicio.EnviarAProveedorAsync(solicitud2.Id, proveedorX.Id, "gestor-1", "Gestor Uno"));
    }

    [Fact]
    public async Task EnviarAProveedor_ExcluirUnaLinea_QuedaMarcadaYElCatalogoNoSeToca()
    {
        var productoA = CrearProducto(1, "Producto A");
        var productoB = CrearProducto(2, "Producto B");
        var proveedorX = CrearProveedor(100, "ProveedorX");
        var asociacionA = Asociacion(productoA.Id, proveedorX, "COD-A");
        var asociacionB = Asociacion(productoB.Id, proveedorX, "COD-B");
        var asociaciones = new List<ProductoProveedor> { asociacionA, asociacionB };

        var solicitud = CrearSolicitud(60,
            Aprobada(1, productoA.Id, 3, productoA),
            Aprobada(2, productoB.Id, 4, productoB));

        var entorno = CrearEntorno(solicitud, asociaciones);

        // El gestor destilda el producto B antes de confirmar el envío.
        var resultado = await entorno.Servicio.EnviarAProveedorAsync(
            solicitud.Id, proveedorX.Id, "gestor-1", "Gestor Uno", productoIdsSeleccionados: [productoA.Id]);

        Assert.True(resultado.Enviado);
        Assert.Equal(1, resultado.CantidadLineas);

        var documento = Assert.Single(entorno.Documentos);
        Assert.Equal(2, documento.Items.Count);
        Assert.False(documento.Items.Single(i => i.ProductoId == productoA.Id).Excluido);
        Assert.True(documento.Items.Single(i => i.ProductoId == productoB.Id).Excluido);

        // El catálogo (ProductoProveedor) no se tocó por excluir la línea de ESTE pedido.
        Assert.True(asociacionB.Activo);
        Assert.Equal("COD-B", asociacionB.CodigoProveedor);
    }

    [Fact]
    public async Task EnviarAProveedor_ReenvioEnCooldown_SeRechazaSinDuplicarElDocumento()
    {
        var producto = CrearProducto(1, "Producto A");
        var proveedorX = CrearProveedor(100, "ProveedorX");
        var asociaciones = new List<ProductoProveedor> { Asociacion(producto.Id, proveedorX, "COD-1") };
        var solicitud = CrearSolicitud(70, Aprobada(1, producto.Id, 5, producto));

        var entorno = CrearEntorno(solicitud, asociaciones);

        var primerEnvio = await entorno.Servicio.EnviarAProveedorAsync(solicitud.Id, proveedorX.Id, "gestor-1", "Gestor Uno");
        Assert.True(primerEnvio.Enviado);
        Assert.Single(entorno.Documentos);
        Assert.Single(entorno.Envios);

        // Reenvío inmediato (mismo pedido, mismo proveedor) — cae dentro del cooldown de 5 min.
        var segundoEnvio = await entorno.Servicio.EnviarAProveedorAsync(solicitud.Id, proveedorX.Id, "gestor-1", "Gestor Uno");

        Assert.False(segundoEnvio.Enviado);
        Assert.NotNull(segundoEnvio.Motivo);
        Assert.Contains("Espera", segundoEnvio.Motivo);

        // No se duplicó el documento ni se generó una segunda notificación.
        Assert.Single(entorno.Documentos);
        Assert.Single(entorno.Envios);
        entorno.EmailSender.Verify(
            e => e.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<EmailAdjunto>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
