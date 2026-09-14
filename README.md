# CatalogoPedidos

Proyecto de práctica en .NET 10 + Blazor Web App con Clean Architecture.

**Flujo:** un Gestor captura productos (formulario manual o importando un CSV) → se arma un catálogo →
un Usuario elige un producto y envía una solicitud → el Gestor la aprueba/rechaza desde una bandeja →
tanto el catálogo como cada solicitud se pueden exportar/imprimir a PDF.

## Estructura

```
src/
├── CatalogoPedidos.Domain          Entidades puras (Producto, SolicitudProducto)
├── CatalogoPedidos.Application     Casos de uso, interfaces de repositorio, DTOs
├── CatalogoPedidos.Infrastructure  EF Core + SQL Server, Identity, PDF (QuestPDF), import CSV (CsvHelper)
└── CatalogoPedidos.Web             Blazor Web App (Interactive Server) + páginas
```

## Requisitos

- .NET 10 SDK
- SQL Server corriendo en Docker (contenedor `mssql-server`, ver `Documentos/Repaso de Proyectos/Configuracion SQL Server.txt`)
- Base de datos y usuario dedicados ya creados: `CatalogoPedidosDb` / login `catalogo_app`

## Cómo correrlo

```bash
cd src/CatalogoPedidos.Web
dotnet run --urls "http://localhost:5220"
```

La cadena de conexión real vive en `dotnet user-secrets` (no en `appsettings.json`). Si necesitas verla:

```bash
cd src/CatalogoPedidos.Web
dotnet user-secrets list
```

Las migraciones y los datos de ejemplo (roles, usuarios de prueba, 5 productos) se aplican solos al iniciar.

## Design System (Tailwind CSS v4)

Toda la interfaz usa un único sistema visual: Sidebar + Topbar + componentes reutilizables
(`Components/UI/`: Button, Badge, Card, Alert, Modal, ConfirmDialog, Pagination, Tabs, Dropdown,
EmptyState, FormField, Switch, Loading, Icon). Los tokens de diseño (colores, tipografía, radios)
viven en `src/CatalogoPedidos.Web/Styles/app.tailwind.css`.

El CSS compilado (`wwwroot/app.css`) **no se regenera solo** — hay que correr el build de Tailwind
después de tocar clases nuevas o el archivo de tokens:

```bash
cd src/CatalogoPedidos.Web
npm install        # una sola vez
npm run build:css  # compila una vez
npm run watch:css  # o deja corriendo mientras desarrollas
```

## Cuentas de prueba

| Rol     | Email                  | Password     |
|---------|------------------------|--------------|
| Gestor  | gestor@catalogo.local  | Gestor123!   |
| Usuario | usuario@catalogo.local | Usuario123!  |

## Comandos útiles de EF Core

```bash
# Nueva migración
dotnet ef migrations add NombreMigracion --project src/CatalogoPedidos.Infrastructure --startup-project src/CatalogoPedidos.Web --output-dir Persistence/Migrations

# Aplicar migraciones
dotnet ef database update --project src/CatalogoPedidos.Infrastructure --startup-project src/CatalogoPedidos.Web
```

## Documentación adicional

- [Propuestas de mejora](docs/MEJORAS_PROPUESTAS.md) — oportunidades identificadas sobre el estado actual (modelo de datos, pruebas, rendimiento, seguridad, UX, etc.).
