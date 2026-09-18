using CatalogoPedidos.Application.Exportacion;
using CatalogoPedidos.Application.Productos;
using CatalogoPedidos.Application.Proveedores;
using CatalogoPedidos.Application.Solicitudes;
using CatalogoPedidos.Infrastructure;
using CatalogoPedidos.Infrastructure.Identity;
using CatalogoPedidos.Infrastructure.Persistence;
using CatalogoPedidos.Web;
using CatalogoPedidos.Web.Components;
using CatalogoPedidos.Web.Components.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

var culturaColombia = CultureInfo.GetCultureInfo("es-CO");
CultureInfo.DefaultThreadCurrentCulture = culturaColombia;
CultureInfo.DefaultThreadCurrentUICulture = culturaColombia;

var builder = WebApplication.CreateBuilder(args);

var opcionesLocalizacion = new RequestLocalizationOptions()
    .SetDefaultCulture(culturaColombia.Name)
    .AddSupportedCultures(culturaColombia.Name)
    .AddSupportedUICultures(culturaColombia.Name);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<PageHeaderState>();
builder.Services.AddScoped<CarritoState>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// Autenticado por defecto: cualquier página o endpoint sin [Authorize]/[AllowAnonymous]
// explícito requiere sesión iniciada, en vez de quedar público por accidente — ver
// docs/PLAN_MEJORAS_AUTENTICACION.md #5.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Registra DbContext (SQL Server), Identity con roles, repositorios y servicios de Application.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Scoped (no Singleton) porque delega en el IEmailSender de Application, que también es
// Scoped (SmtpEmailSender/LoggingEmailSender según haya Smtp:Host configurado) — un
// Singleton reteniendo una dependencia Scoped sería una captive dependency.
builder.Services.AddScoped<IEmailSender<ApplicationUser>, IdentityEmailSender>();

var app = builder.Build();

// Aplica migraciones pendientes y siembra roles (siempre) + cuentas/productos de
// ejemplo (solo en desarrollo — ver docs/PLAN_MEJORAS_AUTENTICACION.md #12).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await Seed.EjecutarAsync(scope.ServiceProvider, app.Environment.IsDevelopment());
}

app.UseRequestLocalization(opcionesLocalizacion);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// CSS/JS/imágenes deben poder cargar en páginas públicas (ej. Login) antes de autenticarse.
app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// --- Endpoints de exportación a PDF ---

app.MapGet("/api/catalogo/pdf", async (
    IProductoService productos,
    IProveedorService proveedores,
    IPdfExportService pdf,
    ClaimsPrincipal usuario,
    string? texto,
    string? categoria,
    int? proveedorId) =>
{
    // El filtro/exportación por proveedor es una vista de gestión (códigos internos y del
    // proveedor); si alguien intenta forzarlo por URL sin ser Gestor, se ignora el filtro
    // y se exporta el catálogo general en su lugar.
    if (proveedorId is int idProveedor && usuario.IsInRole(Roles.Gestor))
    {
        var proveedor = await proveedores.ObtenerPorIdAsync(idProveedor);
        var asociaciones = await proveedores.ObtenerProductosDeProveedorAsync(idProveedor, texto, categoria, soloActivos: true);
        var bytesProveedor = pdf.ExportarCatalogoPorProveedor(asociaciones, proveedor?.Nombre ?? "Proveedor");
        return Results.File(bytesProveedor, "application/pdf", "catalogo-proveedor.pdf");
    }

    var catalogo = await productos.ObtenerCatalogoAsync(texto, categoria);
    var bytes = pdf.ExportarCatalogo(catalogo);
    return Results.File(bytes, "application/pdf", "catalogo.pdf");
}).RequireAuthorization();

app.MapGet("/api/solicitudes/{id:int}/pdf", async (int id, ISolicitudService solicitudes, IPdfExportService pdf, ClaimsPrincipal usuario) =>
{
    var solicitud = await solicitudes.ObtenerPorIdAsync(id);
    if (solicitud is null)
        return Results.NotFound();

    var userId = usuario.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    var esGestor = usuario.IsInRole(Roles.Gestor);

    if (!esGestor && solicitud.SolicitanteId != userId)
        return Results.Forbid();

    var bytes = pdf.ExportarSolicitud(solicitud);
    return Results.File(bytes, "application/pdf", $"solicitud-{solicitud.Id}.pdf");
}).RequireAuthorization();

