using CatalogoPedidos.Application.Exportacion;
using CatalogoPedidos.Application.Notificaciones;
using CatalogoPedidos.Application.Productos;
using CatalogoPedidos.Application.Proveedores;
using CatalogoPedidos.Application.Solicitudes;
using CatalogoPedidos.Application.Usuarios;
using CatalogoPedidos.Infrastructure.BackgroundJobs;
using CatalogoPedidos.Infrastructure.Email;
using CatalogoPedidos.Infrastructure.Excel;
using CatalogoPedidos.Infrastructure.Identity;
using CatalogoPedidos.Infrastructure.Importacion;
using CatalogoPedidos.Infrastructure.Pdf;
using CatalogoPedidos.Infrastructure.Persistence;
using CatalogoPedidos.Infrastructure.Realtime;
using CatalogoPedidos.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;

namespace CatalogoPedidos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'DefaultConnection'.");

        // Blazor Server: varios componentes de una misma página pueden llamar a la
        // base de datos "al mismo tiempo" (cada uno en su propio ciclo de render).
        // Un DbContext Scoped normal NO soporta eso (no es thread-safe). Por eso
        // usamos una fábrica: cada repositorio pide su propia instancia de corta vida.
        services.AddDbContextFactory<AppDbContext>(options => options.UseSqlServer(connectionString));

        // Identity (UserManager/SignInManager) sí necesita un AppDbContext Scoped normal.
        // Se lo damos pidiéndoselo a la misma fábrica (no configurando otro DbContextOptions aparte).
        services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddScoped<IProductoRepository, ProductoRepository>();
        services.AddScoped<ISolicitudRepository, SolicitudRepository>();
        services.AddScoped<IProductoService, ProductoService>();
        services.AddScoped<ISolicitudService, SolicitudService>();
        services.AddScoped<IProductoImportador, ExcelProductoImportador>();
        services.AddScoped<IPdfExportService, PdfExportService>();
        services.AddScoped<IExcelExportService, ExcelExportService>();

        services.AddScoped<IProveedorRepository, ProveedorRepository>();
        services.AddScoped<IProductoProveedorRepository, ProductoProveedorRepository>();
        services.AddScoped<IProveedorService, ProveedorService>();

        services.AddScoped<INotificacionRepository, NotificacionRepository>();
        services.AddScoped<INotificacionService, NotificacionService>();
        services.AddScoped<IGestorDirectory, GestorDirectory>();
        services.AddSingleton<INotificacionBroadcaster, NotificacionBroadcaster>();

        services.AddScoped<IEmailSender, LoggingEmailSender>();
        services.AddScoped<INotificacionProveedorRepository, NotificacionProveedorRepository>();
        services.AddScoped<IPedidoNotificacionProveedorService, PedidoNotificacionProveedorService>();

        services.AddHostedService<RecordatorioPendientesHostedService>();

        return services;
    }
}
