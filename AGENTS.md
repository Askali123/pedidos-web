# AGENTS.md — CatalogoPedidos

Aplicación .NET 10 + Blazor Web App (Interactive Server) con Clean Architecture y Tailwind CSS v4.
Idioma del repo: **español** (docs, comentarios y commits).

## Comandos (verificados)

- Build: `dotnet build src/CatalogoPedidos.slnx` — la solución usa el formato **slnx** (no `.sln`). Build limpio (0 errores/advertencias) es la única verificación disponible: no hay tests, CI ni linter en el repo.
- Correr: `cd src/CatalogoPedidos.Web; dotnet run --urls "http://localhost:5220"`
- Migración nueva (siempre con ambos proyectos, si no falla):
  `dotnet ef migrations add Nombre --project src/CatalogoPedidos.Infrastructure --startup-project src/CatalogoPedidos.Web --output-dir Persistence/Migrations`
- Aplicar migraciones:
  `dotnet ef database update --project src/CatalogoPedidos.Infrastructure --startup-project src/CatalogoPedidos.Web`

## Tailwind CSS (gotcha importante)

- `wwwroot/app.css` **está versionado y NO se regenera con `dotnet build`**. Tras tocar clases nuevas o `Styles/app.tailwind.css` hay que correr (en `src/CatalogoPedidos.Web`; `npm install` una sola vez): `npm run build:css` o `npm run watch:css`.
- Tailwind v4: no se puede `@apply` sobre clases propias. Los tokens de diseño viven en `Styles/app.tailwind.css` (`@theme`): usar siempre los tokens (`bg-card`, `text-heading`, `bg-primary-600`, `shadow-card`…), nunca colores gray/white de Tailwind sueltos.
- Reutilizar los componentes de `Components/UI/` (Button, Card, Badge, Modal, Pagination, Dropdown…) antes de escribir HTML nuevo.

## DB, secretos y arranque

- SQL Server corre en Docker (contenedor `mssql-server`); BD `CatalogoPedidosDb`, login `catalogo_app`. Sin el contenedor arriba la app no arranca (la migración automática falla).
- La cadena de conexión y las credenciales SMTP **no** viven en `appsettings.json` (allí la cadena está vacía a propósito): viven en user-secrets. Ver: `cd src/CatalogoPedidos.Web; dotnet user-secrets list`. Nunca commitear secretos.
- Al iniciar, `Program.cs` aplica migraciones y corre `Seed.EjecutarAsync`: los roles siempre, cuentas de prueba + productos solo en Development. Cuentas: `gestor@catalogo.local` / `Gestor123!` y `usuario@catalogo.local` / `Usuario123!`.
- Correo: si `Smtp:Host` está seteado en secrets se usa `SmtpEmailSender`; si no, `LoggingEmailSender` (simulado, solo log) — ver `Infrastructure/DependencyInjection.cs`.

## Arquitectura

- `src/`: Domain (entidades puras) → Application (casos de uso, interfaces de repo, DTOs) → Infrastructure (EF Core, Identity, PDF/Excel, SMTP, importación, background job) ← Web (Blazor + páginas). Dependencias solo en esa dirección.
- Infrastructure usa `FrameworkReference Microsoft.AspNetCore.App` porque Identity vive ahí, no en Web.
- **DbContext (no romper):** Blazor Server ejecuta `OnInitializedAsync` en paralelo entre componentes de una misma página; un `AppDbContext` Scoped compartido no es thread-safe y lanza `InvalidOperationException`. La regla es `IDbContextFactory<AppDbContext>`: los repositorios crean su propia instancia de corta vida por operación, e Identity recibe un contexto Scoped tomado de la misma fábrica (ver `DependencyInjection.cs`). No inyectar `AppDbContext` Scoped en componentes/repositorios nuevos.
- Notificaciones en tiempo real: singleton `NotificacionBroadcaster` con evento C#; los componentes suscriben/desuscriben con `IDisposable`.
- Endpoints de PDF/Excel y reportes son minimal APIs en `Program.cs` (`/api/...`), no controllers.
- Cultura fija es-CO (precios en pesos) fijada en `Program.cs`.

## Convenciones

- Rama de trabajo: `Develop` (main recibe merges). Commits en español, estilo "Completa …", "Corrige …".
- Auth: `FallbackPolicy` exige usuario autenticado por defecto (`Program.cs`); páginas/endpoints públicos deben declarar `[AllowAnonymous]` explícito; acciones de Gestor usan `Roles.Gestor`.
- Cada área funcional tiene un plan en `docs/` (p.ej. `docs/PLAN_MEJORAS_UI_UX.md`, `docs/PLAN_MEJORAS_AUTENTICACION.md`, `docs/PLAN_CALIDAD_INGENIERIA.md`): leer el plan correspondiente antes de modificar esa área.