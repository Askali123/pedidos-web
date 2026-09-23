# Plan — Empresas filiales del holding Auropaq

Análisis de soporte actual + plan de implementación para la nueva funcionalidad pedida:
cada usuario Solicitante queda asociado a una **sede** de una **empresa filial**
perteneciente al grupo empresarial **Auropaq**, con un módulo de administración
(crear/editar/desactivar empresas y sedes, con datos de geolocalización de cada sede, y
asociar usuarios a cada sede) y un **dashboard** de solicitudes/consumos por empresa, por
sede, por año, por mes y por fecha, con el detalle de cada solicitud (solicitante, fecha,
insumos, categoría, código interno, código de proveedor) y exportación a Excel de cada
corte del reporte. También se pide un mecanismo para que, con el paso de los años, el
histórico de solicitudes se pueda mover a un área más comprimida del mismo sistema en vez
de crecer indefinidamente en las tablas activas.

Este documento sigue la misma convención que el resto de `docs/PLAN_*.md`: primero el
diagnóstico de lo que hay hoy, después el modelo objetivo, los gaps que hay que resolver
y las decisiones a validar, y al final el plan por etapas con checklist. **No hay código
implementado todavía** — este documento es solo análisis + plan, tal como se pidió.

**Precisión importante sobre el alcance**: toda solicitud que pase por este módulo nuevo
pertenece a una empresa que es filial del grupo empresarial Auropaq — el módulo **no**
modela empresas/clientes genéricos ni terceros ajenos al holding. `Empresa` en este
sistema significa, siempre, "filial de Auropaq". Esto es lo que justifica no modelar un
holding aparte (§2): no hace falta una tabla `Holding` porque cada fila de `Empresa` ya
es, por definición y por alcance del sistema, una filial de Auropaq — el dato "pertenece
a Auropaq" no varía por fila, así que no aporta nada modelarlo como relación. Si algún día
el sistema tuviera que operar con empresas de **otro** grupo o clientes fuera del holding,
ese sería un cambio de alcance distinto (agregar `Holding` como entidad real), no algo que
este plan cubre.

## 1. Diagnóstico — ¿el backend soporta esto hoy?

**No. No existe ningún concepto de Empresa/Filial/Holding/Sede en el proyecto.**
Verificado con `grep -rli "empresa|filial|holding|auropaq|sede\b" src/ docs/`: las únicas
coincidencias son incidentales (una mención genérica de "identificador propio de la
empresa" en `PLAN_FUNCIONALIDADES_NEGOCIO.md` y "sede norte" como texto libre de ejemplo
en un campo `Observaciones` de `PLAN_TRAZABILIDAD_ENTREGAS.md`) — ninguna es una entidad
real. Es una funcionalidad enteramente nueva (greenfield), no una extensión de algo que ya
existe a medias. Tampoco existe ningún mecanismo de archivado/compresión histórica de
datos — todas las tablas del dominio crecen indefinidamente hoy.

Lo que sí existe y es directamente relevante:

- **`ApplicationUser`** (`Infrastructure/Identity/ApplicationUser.cs`) solo tiene
  `NombreCompleto` y `DireccionPredeterminada`. Ninguna asociación a nada parecido a una
  empresa o sede.
- **`Solicitud`** (antes `Pedido`, renombrada el 2026-09-22) ya tiene el patrón exacto que
  necesitamos replicar: `DireccionEntrega` es un **snapshot** editable, autocompletado
  desde `ApplicationUser.DireccionPredeterminada` al armar el carrito, que **no cambia
  retroactivamente** si el perfil del usuario cambia después. Los campos de
  Empresa/Sede en `Solicitud` deberían seguir el mismo patrón.
- **Roles**: solo existen `Usuario` y `Gestor` (`Infrastructure/Identity/Roles.cs`). No
  hay un rol "Administrador" separado — `docs/DOMINIO_NEGOCIO.md` lo dice explícito: "el
  Gestor es dueño de todo el back-office". La nueva pantalla de administración debería
  reusar `Roles.Gestor`, no crear un rol nuevo, salvo que se decida lo contrario (ver §4).
