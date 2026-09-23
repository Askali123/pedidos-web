using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CatalogoPedidos.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>();
    public DbSet<DetalleSolicitud> DetallesSolicitud => Set<DetalleSolicitud>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<ProductoProveedor> ProductoProveedores => Set<ProductoProveedor>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<NotificacionProveedor> NotificacionesProveedor => Set<NotificacionProveedor>();
    public DbSet<ConfirmacionEntrega> ConfirmacionesEntrega => Set<ConfirmacionEntrega>();
    public DbSet<PedidoProveedor> PedidosProveedor => Set<PedidoProveedor>();
    public DbSet<DetallePedidoProveedor> DetallesPedidoProveedor => Set<DetallePedidoProveedor>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Solicitud (antes "Pedido") y DetalleSolicitud (antes "SolicitudProducto") se
        // renombraron en el código el 2026-09-22 para separar el vocabulario de "lo que pide
        // el usuario" del "pedido a un proveedor" (PedidoProveedor) — ver
        // docs/PLAN_PEDIDO_PROVEEDOR.md, sección 11. Las tablas/columnas físicas NO se
        // tocan (se fijan a los nombres de siempre) para no arriesgar los datos reales: es
        // un rename de vocabulario en C#, no una migración de esquema.
        builder.Entity<Solicitud>().ToTable("Pedidos");
        builder.Entity<DetalleSolicitud>().ToTable("Solicitudes");
        builder.Entity<DetalleSolicitud>().Property(d => d.SolicitudId).HasColumnName("PedidoId");
        builder.Entity<ConfirmacionEntrega>().Property(c => c.SolicitudId).HasColumnName("PedidoId");
        builder.Entity<NotificacionProveedor>().Property(n => n.SolicitudId).HasColumnName("PedidoId");
        builder.Entity<PedidoProveedor>().Property(pp => pp.SolicitudId).HasColumnName("PedidoId");

        builder.Entity<Producto>(entity =>
        {
            entity.Property(p => p.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Categoria).HasMaxLength(100);
            entity.Property(p => p.UnidadMedida).HasMaxLength(50);
            entity.Property(p => p.Precio).HasColumnType("decimal(18,2)");
        });

        builder.Entity<Solicitud>(entity =>
        {
            entity.Property(p => p.SolicitanteNombre).HasMaxLength(200).IsRequired();
        });

        builder.Entity<DetalleSolicitud>(entity =>
        {
            entity.HasOne(s => s.Producto)
                  .WithMany(p => p.Solicitudes)
                  .HasForeignKey(s => s.ProductoId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Solicitud)
                  .WithMany(p => p.Items)
                  .HasForeignKey(s => s.SolicitudId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Proveedor>(entity =>
        {
            entity.Property(p => p.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Nit).HasMaxLength(50);
        });

        builder.Entity<ProductoProveedor>(entity =>
        {
            entity.Property(pp => pp.CodigoProveedor).HasMaxLength(100).IsRequired();
            entity.Property(pp => pp.PrecioProveedor).HasColumnType("decimal(18,2)");

            entity.HasOne(pp => pp.Producto)
                  .WithMany(p => p.Proveedores)
                  .HasForeignKey(pp => pp.ProductoId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pp => pp.Proveedor)
                  .WithMany(p => p.Productos)
                  .HasForeignKey(pp => pp.ProveedorId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Un proveedor no puede asociarse dos veces al mismo producto.
            entity.HasIndex(pp => new { pp.ProductoId, pp.ProveedorId }).IsUnique();

            // El código que usa un proveedor para identificar un producto debe ser
            // único PARA ESE PROVEEDOR (dos proveedores distintos sí pueden coincidir en código).
            entity.HasIndex(pp => new { pp.ProveedorId, pp.CodigoProveedor }).IsUnique();
        });

        builder.Entity<Notificacion>(entity =>
        {
            entity.Property(n => n.Titulo).HasMaxLength(200).IsRequired();
            entity.Property(n => n.Mensaje).HasMaxLength(500).IsRequired();
            entity.Property(n => n.Url).HasMaxLength(300);
            entity.HasIndex(n => new { n.UsuarioDestinoId, n.Leida });
        });

        builder.Entity<NotificacionProveedor>(entity =>
        {
            entity.Property(n => n.Email).HasMaxLength(256).IsRequired();

            entity.HasOne(n => n.Solicitud)
                  .WithMany(p => p.NotificacionesProveedor)
                  .HasForeignKey(n => n.SolicitudId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(n => n.Proveedor)
                  .WithMany()
                  .HasForeignKey(n => n.ProveedorId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(n => n.PedidoProveedor)
                  .WithMany(pp => pp.Notificaciones)
                  .HasForeignKey(n => n.PedidoProveedorId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PedidoProveedor>(entity =>
        {
            entity.Property(pp => pp.GestorNombre).HasMaxLength(200);

            // Un mismo (Solicitud, Proveedor) genera UN solo documento: los reenvíos
            // reutilizan el existente y añaden notificaciones (no documentos) nuevos.
            entity.HasIndex(pp => new { pp.SolicitudId, pp.ProveedorId }).IsUnique();

            entity.HasOne(pp => pp.Solicitud)
                  .WithMany(p => p.PedidosProveedor)
                  .HasForeignKey(pp => pp.SolicitudId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pp => pp.Proveedor)
                  .WithMany()
                  .HasForeignKey(pp => pp.ProveedorId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DetallePedidoProveedor>(entity =>
        {
            entity.Property(d => d.ProductoNombre).HasMaxLength(200).IsRequired();
            entity.Property(d => d.CodigoProveedor).HasMaxLength(100).IsRequired();
            entity.Property(d => d.UnidadMedida).HasMaxLength(50);
            entity.Property(d => d.PrecioProveedor).HasColumnType("decimal(18,2)");

            entity.HasOne(d => d.PedidoProveedor)
                  .WithMany(pp => pp.Items)
                  .HasForeignKey(d => d.PedidoProveedorId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Producto)
                  .WithMany()
                  .HasForeignKey(d => d.ProductoId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Un producto no aparece dos veces en el mismo documento (aunque un reenvío
            // pueda ANEXAR líneas nuevas, la duplicación se evita en el servicio filtrando
            // por ProductoId ya presente).
            entity.HasIndex(d => new { d.PedidoProveedorId, d.ProductoId }).IsUnique();
        });

        builder.Entity<ConfirmacionEntrega>(entity =>
        {
            entity.Property(c => c.ConfirmadoPorNombre).HasMaxLength(200).IsRequired();

            entity.HasOne(c => c.Solicitud)
                  .WithMany(p => p.ConfirmacionesEntrega)
                  .HasForeignKey(c => c.SolicitudId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.Proveedor)
                  .WithMany()
                  .HasForeignKey(c => c.ProveedorId)
                  .OnDelete(DeleteBehavior.Restrict);

            // No es único: un proveedor no debería confirmarse dos veces en el mismo
            // pedido, pero eso se valida en el servicio (ConfirmacionEntregaService), no
            // acá — un índice único con ProveedorId nullable no protegería el caso
            // "sin proveedor" en SQL Server (cada NULL cuenta como distinto). El índice
            // solo ayuda a las consultas por pedido.
            entity.HasIndex(c => c.SolicitudId);
        });
    }
}
