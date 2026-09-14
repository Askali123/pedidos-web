namespace CatalogoPedidos.Web;

/// <summary>
/// Cada página fija su título/breadcrumb aquí (OnInitialized); el Topbar se
/// suscribe a los cambios para mostrarlo, sin acoplar el layout a cada página.
/// Scoped: uno por circuito de usuario.
/// </summary>
public class PageHeaderState
{
    public string Title { get; private set; } = "";
    public List<(string Text, string? Url)> Breadcrumb { get; private set; } = new();

    public event Action? Changed;

    public void Set(string title, List<(string Text, string? Url)>? breadcrumb = null)
    {
        Title = title;
        Breadcrumb = breadcrumb ?? new();
        Changed?.Invoke();
    }
}