- **Patrón de CRUD con baja lógica**: `Proveedor` (`Nombre`, `Nit?`, `Contacto?`,
  `Telefono?`, `Email?`, `Activo`, `FechaCreacion`) + `Proveedores.razor` +
  `IProveedorService.DesactivarAsync` es el molde a copiar tanto para `Empresa` como para
  `Sede` — mismo patrón de baja lógica (nunca borrado físico), misma UI de tabla con
  crear/editar/desactivar.
- **Patrón de asociación usuario↔atributo**: `GestionRoles.razor` +
  `IUsuarioRolService` + `UsuarioRolDto` (`Id`, `Email`, `NombreCompleto`, `EsGestor`) es
  el molde para "asociar usuarios a cada sede" — una tabla con todos los usuarios y un
  control por fila, usando `ScopeFactory.CreateAsyncScope()` para tomar un `UserManager`
  con contexto propio (evita el problema de `AppDbContext` Scoped compartido entre
  componentes Blazor Server).
- **Reportes existentes**: no hay ningún dashboard agregado por período hoy.
  `IPdfExportService`/`IExcelExportService` solo generan listados planos (catálogo,
  solicitudes filtradas, pedido a un proveedor). El filtro más parecido a "por año/mes"
  que ya existe es el rango de fechas Desde/Hasta en `Administrar.razor` — reusable como
  base, pero no hay agrupación ni vista de dashboard.

### Gaps que ya estaban identificados (y que esta funcionalidad toca de lleno)

`docs/PLAN_FUNCIONALIDADES_NEGOCIO.md` tiene 3 tareas pendientes (`[ ]`, ninguna hecha)
que se solapan directamente con lo pedido:

- **Tarea 1 — Reporte de gasto por proveedor/categoría/período**: prácticamente el mismo
  problema de "agrupar solicitudes por período" que pide el nuevo dashboard, solo que
  agrupado por Proveedor/Categoría en vez de por Empresa/Sede. **Recomendación**:
  construir una sola infraestructura de reporte (filtro de rango de fechas + agregación
  reusable) que sirva para ambos, en vez de dos mecanismos paralelos — ver Etapa 6.
- **Tarea 2 — Centro de costo / presupuesto por pedido**: proponía un campo
  `CentroCosto`/`Departamento` opcional en `Solicitud` por la misma razón que ahora
  motiva `Empresa`/`Sede` (agrupar el gasto por unidad organizativa). **Recomendación**:
  la Tarea 2 queda **superada/absorbida** por `Sede` (el nivel operativo más fino que ya
  vamos a tener) — no tiene sentido tener dos campos de agrupación organizativa
  paralelos.
- **Tarea 4 — Normalizar `Categoria` a entidad propia** y **Tarea 6 — Código/SKU interno
  propio del producto**: el dashboard pedido muestra explícitamente "categorías" y
  "códigos internos" — hoy `Categoria` es texto libre (permite duplicados tipo "Aseo" vs
  "aseo") y no existe ningún SKU propio, solo `Producto.Id` (autoincremental, que
  `docs/DOMINIO_NEGOCIO.md` ya documenta como "el código interno del catálogo"). **Estas
  NO son bloqueantes** para construir el dashboard — se puede lanzar v1 usando
  `Producto.Id` como código interno y `Categoria` tal cual (texto libre) — pero heredan
  sus mismas limitaciones. Quedan como mejoras independientes, no como prerrequisito.

## 2. Modelo objetivo

Dos niveles, confirmado: una **Empresa** (filial de Auropaq) puede tener **varias Sedes**
físicas, y cada usuario pertenece a una Sede concreta (no directamente a la Empresa) —
porque es en la Sede donde efectivamente se necesitan los insumos.

```
Empresa (filial de Auropaq)
 └─ Sede 1 (país, ciudad, dirección propios)
 │   └─ Usuarios (N) → Solicitudes (N)
 └─ Sede 2 (país, ciudad, dirección propios)
     └─ Usuarios (N) → Solicitudes (N)
```

### Entidad nueva: `Empresa` — nivel agregador

