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

## Envío de correo a proveedores (SMTP)

El botón "Enviar a proveedor" (Bandeja / Administrar pedidos) manda un correo real solo si
hay un servidor SMTP configurado; si no, el correo se simula y solo queda en el log de la
app (`CatalogoPedidos.Infrastructure.Email.LoggingEmailSender`) — ver `IEmailSender`.

Para conectar Gmail:

1. Activa la verificación en dos pasos en la cuenta de Gmail que vas a usar para enviar
   (Cuenta de Google → Seguridad → Verificación en 2 pasos). Gmail no acepta tu contraseña
   normal para SMTP, solo **contraseñas de aplicación**.
2. Genera una: Cuenta de Google → Seguridad → Verificación en 2 pasos → **Contraseñas de
   aplicaciones** → elige "Otra (nombre personalizado)" → "CatalogoPedidos". Copia la
   contraseña de 16 caracteres que te muestra (una sola vez).
3. Configúrala en `user-secrets` (nunca en `appsettings.json`, y nunca se la pegues a
   Claude ni a nadie por chat):

   ```bash
   cd src/CatalogoPedidos.Web
   dotnet user-secrets set "Smtp:Host" "smtp.gmail.com"
   dotnet user-secrets set "Smtp:Usuario" "tu-cuenta@gmail.com"
   dotnet user-secrets set "Smtp:Password" "la-contraseña-de-aplicación-de-16-caracteres"
   dotnet user-secrets set "Smtp:RemitenteEmail" "tu-cuenta@gmail.com"
   ```

   `Smtp:Port` (587) y `Smtp:EnableSsl` (true) ya vienen bien por defecto en
   `appsettings.json` para Gmail — no hace falta tocarlos.
4. Reinicia la app. Si `Smtp:Host` quedó configurado, `DependencyInjection` registra
   `SmtpEmailSender` en vez del simulado — sin cambiar nada más del código.

Para otro proveedor SMTP (Outlook, SendGrid, un servidor propio) el procedimiento es el
mismo: solo cambian `Smtp:Host`/`Smtp:Port` y cómo genera ese proveedor la contraseña.

## Cuentas de prueba

| Rol     | Email                  | Password     |
|---------|------------------------|--------------|
| Gestor  | gestor@catalogo.local  | Gestor123!   |
| Usuario | usuario@catalogo.local | Usuario123!  |

Estas cuentas (y los productos de ejemplo del catálogo) solo se crean cuando la app
corre en el ambiente Development (`Seed.EjecutarAsync` en `Program.cs` lo verifica antes
de sembrar nada — los roles sí se crean siempre, son infraestructura necesaria). Si este
proyecto alguna vez se despliega en un ambiente real, no van a aparecer solas, pero de
todos modos conviene no reutilizar estas contraseñas ahí bajo ningún concepto.

## Comandos útiles de EF Core

```bash
# Nueva migración
dotnet ef migrations add NombreMigracion --project src/CatalogoPedidos.Infrastructure --startup-project src/CatalogoPedidos.Web --output-dir Persistence/Migrations

# Aplicar migraciones
dotnet ef database update --project src/CatalogoPedidos.Infrastructure --startup-project src/CatalogoPedidos.Web
```

## Documentación adicional

- [Dominio y casos de uso](docs/DOMINIO_NEGOCIO.md) — la idea de negocio, las entidades, la tabla intermedia Producto↔Proveedor y los casos de uso por rol.
- [Propuestas de mejora](docs/MEJORAS_PROPUESTAS.md) — oportunidades identificadas sobre el estado actual (modelo de datos, pruebas, rendimiento, seguridad, UX, etc.).
- [Plan del flujo pedido → proveedor](docs/PLAN_FLUJO_PEDIDO_PROVEEDOR.md) — checklist priorizado de mejoras al flujo de pedido, aprobación y envío a proveedor, para ir avanzando tarea por tarea.
- [Plan de trazabilidad y entregas](docs/PLAN_TRAZABILIDAD_ENTREGAS.md) — checklist priorizado sobre mensajes de resolución por línea, dirección de entrega y confirmación de entrega.
- [Plan de mejoras de autenticación](docs/PLAN_MEJORAS_AUTENTICACION.md) — checklist priorizado sobre Identity: recuperación de contraseña rota, concurrencia de DbContext en "Administrar cuenta", bloqueo por fuerza bruta y gestión de roles.
- [Plan de mejoras de UI/UX](docs/PLAN_MEJORAS_UI_UX.md) — checklist priorizado sobre la interfaz: validación de formularios, tablas que quedan fuera de pantalla en mobile, paginación, y páginas de cuenta sin traducir ni estilizar.
- [Plan de mejoras — proveedores y entregas](docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md) — checklist priorizado sobre el envío a proveedores cuando un pedido mezcla varios (claridad y evitar duplicados) y la confirmación de entrega, que pasa de ser por pedido a ser por proveedor.
- [Plan de calidad e ingeniería](docs/PLAN_CALIDAD_INGENIERIA.md) — checklist priorizado sobre el bug de stock que nunca se repone, tests automatizados (hoy no hay ninguno), manejo de errores en la UI, auditoría de acciones administrativas y deuda técnica (performance, seguridad, observabilidad).
- [Plan de funcionalidades de negocio](docs/PLAN_FUNCIONALIDADES_NEGOCIO.md) — checklist priorizado sobre funcionalidades nuevas: reportes de gasto, presupuesto por pedido, lead time real por proveedor y otros huecos de negocio.
