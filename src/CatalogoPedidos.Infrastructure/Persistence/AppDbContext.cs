using CatalogoPedidos.Domain.Entities;
using CatalogoPedidos.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CatalogoPedidos.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<SolicitudProducto> Solicitudes => Set<SolicitudProducto>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<ProductoProveedor> ProductoProveedores => Set<ProductoProveedor>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Producto>(entity =>
        {
            entity.Property(p => p.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Categoria).HasMaxLength(100);
            entity.Property(p => p.Precio).HasColumnType("decimal(18,2)");
        });

        builder.Entity<SolicitudProducto>(entity =>
        {
            entity.HasOne(s => s.Producto)
                  .WithMany(p => p.Solicitudes)
                  .HasForeignKey(s => s.ProductoId)
                  .OnDelete(DeleteBehavior.Restrict);
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
    }
}