| Campo | Tipo | Notas |
|---|---|---|
| `Id` | int | |
| `Nombre` | string | Obligatorio. Nombre de la filial (p. ej. "Auropaq"). |
| `Nit` | string? | Opcional — si el NIT es a nivel de filial completa (lo más común en Colombia) y no por sede. |
| `Activo` | bool | Baja lógica (`DesactivarAsync`) — igual que `Proveedor`. Desactivar una Empresa no borra sus Sedes ni su histórico. |
| `FechaCreacion` | DateTime | |

### Entidad nueva: `Sede` — nivel operativo, con geolocalización

| Campo | Tipo | Notas |
|---|---|---|
| `Id` | int | |
| `EmpresaId` → `Empresa` | int | FK, `OnDelete: Restrict` (no se puede borrar una Empresa con Sedes activas sin antes desactivarlas). |
| `Nombre` | string | Obligatorio (p. ej. "Sede Norte", "Sede Medellín"). |
| `Pais` | string | Obligatorio — geolocalización. |
| `Ciudad` | string | Obligatorio — geolocalización. |
| `Direccion` | string? | Opcional — dirección física exacta. |
| `Contacto`, `Telefono`, `Email` | string? | Opcionales, mismo patrón que `Proveedor`. |
| `Activo` | bool | Baja lógica (`DesactivarAsync`) — no se borra físico; sus usuarios y solicitudes históricas quedan intactos. |
| `FechaCreacion` | DateTime | |

No se modela "Auropaq" (el holding) como entidad propia — ver justificación en la
introducción de este documento.

### `ApplicationUser` — asociación a Sede

Agregar `SedeId` (int?, FK a `Sede`, nullable) + navegación opcional. Nullable porque las
cuentas existentes no tendrán sede asignada hasta que un Gestor las asocie manualmente
(ver Etapa 1) — no hay heurística de backfill automático. La Empresa de un usuario se
deriva siempre de `Sede.EmpresaId` — no se duplica `EmpresaId` en `ApplicationUser` para
no tener dos fuentes de verdad que se puedan desincronizar.

### `Solicitud` — snapshot de Sede y Empresa

Agregar `SedeId` (int?) + `SedeNombre` (string?) + `EmpresaId` (int?) + `EmpresaNombre`
(string?) — mismo patrón que `DireccionEntrega`: se autocompletan desde
`ApplicationUser.SedeId` → `Sede`/`Empresa` al crear la solicitud, y **no cambian** si
luego se reasigna al usuario a otra sede — el histórico queda fiel a la sede/empresa real
al momento del pedido, igual que ya se garantiza con `SolicitanteNombre` y
`DireccionEntrega`. Se guardan **ambos niveles** (Sede y Empresa) desnormalizados en la
misma fila para que el dashboard pueda agrupar por cualquiera de los dos sin joins —
mismo criterio ya usado en `DetallePedidoProveedor`, que snapshotea `Categoria` para
evitar depender de un join al reportar.

### Dashboard — filtros y de dónde sale cada dato pedido

El Gestor necesita ver los **consumos** (solicitudes + insumos) de forma organizada e
intuitiva, combinando estos filtros — no una sola vista fija sino varias formas de cortar
el mismo dato:

- **Por empresa** — una filial a la vez (agregando todas sus sedes), o todas comparadas.
- **Por sede** — una sede concreta dentro de una empresa.
- **Por mes** y **por año** — el pedido explícito original.
- **Por fecha** — rango libre (Desde/Hasta), igual al filtro que ya existe en
  `Administrar.razor`, para cuando el corte mes/año no alcanza.

Los filtros se combinan (empresa + sede + año + mes, o empresa + rango de fechas libre) en
vez de ser vistas separadas — mismo criterio de filtro combinable que ya usa
`Administrar.razor` hoy con fecha/solicitante/producto/estado/proveedor.

