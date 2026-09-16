using System.Text.Json;
using CatalogoPedidos.Application.Productos;
using CatalogoPedidos.Domain.Entities;
using Microsoft.JSInterop;

namespace CatalogoPedidos.Web;

public class ItemCarrito
{
    public required Producto Producto { get; init; }
    public int Cantidad { get; set; }
}

/// <summary>
/// Carrito de productos que el usuario arma en el catálogo antes de enviar
/// una sola solicitud (con varios ítems) al gestor. Scoped: uno por circuito de usuario.
///
/// Se respalda en <c>localStorage</c> del navegador (namespaced por usuario, para que dos
/// cuentas en el mismo navegador no se mezclen) — así sobrevive a una recarga forzada o a
/// que se caiga la conexión de Blazor Server, que antes vaciaban el carrito por completo
/// al perderse el circuito. Solo guarda ProductoId+Cantidad (no el Producto completo): al
/// restaurar se vuelve a pedir cada producto a la base, así el precio/stock/nombre que se
/// ve siempre está actualizado, y un producto desactivado mientras tanto simplemente no
/// vuelve (ver <see cref="IProductoService"/> y la tarea 3 del plan de mejoras).
/// </summary>
public class CarritoState(IJSRuntime js, IProductoService productos)
{
    private sealed record ItemGuardado(int ProductoId, int Cantidad);

    private readonly Dictionary<int, ItemCarrito> items = new();
    private string? usuarioId;
    private bool restaurado;

    public IReadOnlyCollection<ItemCarrito> Items => items.Values;
    public int TotalItems => items.Count;
    public bool Contiene(int productoId) => items.ContainsKey(productoId);

    public event Action? Changed;

    private string Clave => $"carrito:{usuarioId}";

    /// <summary>
    /// Se llama una sola vez por circuito, después del primer render (recién ahí hay
    /// conexión JS interop disponible en Blazor Server), con el id del usuario actual.
    /// </summary>
    public async Task RestaurarAsync(string usuarioIdActual, CancellationToken ct = default)
    {
        if (restaurado)
            return;

        restaurado = true;
        usuarioId = usuarioIdActual;

        string? json;
        try
        {
            json = await js.InvokeAsync<string?>("localStorage.getItem", ct, Clave);
        }
        catch
        {
            return; // localStorage no disponible todavía (p. ej. prerender) — no es crítico.
        }

        if (string.IsNullOrWhiteSpace(json))
            return;

        List<ItemGuardado>? guardados;
        try
        {
            guardados = JsonSerializer.Deserialize<List<ItemGuardado>>(json);
        }
        catch
        {
            return; // dato corrupto/de otra versión — mejor un carrito vacío que uno roto.
        }

        if (guardados is null || guardados.Count == 0)
            return;

        foreach (var guardado in guardados)
        {
            var producto = await productos.ObtenerPorIdAsync(guardado.ProductoId, ct);
            if (producto is not null && producto.Activo)
                items[guardado.ProductoId] = new ItemCarrito { Producto = producto, Cantidad = guardado.Cantidad };
        }

        Changed?.Invoke();
    }

    private async Task GuardarAsync()
    {
        if (usuarioId is null)
            return; // todavía no se restauró para nadie en este circuito — nada que namespacear.

        try
        {
            var guardados = items.Values.Select(i => new ItemGuardado(i.Producto.Id, i.Cantidad)).ToList();
            await js.InvokeVoidAsync("localStorage.setItem", Clave, JsonSerializer.Serialize(guardados));
        }
        catch
        {
            // Best-effort: si falla guardar (localStorage lleno, deshabilitado, etc.) el
            // carrito en memoria sigue funcionando igual para el resto de esta sesión.
        }
    }

    public void Agregar(Producto producto, int cantidad = 1)
    {
        if (items.TryGetValue(producto.Id, out var existente))
            existente.Cantidad += cantidad;
        else
            items[producto.Id] = new ItemCarrito { Producto = producto, Cantidad = cantidad };

        Changed?.Invoke();
        _ = GuardarAsync();
    }

    public void ActualizarCantidad(int productoId, int cantidad)
    {
        if (items.TryGetValue(productoId, out var item))
        {
            item.Cantidad = Math.Max(1, cantidad);
            Changed?.Invoke();
            _ = GuardarAsync();
        }
    }

    public void Quitar(int productoId)
    {
        if (items.Remove(productoId))
        {
            Changed?.Invoke();
            _ = GuardarAsync();
        }
    }

    public void Limpiar()
    {
        items.Clear();
        Changed?.Invoke();
        _ = GuardarAsync();
    }
}
