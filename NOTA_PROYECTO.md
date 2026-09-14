# CatalogoPedidos

- **Objetivo**: Aprender .NET 10 + Blazor Web App + Clean Architecture construyendo un catálogo de productos donde un usuario elige un producto, envía una solicitud a un gestor, y todo se puede exportar/imprimir a PDF.
- **Estado**: en progreso — flujo completo funcionando de punta a punta.
- **Stack / tecnologías**: .NET 10, Blazor Web App (Interactive Server), ASP.NET Core Identity + Roles, EF Core (con `IDbContextFactory`), SQL Server (Docker), QuestPDF, CsvHelper, ClosedXML, Tailwind CSS v4.
- **Próximos pasos**:
  - Descontar stock automáticamente al aprobar una solicitud.
  - Paginar más listas cuando crezcan (ya hay `Pagination` en Catálogo y Administrar pedidos).
  - Agregar pruebas automatizadas (xUnit) para Application.
  - Explorar MediatR/CQRS como siguiente nivel de Clean Architecture.
  - Permitir editar/desactivar proveedores (hoy solo se crean y se listan).
  - Alerta de stock bajo al gestor (cuando se implemente el descuento de stock).
  - Recordatorio automático de pedidos pendientes hace varios días (hoy solo se resaltan visualmente en la Bandeja, >48h).
  - Extender el Design System al resto de páginas de Identity (Register, Manage/*) — por ahora solo Login quedó restilizado; las demás siguen con el markup scaffolded original de Microsoft.
- **Enlaces relacionados**: [[repaso-sql-server]] · [Propuestas de mejora](docs/MEJORAS_PROPUESTAS.md)

## Qué aprendí

- Separar Domain / Application / Infrastructure / Web y por qué Identity vive en Infrastructure, no en Web.
- `FrameworkReference Microsoft.AspNetCore.App` para usar tipos de ASP.NET Core (Identity) desde un classlib.
- CsvHelper hace lecturas síncronas: el stream de `InputFile` de Blazor solo soporta async, por eso hay que copiarlo primero a un `MemoryStream`.
- `dotnet user-secrets` para no dejar contraseñas en `appsettings.json`.
- `RequestLocalizationOptions` para fijar la cultura (es-CO) y que los precios salgan en pesos, no en euros.
- Relación muchos-a-muchos "con datos extra" (Producto↔Proveedor) se modela con una entidad intermedia propia (`ProductoProveedor`), no con una tabla puente simple, porque cada proveedor guarda SU PROPIO código para el mismo producto.
- Dos índices únicos distintos resuelven las dos reglas de negocio: `(ProductoId, ProveedorId)` evita asociar el mismo proveedor dos veces al mismo producto; `(ProveedorId, CodigoProveedor)` evita que un proveedor repita su código en otro producto — y además se valida en el Application layer para dar un mensaje de error claro, no solo un error de SQL.
- **Bug real encontrado**: en Blazor Server, varios componentes de UNA MISMA página (p.ej. la campanita de notificaciones en el layout + el catálogo en el body) corren su `OnInitializedAsync` de forma concurrente y comparten el mismo `DbContext` con scope de circuito → `InvalidOperationException: A second operation was started on this context instance before a previous operation completed`. La solución correcta (no un parche) es `IDbContextFactory<AppDbContext>`: cada repositorio pide su propia instancia de corta vida por operación en vez de recibir un DbContext ya armado por inyección.
- Para que Identity (que sí necesita un `AppDbContext` Scoped "de verdad") conviva con la fábrica sin registrar dos veces `DbContextOptions`, el truco es: `services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext())` — Identity recibe un contexto scoped que en realidad viene de la misma fábrica.
- Notificaciones en tiempo real en Blazor Server sin SignalR Hub aparte: un singleton en memoria con un evento de C# (`event Action<Notificacion>?`) que cada componente conectado suscribe/desuscribe (`IDisposable`) filtrando por su propio UsuarioId. Aprovecha que Blazor Server YA mantiene una conexión persistente por usuario.
- **Tailwind v4 no permite `@apply` sobre tus propias clases** (solo sobre utilidades nativas de Tailwind). `.btn-primary { @apply btn bg-primary-600 ...; }` falla con "unknown utility class". Solución: la clase base (`.btn`) y la variante (`.btn-primary`) se combinan en el *markup* (`class="btn btn-primary"`), cada una con sus propios estilos por separado en el CSS.
- Colores semánticos (`primary`, `success`, `warning`, `danger`, `info`) se definen en `@theme` como **alias** de la paleta de Tailwind ya existente (`--color-primary-600: var(--color-blue-600)`), no como hex nuevos — así `bg-primary-600` funciona igual que cualquier utilidad nativa, sin duplicar la paleta de colores.
- Para compartir "título de página + breadcrumb" entre cada página de contenido y el Topbar (que vive en un layout distinto) sin acoplarlos, un servicio `Scoped` simple con un evento (`PageHeaderState`) es más limpio que pelear con cascading parameters de varios niveles.

## Qué mejoraría

- El nombre del solicitante/gestor usa el email como "nombre" — falta pedir nombre completo real en el registro.
- No hay control de stock automático al aprobar una solicitud (el stock no se descuenta todavía).