| Dato pedido | Fuente |
|---|---|
| Solicitudes por empresa/sede/año/mes/fecha | `Solicitud.EmpresaId`/`SedeId` + `FechaCreacion` |
| Solicitante | `Solicitud.SolicitanteNombre` (ya desnormalizado) |
| Fecha | `Solicitud.FechaCreacion` |
| Detalle de insumos | `DetalleSolicitud` → `Producto.Nombre`/`Cantidad` |
| Categoría | `Producto.Categoria` (texto libre, ver gap Tarea 4) |
| Código interno | `Producto.Id` (ver gap Tarea 6) |
| Código de proveedor | Si la línea ya se envió a un proveedor:
  `DetallePedidoProveedor.CodigoProveedor` (snapshot congelado, más preciso). Si no se ha
  enviado todavía: `ProductoProveedor.CodigoProveedor` del proveedor preferido
  (`EsPreferido`), como referencia. |

**Exportación a Excel**: cada corte del dashboard (la combinación de filtros activa en
pantalla) debe poder exportarse a Excel con el mismo detalle que se está viendo — reusando
`IExcelExportService` y el patrón ya usado para exportar solicitudes, no un solo botón
fijo para un único reporte.

### Archivado histórico anual

Pedido explícito: con el paso de los años, el histórico de solicitudes (y sus tablas
relacionadas — `DetalleSolicitud`, `PedidoProveedor`, etc.) no debería crecer para
siempre en las tablas "en vivo" — debería poder pasarse a un área más comprimida **dentro
del mismo sistema** (no un export externo).

Dos formas de lograrlo, con trade-offs muy distintos:

1. **Particionamiento de tablas en SQL Server (por año)** — transparente para la app (el
   motor de base de datos decide dónde vive cada fila), permite compresión real de datos
   fríos (`DATA_COMPRESSION`), pero requiere `SQL Server Enterprise`/`Standard` con
   soporte de particiones, y bastante trabajo de DBA para definir los *filegroups* y el
   *sliding window* de archivado. Sobra para el volumen de datos que este sistema maneja
   hoy (unas pocas empresas/sedes, no millones de filas).
2. **Archivado a nivel de aplicación** — tablas paralelas (`SolicitudArchivada`,
   `DetalleSolicitudArchivada`, con la misma forma que las originales) + una acción
   administrativa ("Archivar año X") que mueve las filas de solicitudes cerradas de un año
   ya terminado a esas tablas y las quita de las tablas activas. Más simple, portable
   (no depende de ediciones especiales de SQL Server), y el dashboard puede seguir
   consultando años archivados con el mismo filtro de Año — solo que la consulta apunta a
   la tabla de archivo en vez de la activa.

**Recomendación**: opción 2 (archivado a nivel de aplicación), por ser la más simple y no
requerir capacidades especiales de SQL Server — consistente con cómo está construido el
resto del sistema. Ver decisión de prioridad/momento en §4: no hay todavía datos reales de
volumen/crecimiento que justifiquen construir esto ya mismo en la v1 del dashboard.

## 3. Gaps a resolver (además de las entidades nuevas)

1. Migración EF Core aditiva: tablas `Empresas` y `Sedes` + columna `SedeId` nullable en
   `AspNetUsers` + columnas `SedeId`/`SedeNombre`/`EmpresaId`/`EmpresaNombre` nullable en
   `Solicitudes`. Todo aditivo, sin riesgo sobre datos existentes — mismo criterio ya
   usado en las migraciones de esta sesión.
2. No hay ningún mecanismo hoy para "agrupar y filtrar por período + entidad
   organizativa" — hay que construirlo (Etapa 6), reusando el filtro de fechas de
   `Administrar.razor` como base.
3. Datos existentes: hoy ningún usuario ni ninguna solicitud tiene empresa/sede. Al
   lanzar, todo el histórico y las cuentas actuales aparecerán como "sin sede asignada"
   hasta que un Gestor las asocie manualmente uno por uno (no hay heurística automática
   confiable para backfill).
4. Seed de desarrollo (`Seed.EjecutarAsync`, solo Development): agregar 1 empresa con 2
   sedes de prueba (para poder probar de una vez el caso "empresa con varias sedes") y
   asociar las cuentas de prueba (`usuario@catalogo.local`) a una de ellas.
5. No existe ningún mecanismo de archivado histórico hoy — cualquier implementación es
   greenfield (ver §2, Archivado histórico anual).

