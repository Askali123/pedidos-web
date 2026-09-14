using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Web;

public class ItemCarrito
{
    public required Producto Producto { get; init; }
    public int Cantidad { get; set; }
}

/// <summary>
/// Carrito de productos que el usuario arma en el catálogo antes de enviar
/// una sola solicitud (con varios ítems) al gestor. Scoped: uno por circuito de usuario.
/// </summary>
public class CarritoState
{
    private readonly Dictionary<int, ItemCarrito> items = new();

    public IReadOnlyCollection<ItemCarrito> Items => items.Values;
    public int TotalItems => items.Count;
    public bool Contiene(int productoId) => items.ContainsKey(productoId);

    public event Action? Changed;

    public void Agregar(Producto producto, int cantidad = 1)
    {
        if (items.TryGetValue(producto.Id, out var existente))
            existente.Cantidad += cantidad;
        else
            items[producto.Id] = new ItemCarrito { Producto = producto, Cantidad = cantidad };

        Changed?.Invoke();
    }

    public void ActualizarCantidad(int productoId, int cantidad)
    {
        if (items.TryGetValue(productoId, out var item))
        {
            item.Cantidad = Math.Max(1, cantidad);
            Changed?.Invoke();
        }
    }

    public void Quitar(int productoId)
    {
        if (items.Remove(productoId))
            Changed?.Invoke();
    }

    public void Limpiar()
    {
        items.Clear();
        Changed?.Invoke();
    }
}