app.MapGet("/api/pedidos/{id:int}/pdf", async (int id, ISolicitudService solicitudes, IPdfExportService pdf, ClaimsPrincipal usuario) =>
{
    var pedido = await solicitudes.ObtenerPedidoAsync(id);
    if (pedido is null)
        return Results.NotFound();

    var userId = usuario.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    var esGestor = usuario.IsInRole(Roles.Gestor);

    if (!esGestor && pedido.SolicitanteId != userId)
        return Results.Forbid();

    var bytes = pdf.ExportarPedido(pedido);
    return Results.File(bytes, "application/pdf", $"pedido-{pedido.Id}.pdf");
}).RequireAuthorization();

static FiltroSolicitudesDto ConstruirFiltroReporte(DateTime? desde, DateTime? hasta, string? solicitanteId, int? productoId, int? proveedorId, CatalogoPedidos.Domain.Enums.EstadoSolicitud? estado)
    => new()
    {
        FechaDesde = desde,
        FechaHasta = hasta,
        SolicitanteId = solicitanteId,
        ProductoId = productoId,
        ProveedorId = proveedorId,
        Estado = estado
    };

app.MapGet("/api/solicitudes/reporte/pdf", async (
    ISolicitudService solicitudes,
    IProveedorService proveedores,
    IPdfExportService pdf,
    DateTime? desde,
    DateTime? hasta,
    string? solicitanteId,
    int? productoId,
    int? proveedorId,
    CatalogoPedidos.Domain.Enums.EstadoSolicitud? estado) =>
{
    var filtro = ConstruirFiltroReporte(desde, hasta, solicitanteId, productoId, proveedorId, estado);
    var resultado = await solicitudes.BuscarAsync(filtro);

    if (proveedorId is int idProveedor)
    {
        var proveedor = await proveedores.ObtenerPorIdAsync(idProveedor);
        var codigos = (await proveedores.ObtenerProductosDeProveedorAsync(idProveedor))
            .ToDictionary(pp => pp.ProductoId, pp => pp.CodigoProveedor);
        var bytesProveedor = pdf.ExportarSolicitudesPorProveedor(resultado, codigos, proveedor?.Nombre ?? "Proveedor");
        return Results.File(bytesProveedor, "application/pdf", "pedido-proveedor.pdf");
    }

    var bytes = pdf.ExportarSolicitudes(resultado);
    return Results.File(bytes, "application/pdf", "reporte-solicitudes.pdf");
}).RequireAuthorization(new AuthorizeAttribute { Roles = Roles.Gestor });

app.MapGet("/api/solicitudes/reporte/excel", async (
    ISolicitudService solicitudes,
    IProveedorService proveedores,
    IExcelExportService excel,
    DateTime? desde,
    DateTime? hasta,
    string? solicitanteId,
    int? productoId,
    int? proveedorId,
    CatalogoPedidos.Domain.Enums.EstadoSolicitud? estado) =>
{
    var filtro = ConstruirFiltroReporte(desde, hasta, solicitanteId, productoId, proveedorId, estado);
    var resultado = await solicitudes.BuscarAsync(filtro);

    if (proveedorId is int idProveedor)
    {
        var proveedor = await proveedores.ObtenerPorIdAsync(idProveedor);
        var codigos = (await proveedores.ObtenerProductosDeProveedorAsync(idProveedor))
            .ToDictionary(pp => pp.ProductoId, pp => pp.CodigoProveedor);
        var bytesProveedor = excel.ExportarSolicitudesPorProveedor(resultado, codigos, proveedor?.Nombre ?? "Proveedor");
        return Results.File(bytesProveedor, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "pedido-proveedor.xlsx");
    }

    var bytes = excel.ExportarSolicitudes(resultado);
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "reporte-solicitudes.xlsx");
}).RequireAuthorization(new AuthorizeAttribute { Roles = Roles.Gestor });

app.Run();