## 4. Decisiones a validar

- ~~¿Empresa y Sede son el mismo concepto o dos niveles distintos?~~ **Confirmado por el
  usuario**: dos niveles. Una Empresa puede tener varias Sedes, cada una con su propia
  geolocalización (país, ciudad, dirección); el usuario se asocia a la Sede, no
  directamente a la Empresa.
- **¿La Tarea 2 (CentroCosto) del plan de funcionalidades de negocio queda absorbida por
  Sede, o son dos niveles distintos (Sede → Departamento)?** Recomendación: que Sede la
  reemplace — evita dos campos de agrupación organizativa redundantes.
- **¿Quién administra Empresas/Sedes — todo `Gestor` o un rol más restringido?**
  Recomendación: reusar `Roles.Gestor` (consistente con "el Gestor es dueño de todo el
  back-office", `docs/DOMINIO_NEGOCIO.md`). Un rol `Administrador` separado es más trabajo
  para un caso que hoy nadie pidió explícitamente.
- **¿Un usuario pertenece a una sola Sede o puede tener varias?** El pedido describe la
  asociación en singular. Recomendación: relación 1—N simple (`ApplicationUser.SedeId`
  nullable, no una tabla intermedia) — más simple; si después se necesita que un usuario
  pertenezca a varias sedes, se puede migrar a una tabla intermedia sin perder datos.
- **¿"Auropaq" necesita ser una entidad (`Holding`) o alcanza con ser implícito?**
  Recomendación: implícito (ninguna tabla `Holding`) salvo que el negocio realmente vaya a
  operar más de un holding en esta misma instancia de la app.
- **¿Cuándo construir el archivado histórico anual (§2)?** Es una pieza de
  infraestructura de largo plazo, no algo que el volumen de datos actual necesite ya —
  construirlo antes de tener una idea real de cuántas solicitudes/año se generan es
  diseñar a ciegas. **Recomendación**: dejarlo como Etapa 8 (última, prioridad a
  revisar), y decidir el momento de construirlo cuando ya haya uno o dos años de datos
  reales de Empresas/Sedes para dimensionarlo bien, no antes.

## 5. Plan por etapas

### Etapa 1 — Dominio y persistencia

- [x] Crear entidad `Empresa` (`Domain/Entities/Empresa.cs`) — campos según §2.
  Hecho. `Archivos: src/CatalogoPedidos.Domain/Entities/Empresa.cs`.
- [x] Crear entidad `Sede` (`Domain/Entities/Sede.cs`) — campos según §2, incluyendo
  `Pais`/`Ciudad`/`Direccion`.
  Hecho. `Archivos: src/CatalogoPedidos.Domain/Entities/Sede.cs`.
- [x] Agregar `SedeId` (int?, FK) a `ApplicationUser`.
  Hecho. `Archivos: src/CatalogoPedidos.Infrastructure/Identity/ApplicationUser.cs`.
- [x] Agregar `SedeId`/`SedeNombre`/`EmpresaId`/`EmpresaNombre` (todos opcionales) a
  `Solicitud`.
  Hecho. `Archivos: src/CatalogoPedidos.Domain/Entities/Solicitud.cs`.
- [x] Configurar `AppDbContext` (Fluent API): FK `Sede.EmpresaId` → `Empresa.Id`
  (`OnDelete: Restrict`); FK `ApplicationUser.SedeId` → `Sede.Id` (`OnDelete: Restrict`,
  no se puede desactivar/borrar una sede con usuarios asociados sin antes reasignarlos);
  FKs `Solicitud.SedeId`/`EmpresaId` → `Sede.Id`/`Empresa.Id` (`OnDelete: SetNull`, son
  snapshot, no deben bloquear nada).
  Hecho. `Archivos: src/CatalogoPedidos.Infrastructure/Persistence/AppDbContext.cs`.
  Verificado con `dotnet build src/CatalogoPedidos.slnx` (0 errores/advertencias).
- [x] Migración aditiva (`dotnet ef migrations add AgregaEmpresasYSedes ...`), verificar
  que el script generado sea 100% `ADD TABLE`/`ADD COLUMN` sin tocar datos existentes.
  Hecho. `Archivos:
  src/CatalogoPedidos.Infrastructure/Persistence/Migrations/20260923172125_AgregaEmpresasYSedes*.cs`.
  Verificado leyendo el script generado: solo `CreateTable` (`Empresas`, `Sedes`) y
  `AddColumn` nullable en `Pedidos`/`AspNetUsers` — ningún `ALTER`/`DROP` sobre columnas
  existentes. **Falta aplicarla contra la base real** (`dotnet ef database update`) — no
  se corrió todavía, queda pendiente de tu confirmación antes de tocar la BD (ver
  metodología acordada).
- [x] Seed de desarrollo: 1 `Empresa` con 2 `Sede` de prueba + asociar cuentas de prueba.
  Hecho. `Archivos: src/CatalogoPedidos.Web/Seed.cs` — siembra "Auropaq Colombia" con
  "Sede Norte" (Medellín) y "Sede Sur" (Bogotá), asocia `usuario@catalogo.local` a Sede
  Norte. Se ejecutará la próxima vez que arranque la app en Development, después de
  aplicar la migración.

Verificación conjunta de la etapa: `dotnet build src/CatalogoPedidos.slnx` (0
errores/advertencias) y `dotnet test src/CatalogoPedidos.slnx` (8/8 tests existentes
siguen pasando, ninguno tocado en esta etapa porque todavía no hay lógica de Application).

### Etapa 2 — Application

- [x] `CrearEmpresaDto` (espejo de los DTOs de `Proveedor` — no hizo falta un DTO de
  salida separado: `IEmpresaService` devuelve la entidad `Empresa` directamente, mismo
  criterio que ya usa `IProveedorService` con `Proveedor`).
  Hecho. `Archivos: src/CatalogoPedidos.Application/Empresas/CrearEmpresaDto.cs`.
- [x] `CrearSedeDto` (incluye `EmpresaId`, `Pais`, `Ciudad`, `Direccion`; mismo criterio
  de DTO único que `Empresa` — `ISedeService` devuelve `Sede` directamente).
  Hecho. `Archivos: src/CatalogoPedidos.Application/Sedes/CrearSedeDto.cs`.
- [x] `IEmpresaService`/`EmpresaService` y `ISedeService`/`SedeService`:
  `ObtenerTodasAsync`, `CrearAsync`, `ActualizarAsync`, `DesactivarAsync` — mismo patrón
  que `IProveedorService`/`ProveedorService` (repositorio con `IDbContextFactory`,
  validación de campos obligatorios y de formato de email igual que `ProveedorService`).
  `SedeService.ObtenerTodasAsync` acepta filtro opcional por `EmpresaId`.
  Hecho. `Archivos:
  src/CatalogoPedidos.Application/Empresas/{IEmpresaRepository,IEmpresaService,EmpresaService}.cs,
  src/CatalogoPedidos.Application/Sedes/{ISedeRepository,ISedeService,SedeService}.cs,
  src/CatalogoPedidos.Infrastructure/Repositories/{EmpresaRepository,SedeRepository}.cs`.
- [x] Extender `UsuarioRolDto` con `SedeId`/`SedeNombre`/`EmpresaNombre`, y agregar
  `AsociarSedeAsync(usuarioId, sedeId?)` al servicio de gestión de usuarios existente
  (mismo servicio que ya maneja `AsignarGestor`/`QuitarGestor`, para tener un solo lugar
  de administración de atributos de usuario en vez de duplicar pantallas).
  Hecho. `Archivos: src/CatalogoPedidos.Application/Usuarios/UsuarioRolDto.cs,
  src/CatalogoPedidos.Application/Usuarios/IUsuarioRolService.cs,
  src/CatalogoPedidos.Infrastructure/Identity/UsuarioRolService.cs`.
  `ObtenerUsuariosAsync` resuelve las sedes de todos los usuarios en un solo viaje
  (`ISedeRepository.ObtenerPorIdsAsync`, sin filtrar por `Activo` para no perder el
  nombre si la sede se desactiva después) — evita N+1.
- [x] Al crear una `Solicitud`, autocompletar `SedeId`/`SedeNombre`/`EmpresaId`/
  `EmpresaNombre` desde el `ApplicationUser` del solicitante — mismo punto donde ya se
  autocompleta `DireccionEntrega`.
  Hecho, con una diferencia deliberada respecto al plan original: en vez de que el
  snapshot dependa de `ApplicationUser` directamente (Application no puede depender de
  Infrastructure/Identity), se agregó la abstracción `IUsuarioSedeDirectory`
  (`Application/Usuarios`), implementada en Infrastructure como `UsuarioSedeDirectory` —
  mismo patrón ya usado por `IGestorDirectory`/`GestorDirectory` para el mismo problema
  (notificar gestores sin que Application conozca Identity). A diferencia de
  `DireccionEntrega` (editable por el usuario en el carrito), Sede/Empresa **no** es un
  parámetro que el caller pase — se resuelve siempre server-side desde la asociación real
  del usuario, para que no se pueda "elegir" una empresa distinta a la asignada.
  `Archivos: src/CatalogoPedidos.Application/Usuarios/{IUsuarioSedeDirectory,UsuarioSedeDto}.cs,
  src/CatalogoPedidos.Application/Solicitudes/SolicitudService.cs,
  src/CatalogoPedidos.Infrastructure/Identity/UsuarioSedeDirectory.cs,
  src/CatalogoPedidos.Infrastructure/DependencyInjection.cs`.
  Verificado con 2 tests nuevos en
  `CatalogoPedidos.Application.Tests/Solicitudes/SolicitudServiceTests.cs`
  (`SolicitanteConSedeAsignada_CopiaSedeYEmpresaComoSnapshot` y
  `SolicitanteSinSedeAsignada_NoBloqueaYDejaCamposEnNull`).

Verificación conjunta de la etapa: `dotnet build src/CatalogoPedidos.slnx` (0
errores/advertencias), `dotnet test src/CatalogoPedidos.slnx` (10/10 — 8 existentes + 2
nuevos), y un arranque de prueba (`dotnet run`) para confirmar que toda la inyección de
dependencias nueva resuelve en runtime sin errores.

### Etapa 3 — UI: administración de Empresas

- [x] `Empresas.razor` (`Roles.Gestor`) — tabla crear/editar/desactivar, calcada de
  `Proveedores.razor`. Entrada de menú nueva.
  Hecho. `Archivos: src/CatalogoPedidos.Web/Components/Pages/Empresas/Empresas.razor,
  src/CatalogoPedidos.Web/Components/Layout/Sidebar.razor,
  src/CatalogoPedidos.Web/Components/_Imports.razor`. Incluye un link "Ver sedes" por
  fila hacia `/sedes?empresaId=X` — esa ruta todavía no existe, la crea la Etapa 4 (hasta
  entonces da 404 si se hace clic, no rompe nada del resto de la página).
  Verificado con `dotnet build` (0 errores/advertencias), `dotnet test` (10/10, sin
  regresiones) y un arranque de prueba: `curl /empresas` sin sesión redirige a
  `Iniciar sesión` (confirma que el `[Authorize(Roles = Roles.Gestor)]` y el routing
  funcionan, sin excepciones en el log del servidor). **No se probó el flujo completo
  crear/editar/desactivar con clics reales en el navegador** — recomiendo hacer esa
  pasada de UI recién cuando también esté la Etapa 4 (Sedes), para probar de una vez el
  flujo completo Empresa→Sede en vez de dos pasadas de navegador separadas.

### Etapa 4 — UI: administración de Sedes

- [x] `Sedes.razor` (`Roles.Gestor`) — tabla crear/editar/desactivar con los campos de
  geolocalización (país, ciudad, dirección); filtro/selector de Empresa dueña.
  Posiblemente accesible también desde el detalle de cada Empresa en `Empresas.razor`
  ("Ver sedes de esta empresa").
  Hecho. `Archivos: src/CatalogoPedidos.Web/Components/Pages/Empresas/Sedes.razor,
  src/CatalogoPedidos.Web/Components/Layout/Sidebar.razor,
  src/CatalogoPedidos.Web/Components/UI/Icon.razor` (ícono nuevo `map-pin`, no existía en
  la librería de íconos). Filtro por Empresa arriba de la tabla (selector `<select>`,
  mismo patrón que los filtros de `Administrar.razor`) que también lee `?empresaId=` de
  la URL vía `[SupplyParameterFromQuery]` — así el link "Ver sedes" que quedó pendiente
  de la Etapa 3 en `Empresas.razor` ya funciona y llega con el filtro preaplicado.
  Verificado con `dotnet build`/`dotnet test` (0 errores, 10/10 sin regresiones) y
  smoke test no interactivo (`curl` a `/sedes`, `/sedes?empresaId=1` y `/empresas`, los
  tres HTTP 200 sin excepciones en el log del servidor).
  **El clic-testing real (crear/editar/desactivar sedes, filtro por empresa, el link
  "Ver sedes" desde Empresas) lo hace el usuario directamente en su navegador** — no lo
  hago yo por costo de tokens (ver memoria `feedback-no-browser-testing`). Instrucciones
  de qué probar abajo, en el mensaje de cierre de esta etapa.

### Etapa 5 — UI: asociar usuarios a Sede

- [ ] Extender `GestionRoles.razor` (o la pantalla de gestión de usuarios que corresponda)
  con un selector de Empresa → Sede por usuario, reusando el patrón try/catch + mensaje +
  recarga ya usado para `AsignarGestor`/`QuitarGestor`.

### Etapa 6 — Dashboard de solicitudes/consumos

- [ ] Servicio de reporte: filtro combinable (Empresa opcional + Sede opcional + Año/Mes
  opcional + rango de fechas Desde/Hasta opcional) sobre `Solicitud` + `DetalleSolicitud`,
  devolviendo el detalle descrito en §2 (solicitante, fecha, insumo, categoría, código
  interno, código de proveedor) — mismo criterio de combinación de filtros que ya usa
  `Administrar.razor`.
- [ ] Página de dashboard (nombre a definir) — selectores de Empresa, Sede, Año, Mes y
  rango de fechas, tabla de detalle agrupada de forma clara (p. ej. por empresa, luego
  por sede, luego por mes), pensada para que el Gestor navegue el consumo de forma
  organizada e intuitiva, no una tabla plana sin agrupar.
- [ ] Exportación a Excel del corte activo en pantalla (la combinación de filtros vigente),
  reusando `IExcelExportService` — no un solo reporte fijo, sino "lo que se está viendo,
  a Excel".
- [ ] Evaluar en este mismo punto si conviene resolver junto la Tarea 1 de
  `PLAN_FUNCIONALIDADES_NEGOCIO.md` (reporte de gasto por proveedor/categoría/período)
  sobre la misma infraestructura de filtro/agregación, en vez de construirla dos veces.

### Etapa 7 — Reconciliar con el plan de funcionalidades de negocio existente

- [ ] Marcar la Tarea 2 (`CentroCosto`) de `PLAN_FUNCIONALIDADES_NEGOCIO.md` como
  superada por `Sede` (o ajustar según lo que se decida en §4) para no dejar dos planes
  con propuestas contradictorias.

### Etapa 8 — Archivado histórico anual (prioridad a revisar, ver §4)

- [ ] Diseñar y crear tablas de archivo (`SolicitudArchivada`, `DetalleSolicitudArchivada`,
  y las tablas relacionadas que corresponda) con la misma forma que las originales.
- [ ] Acción administrativa ("Archivar año X") que mueva las solicitudes cerradas de un
  año terminado a las tablas de archivo y las retire de las tablas activas, de forma
  transaccional (todo o nada).
- [ ] Adaptar el dashboard (Etapa 6) para que, al filtrar por un año ya archivado, consulte
  transparentemente las tablas de archivo en vez de las activas.

---

*No se ha escrito código todavía — este documento es el análisis + plan pedido. La
implementación empieza en una conversación/aprobación aparte, etapa por etapa, igual que
el resto de los planes de este repo.*
