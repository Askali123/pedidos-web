namespace CatalogoPedidos.Infrastructure.Identity;

public static class Roles
{
    public const string Usuario = "Usuario";
    public const string Gestor = "Gestor";

    public static readonly string[] Todos = [Usuario, Gestor];
}
