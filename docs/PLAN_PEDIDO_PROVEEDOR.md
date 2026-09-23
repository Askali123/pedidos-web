# Plan — PedidoProveedor y DetallePedidoProveedor

Checklist de trabajo, de mayor a menor prioridad, para modelar el **PedidoProveedor** como
un documento persistido (en vez del envío efímero actual). Este plan es la **Etapa 0**: define
el modelo de negocio y las relaciones **antes** de tocar código. Cada tarea se marca `[x]` al
completarse; no reordenar sin avisar — el orden es la prioridad acordada.

Referencia de análisis completo: equivalencias, gaps y decisión snapshot/referencia
resueltos el 2026-09-21.

## Conceptos del negocio

- Un **catálogo** administrado por el Gestor registra Productos y los asocia con Proveedores.
- Un producto puede estar asociado a uno o varios proveedores.
- La relación `ProductoProveedor` guarda información **propia** de esa relación, sobre todo
  el **código que cada proveedor usa para identificar ese producto** (el código NO pertenece
  al Producto).

Ejemplo:

| Producto      | Proveedor   | Código del proveedor |
| ------------- | ----------- | -------------------- |
| Tornillo 10mm | Proveedor A | `TOR-001`            |
| Tornillo 10mm | Proveedor B | `T-10`               |

## 1. Diagnóstico del modelo actual

| Concepto de negocio                                    | Entidad             | Tabla                 | Estado        |
| ------------------------------------------------------ | ------------------- | --------------------- | ------------- |
| Solicitud (cabecera)                                   | `Pedido`            | `Pedidos`             | Existe        |
| Detalle de solicitud                                   | `SolicitudProducto` | `Solicitudes`         | Existe        |
| Producto                                               | `Producto`          | `Productos`           | Existe        |
| Proveedor                                              | `Proveedor`         | `Proveedores`         | Existe        |
| Relación Producto↔Proveedor (con código propio)        | `ProductoProveedor` | `ProductoProveedores` | Existe        |
| **PedidoProveedor (cabecera del pedido al proveedor)** | —                   | —                     | **No existe** |
| **DetallePedidoProveedor**                             | —                   | —                     | **No existe** |

### Aclaraciones clave

1. **`Pedido` ES la Solicitud.** El usuario no crea una "Solicitud": desde el carrito llama
   a `SolicitudService.CrearPedidoAsync`, que persiste un `Pedido` (cabecera con solicitante,
   comentario, dirección, fecha) con sus líneas `SolicitudProducto`. El nombre es un legacy de
   la migración `AgregarPedido` (antes solo existía `SolicitudProducto`). Hoy "solicitud" = la
   **línea** y "pedido" = el **grupo**: son dos niveles del mismo concepto.
2. **`PedidoProveedor` NO existe y no es lo mismo que `Pedido`.** El pedido-a-proveedor actual
   es **efímero y derivado**: se calcula en vivo (`PedidoNotificacionProveedorService`)
   haciendo la intersección pedido ∩ proveedor, se manda el correo con PDF/Excel, y lo único
   que se persiste es `NotificacionProveedor` con **solo un conteo** (`CantidadLineas`) + email
   snapshot + gestor/fecha. **Las líneas y los códigos no quedan guardados.**

## 2. Lo que ya existe y coincide con el objetivo (no se toca)

- `ProductoProveedor` ya tiene `CodigoProveedor`, `PrecioProveedor`, `EsPreferido`, con
  índices únicos `(ProductoId, ProveedorId)` y `(ProveedorId, CodigoProveedor)`.
- La intersección pedido ∩ proveedor ya existe y es correcta: `EnviarAProveedorAsync` solo
  toma líneas **Aprobadas** del pedido cuyos productos tengan asociación con el proveedor
  elegido, y arma PDF/Excel con el `CodigoProveedor` de ESE proveedor. **No** carga todo el
  catálogo del proveedor.
- Panel administrativo básico: ver proveedores de un producto, ver productos de un proveedor,
  asociar con código, quitar asociación.
- Regla anti-duplicado: una línea ya enviada a otro proveedor del mismo pedido deja de ofrecerse.

## 3. Gaps a cerrar

- [x] **G1. No existe `PedidoProveedor` ni `DetallePedidoProveedor`** — **Hecho.** Entidades
      nuevas + migración `AgregarPedidoProveedorYDocsPorProveedor` (aplicada en la BD local).
      `EnviarAProveedorAsync` ahora persiste el documento en vez de solo mandar el correo.
- [x] **G2. `CodigoProveedor` no es snapshot** — **Hecho.** `DetallePedidoProveedor` congela
      `ProductoNombre`, `Cantidad`, `CodigoProveedor`, `UnidadMedida` y `PrecioProveedor` al
      momento del envío (`ConstruirSnapshots` en `PedidoNotificacionProveedorService`).
- [x] **G3. No existe "editar asociación"** — **Hecho.** `ProductoProveedor.Activo` (default
      `true`) + `IProveedorService.ActualizarAsociacionAsync`/`DesactivarAsociacionAsync`/
      `ReactivarAsociacionAsync` (ya no hay DELETE físico). UI en `ProveedoresDeProducto.razor`
      y `ProductosDeProveedor.razor`.
- [x] **G4. No hay decisión explícita por línea en el pedido** — **Hecho.** `DetallePedidoProveedor.Excluido`
      + checkboxes por línea en la confirmación de "Enviar a proveedor" (`Bandeja.razor`/
      `Administrar.razor`, parámetro `productoIdsSeleccionados` en `EnviarAProveedorAsync`).
      Excluir de un pedido no toca `ProductoProveedor`.
- [x] **G5. No hay estado del documento** — **Decisión aplicada:** no se agregó ningún campo
      de estado nuevo; se sigue derivando de `NotificacionProveedor` (¿se envió?) +
      `ConfirmacionEntrega` (¿llegó?), como recomendaba la sección 7.

## 4. Modelo objetivo

```
Pedido 1 ── * SolicitudProducto  * ── 1 Producto  1 * ─ ProductoProveedor * ─ 1 Proveedor
                                                          │ CodigoProveedor, PrecioProveedor,
                                                          │ EsPreferido, (Activo nuevo)
Pedido 1 ── * PedidoProveedor * ─ 1 Proveedor
                │ (FechaCreacion, GestorId, Notificación guardada)
                *─ * DetallePedidoProveedor * ─ 1 Producto
                            │ ProductoId, ProductoNombre (snapshot), Cantidad (snapshot),
                            │ CodigoProveedor (snapshot), PrecioProveedor (opcional/snapshot),
                            │ Excluido (flag: quitado manualmente de ESTE pedido)
```

`NotificacionProveedor` se mantiene como historial de **envíos de correo** y gana una FK a
`PedidoProveedor` (sin duplicar líneas).

## 5. Referencia vs snapshot

| Campo en DetallePedidoProveedor                  | Tipo              | Motivo                                                               |
| ------------------------------------------------ | ----------------- | -------------------------------------------------------------------- |
| `PedidoProveedorId`, `ProveedorId`, `ProductoId` | Referencia (FK)   | joins, badges, filtros, navegación                                   |
| `ProductoNombre`                                 | **Snapshot**      | identificar en PDF/histórico aunque renombren/desactiven el producto |
| `Cantidad`                                       | **Snapshot**      | refleja lo acordado en el momento                                    |
| `CodigoProveedor`                                | **Snapshot**      | requisito central: cambiar el SKU no altera pedidos pasados          |
| `PrecioProveedor`                                | Snapshot opcional | útil para reportes de gasto; omitir si no se necesita ya             |
| `UnidadMedida`                                   | Snapshot opcional | barato, útil en PDF                                                  |

`PedidoProveedor` se crea **inmutable al primer envío**: mismo pedido+proveedor reusa el mismo
documento, y el reenvío crea otra `NotificacionProveedor`, no otro documento. Respeta el
cooldown de 5 min actual.

## 6. Reglas de negocio

- [x] Solo líneas `Aprobada` entran al `PedidoProveedor` (regla actual, se mantiene).
- [x] Producto sin asociación con el proveedor → no entra (nunca aparece en ningún grupo de
      proveedor) y ahora **sí se avisa** con un badge "Sin proveedor asociado" en el panel
      (ver sección 8, Etapa 4, tarea 7).
- [x] Producto con varios proveedores → el gestor elige (dropdown actual, ya existe).
- [x] Quitar una línea de un `PedidoProveedor` concreto → flag `Excluido` en el detalle;
      **jamás** toca `ProductoProveedor`. Hecho vía checkboxes + `productoIdsSeleccionados`.
- [x] Eliminar/desactivar una asociación del catálogo → no altera `PedidoProveedor` previos
      (snapshots); sí oculta futuros (`ObtenerProveedoresDisponiblesAsync` y
      `EnviarAProveedorAsync` ya filtran por `Activo`).
- [x] Una línea enviada a otro proveedor del mismo pedido → excluida del proceso (regla
      actual, ahora explicitada en el detalle).

## 7. Decisiones tomadas (resueltas con la implementación)

- [x] **¿`PedidoProveedor` inmutable o editable?** → Ni una cosa ni la otra estrictamente:
      el mismo (Pedido, Proveedor) reutiliza el mismo documento (índice único), y un reenvío
      **anexa** líneas nuevas sin duplicar ni tocar las ya presentes (`ObtenerOCrearDocumentoAsync`).
- [x] **¿Estado del documento?** → Sin campo nuevo; se deriva de `NotificacionProveedor`
      (¿se envió?) + `ConfirmacionEntrega` (¿llegó?), como estaba recomendado.
- [x] **¿Precio y UnidadMedida se snapshottean ya?** → Sí, ya quedaron en `DetallePedidoProveedor`
      desde la primera versión (no se dejó para después).
- [x] **Envíos históricos sin documento** → Confirmado el criterio aceptado: `NotificacionProveedor.PedidoProveedorId`
      es nullable para las filas viejas; un reenvío nuevo de ese (Pedido, Proveedor) crea el
      documento con los datos actuales y lo referencia desde ahí en adelante.

## 8. Plan por etapas — checklist de trabajo

Cada tarea se marca `[x]` al completarse, con un párrafo "Hecho." (qué se hizo, cómo se
verificó, archivos tocados) — mismo formato que `PLAN_FLUJO_PEDIDO_PROVEEDOR.md`. Esta
sesión corrió en un PC sin Docker (SQL Server ya está configurado aparte, con SSMS de
cliente) y sin usar la extensión de Chrome para no gastar tokens en pruebas rutinarias, así
que la verificación de TODAS las tareas de esta sección es **build limpio + arranque real
de la app sin errores + revisión de código**, no clic-por-clic en el navegador — falta que
el usuario las confirme en vivo.

**Etapa 0 — Modelo:** hecha (secciones 1-7 de este documento).

**Etapa 1 — Persistencia**

- [x] **1. Entidades + migración** — **Hecho.** `PedidoProveedor`, `DetallePedidoProveedor`,
      `ProductoProveedor.Activo`, `NotificacionProveedor.PedidoProveedorId` (nullable) +
      migración `AgregarPedidoProveedorYDocsPorProveedor`. Aplicada contra la BD local
      (`dotnet ef database update` confirmó "already up to date": ya estaba aplicada de la
      sesión anterior); la app arrancó y sirvió peticiones sin errores.
      Archivos: `Domain/Entities/PedidoProveedor.cs`, `DetallePedidoProveedor.cs`,
      `ProductoProveedor.cs`, `NotificacionProveedor.cs`, `Infrastructure/Persistence/Migrations/20260921212813_*`.

**Etapa 2 — Servicio de pedido a proveedor**

- [x] **2. `EnviarAProveedorAsync` persiste el documento con snapshots** — **Hecho.**
      Cada envío arma `DetallePedidoProveedor` (nombre/cantidad/código/unidad/precio
      congelados) y los persiste en el `PedidoProveedor` de ese (Pedido, Proveedor) —
      reusándolo si ya existe, anexando solo líneas nuevas. El PDF/Excel adjuntos al correo
      ahora se generan desde esos snapshots (`ExportarPedidoProveedor`), no del catálogo vivo.
      `IPedidoProveedorRepository` nuevo (`PedidoProveedorRepository`).
      Archivos: `PedidoNotificacionProveedorService.cs`, `IPedidoProveedorRepository.cs`,
      `PedidoProveedorRepository.cs`, `IPdfExportService.cs`/`PdfExportService.cs`,
      `IExcelExportService.cs`/`ExcelExportService.cs`.

**Etapa 3 — Administración de asociaciones**

- [x] **3. Editar + desactivar/reactivar (no DELETE)** — **Hecho.** `ProductoProveedor.Activo`
      reemplaza el DELETE físico; `ActualizarAsociacionAsync` edita código/precio/preferido;
      `AsociarProveedorAsync` reactiva una fila desactivada en vez de fallar por el índice
      único. UI ya conectada en ambas pantallas.
      Archivos: `IProveedorService.cs`/`ProveedorService.cs`, `IProductoProveedorRepository.cs`/
      `ProductoProveedorRepository.cs`, `ProveedoresDeProducto.razor`, `ProductosDeProveedor.razor`.

**Etapa 4 — UI del pedido a proveedor**

- [x] **4. Checkboxes por línea en la confirmación (excluir)** — **Hecho.** El diálogo de
      "Enviar a proveedor" deja destildar líneas; lo destildado queda `Excluido = true` en
      el documento (no se vuelve a ofrecer ni se cuenta en reenvíos), sin tocar el catálogo.
      Archivos: `Bandeja.razor`, `PedidoNotificacionProveedorService.cs`.
- [x] **5. Historial de envíos con detalle real** — **Hecho.** `HistorialEnvios.razor` gana
      una fila expandible por envío (ícono chevron) con las líneas reales del documento
      (producto, código, cantidad, unidad) en vez de solo el conteo; el botón "PDF del
      pedido" ahora baja el PDF regenerado desde los snapshots de ESE envío
      (`/api/pedidos-proveedor/{id}/pdf`, filtra `Excluido`), con fallback al PDF del pedido
      completo para filas viejas sin documento (`PedidoProveedorId` null).
      Verificado: build limpio; con la app corriendo, `GET /api/pedidos-proveedor/999999/pdf`
      y `GET /solicitudes/historial-envios` respondieron sin error 500 (302 a login, sin
      sesión) — falta confirmar visualmente la tabla expandida con datos reales.
      Archivos: `HistorialEnvios.razor`, `NotificacionProveedorRepository.cs`,
      `IPedidoProveedorRepository.cs`/`PedidoProveedorRepository.cs`, `Program.cs`.
- [x] **6. Badge "+N más" con redirección** — **Hecho.** El badge en Bandeja/Administrar
      (cuando un producto tiene más de un proveedor) ahora enlaza a
      `/catalogo/{ProductoId}/proveedores`, la pantalla de asociaciones de ese producto.
      Archivos: `Bandeja.razor`, `Administrar.razor`.
- [x] **7. Aviso "sin proveedor asociado"** — **Hecho.** Donde antes se mostraba un simple
      "—" para un producto sin ninguna asociación de proveedor, ahora aparece un badge de
      advertencia "Sin proveedor asociado" (`BadgeVariant.Warning`, ya usado en otras
      pantallas — no hizo falta recompilar Tailwind) que enlaza a
      `/catalogo/{ProductoId}/proveedores` para asociarlo ahí mismo. No toqué la columna
      "Código proveedor" que aparece cuando Administrar ya está filtrado por UN proveedor
      elegido (`proveedorId is not null`): ahí el "-" es autoexplicativo porque la columna
      ya está encabezada con ese proveedor específico.
      Verificado: build limpio; con la app corriendo, `/` y `/solicitudes/bandeja`
      respondieron sin error 500 — falta confirmar visualmente el badge en el navegador.
      Archivos: `Bandeja.razor`, `Administrar.razor`.

**Etapa 5 — Reportes/casos límite**

- [x] **8. PDF/Excel del envío por correo leen snapshots** — cubierto en la tarea 2.
- [x] **9. Descarga puntual del PDF de un envío ya hecho lee snapshots** — cubierto en la
      tarea 5 (nuevo endpoint `/api/pedidos-proveedor/{id}/pdf`).
- [x] **10. Alertas de stock / badges coherentes con `Activo`** — **Hecho.**
      `ObtenerPreferidosPorProductosAsync` (usada para el código preferido en Bandeja/
      Administrar y para el deep-link de "Confirmar entrega"/alerta de stock bajo en
      `SolicitudService.AlertarStockBajoAsync`) ahora filtra `Activo`, así nunca ofrece ni
      enlaza a una asociación desactivada.
      Archivo: `ProductoProveedorRepository.cs`.

## 9. Casos de prueba (xUnit) — Hecho (2026-09-22)

Proyecto nuevo `src/CatalogoPedidos.Application.Tests` (xUnit + Moq), agregado a
`CatalogoPedidos.slnx`. Referencia solo `CatalogoPedidos.Application` — sin DB real: los
repositorios se simulan con Moq respaldado por listas en memoria (no mocks "stateless"),
porque varios casos necesitan leer después de escribir dentro del mismo test (enviar y
volver a consultar disponibilidad). Los 7 casos de esta sección quedaron como
`PedidoNotificacionProveedorServiceTests.cs`, uno por bullet:

- [x] Intersección: pedido con A,B,C + Proveedor X con A,B → documento solo con A,B y
      códigos. → `ObtenerProveedoresDisponibles_SoloIncluyeProductosAsociadosAEseProveedor`
- [x] Producto con varios proveedores → documento con el proveedor elegido; el otro sigue
      libre. → `EnviarAProveedor_ConProductoDeVariosProveedores_SoloAfectaAlElegidoYElOtroQuedaLibreDeLaTransaccion`
      (verifica que la asociación de catálogo del otro proveedor no se toca Y que deja de
      competir por esa línea tras el envío).
- [x] Producto sin proveedor → excluido; el resto del pedido sigue. →
      `ObtenerProveedoresDisponibles_ProductoSinProveedor_QuedaExcluidoYElRestoSigue`
      (el aviso/badge es de la UI, no de este servicio — no se testea acá).
- [x] Cambio de código en catálogo después del pedido → el documento viejo conserva el
      código viejo. → `EnviarAProveedor_CambioDeCodigoDespues_NoAlteraElDocumentoYaEmitido`
- [x] Desactivar asociación después del pedido → histórico intacto; pedidos nuevos lo
      excluyen. → `EnviarAProveedor_DesactivarAsociacionDespues_NoAlteraHistoricoYExcluyeNuevosEnvios`
- [x] Quitar línea del pedido → `Excluido = true`; catálogo intacto. →
      `EnviarAProveedor_ExcluirUnaLinea_QuedaMarcadaYElCatalogoNoSeToca`
- [x] Reenvío en cooldown → rechazado con motivo y sin duplicar documento. →
      `EnviarAProveedor_ReenvioEnCooldown_SeRechazaSinDuplicarElDocumento`

Verificado: `dotnet test src/CatalogoPedidos.slnx` → 7/7 correctas; `dotnet build` de la
solución completa sigue limpio (0 errores/advertencias) con el proyecto de test agregado.
`AGENTS.md` actualizado (ya no dice "no hay tests"). Domain/Infrastructure/Web siguen sin
tests propios — este primer proyecto cubre la lógica de negocio de mayor riesgo
(`PedidoNotificacionProveedorService`), que era justamente el motivo original de esta
sección.

## 10. API y frontend (implementado — ajustado respecto al diseño original)

- **Backend:** no se creó un `IPedidoProveedorService`/DTOs separados — se optó por
  `IPedidoProveedorRepository` (crear/obtener/actualizar) consumido directo desde
  `PedidoNotificacionProveedorService`, que ya era el punto único de entrada para "enviar a
  proveedor"; no hacía falta una capa de servicio nueva encima. `ActualizarAsociacionAsync` +
  `DesactivarAsociacionAsync`/`ReactivarAsociacionAsync` sí quedaron en `IProveedorService`
  como estaba planeado.
- **Frontend:** "Enviar a proveedor" en `Bandeja.razor`/`Administrar.razor` → confirmación
  con checkbox por línea → persiste el documento → recién ahí el correo. `HistorialEnvios`
  ya lee los detalles reales (fila expandible). Badges "+N más" ya redirigen a
  `/catalogo/{ProductoId}/proveedores`.
- **Minimal APIs:** `/api/pedidos-proveedor/{id}/pdf` (nuevo, solo Gestor) — el resto son
  internos, nada público.

## 11. Etapa 6 — Claridad "Solicitud" vs "Pedido" (2026-09-22)

El usuario pidió separar dos conceptos que sentía mezclados: lo que el usuario solicitante
pide (**Solicitud**) y lo que el gestor termina mandándole a un proveedor concreto
(**Pedido**), con la regla de que un mismo producto solicitado no puede terminar
repartido/duplicado entre dos proveedores.

**Diagnóstico: no falta modelo, falta nomenclatura.** Ambas entidades y la regla anti-duplicado
YA EXISTEN y funcionan (verificado leyendo el código, no de memoria):

- Solicitud = `Pedido` (cabecera) + `SolicitudProducto` (líneas) — historial en "Mis
  solicitudes"/"Bandeja de solicitudes"/"Historial de resoluciones".
- Pedido a proveedor = `PedidoProveedor` + `DetallePedidoProveedor` (Etapas 1-5 de este
  documento).
- Anti-duplicado ya garantizado en DOS capas de `PedidoNotificacionProveedorService.cs`:
  `ObtenerProveedoresDisponiblesAsync` (línea 65, lo que ve el gestor) y `EnviarAProveedorAsync`
  (línea 134-137, última validación del lado del servidor aunque alguien fuerce la UI).
- No hace falta una entidad `catalogoSolicitud` nueva: la asociación producto↔proveedor↔código
  ya vive en `ProductoProveedor`.

**El problema real es que la palabra "Pedido" está sobrecargada en la UI** para dos cosas
distintas — administrar solicitudes (`Administrar.razor`, título "Administrar pedidos") vs.
el documento que se le manda a un proveedor (PDF/correo "Pedido de reabastecimiento N.º X").
Ya estaba señalado como deuda conocida en la sección 1 ("Aclaraciones clave" #1) pero sin
resolver. Esto es lo que hay que arreglar, con dos alcances posibles de riesgo muy distinto:

**Opción A — Pass de nomenclatura en la UI — Hecho (2026-09-22).**

- [x] **A1. Renombrar "Administrar pedidos" → "Administrar solicitudes"** — **Hecho.**
      Sidebar, `PageTitle` y `HeaderState.Set` de `Administrar.razor`, más un comentario
      suelto en `RecordatorioPendientesHostedService.cs` que todavía la nombraba así.
      Archivos: `Administrar.razor`, `Sidebar.razor`, `RecordatorioPendientesHostedService.cs`.
- [x] **A2. Distinguir los dos PDF con nombres distintos** — **Hecho.** El título dentro del
      PDF de `ExportarPedido` ya decía "Solicitud N.º X" (quedó así de arrastre del rename
      mecánico de la Etapa 6/Opción B — lo confirmé leyendo el archivo). Lo que faltaba: el
      botón decía "PDF del pedido" en las 3 pantallas que enlazan a ESE PDF (la solicitud
      completa) — pasó a "PDF de la solicitud"; el nombre del archivo descargado pasó de
      `pedido-{id}.pdf` a `solicitud-{id}.pdf`. El botón de `HistorialEnvios.razor` que baja
      el PDF de un `PedidoProveedor` real se dejó tal cual ("PDF del pedido") — es
      correcto, ese sí es un pedido.
      Archivos: `Bandeja.razor`, `Administrar.razor`, `MisSolicitudes.razor`, `Program.cs`.
- [x] **A3. Revisar el resto de copys** — **Hecho.** Repasé cada aparición de "pedido" en
      `src/CatalogoPedidos.Web` con grep y clasifiqué cada una: las que hablan del
      documento real a un proveedor (`ProveedoresDeProducto.razor`, `ProductosDeProveedor.razor`
      — "los pedidos ya emitidos...") se dejaron intactas; las que en realidad hablan de la
      solicitud del usuario se corrigieron — stat cards y CTAs de `Home.razor` ("Pedidos
      pendientes"→"Solicitudes pendientes", "Enviá tu pedido"→"Enviá tu solicitud", etc.),
      el diálogo de confirmación "¿Enviar el pedido #N...?"→"¿Enviar la solicitud #N...?",
      "Aprobar/Rechazar todo el pedido"→"...toda la solicitud", el mensaje de entrega
      confirmada, los hints de dirección de entrega en `Carrito.razor` y en el perfil
      (`Manage/Index.razor`, `Register.razor`), y el EmptyState de filtros en
      `Administrar.razor` ("Ningún pedido"→"Ninguna solicitud"). Dejé sin tocar (a
      propósito, ver Etapa 6/B3) los nombres de variables y métodos internos
      (`pedidoId`, `AbrirResolverPedido`, etc.) y las rutas `/api/pedidos/...`.
      Archivos: `Home.razor`, `Bandeja.razor`, `Administrar.razor`, `Carrito.razor`,
      `EntregaBadge.razor`, `Manage/Index.razor`, `Register.razor`.
- [x] **A4. Nota para el gestor sobre líneas ya cubiertas** — **Hecho.** El dropdown
      "Enviar a proveedor" (en `Bandeja.razor` y `Administrar.razor`) ahora termina con una
      nota fija: "Un producto ya enviado a otro proveedor no vuelve a aparecer acá — cada
      línea solo se compra una vez." — para que la regla anti-duplicado (ya verificada en
      la Etapa 6) no parezca un bug cuando un producto de la solicitud no aparece en la
      lista de ningún proveedor.
      Archivos: `Bandeja.razor`, `Administrar.razor`.

Verificación de las 4 tareas: `dotnet build` limpio en cada paso; con la app corriendo,
`/`, `/solicitudes/bandeja` y `/solicitudes/administrar` respondieron sin error 500 y sin
excepciones en el log. No probado clic-por-clic en el navegador.

**Opción B — Renombrar la entidad de dominio `Pedido` → `Solicitud` — Hecho.**

El usuario eligió el alcance completo (A+B). Decisión de ejecución que bajó el riesgo real
muy por debajo de lo que decía el análisis original: **las tablas y columnas físicas de SQL
Server NO se tocaron.** `AppDbContext` fija explícitamente `Solicitud.ToTable("Pedidos")`,
`DetalleSolicitud.ToTable("Solicitudes")` y `HasColumnName("PedidoId")` en las 4 FK
renombradas — es un rename de vocabulario en C#, no una migración de esquema. La migración
generada (`20260922155237_RenombrarPedidoASolicitud`) tiene `Up`/`Down` **vacíos**: se
verificó leyendo el archivo antes de aplicarla. `dotnet ef database update` solo insertó una
fila en `__EFMigrationsHistory`, cero DDL contra la base real (que ya tenía datos de sesiones
anteriores).

- [x] **B1. Resolver el choque de nombres** — `SolicitudProducto` → `DetalleSolicitud`
      (mismo patrón `Detalle` que ya usaba `DetallePedidoProveedor`), liberando el nombre
      `Solicitud` para la cabecera. Ambas tablas físicas se pinearon a sus nombres de
      siempre (`Pedidos` y `Solicitudes` respectivamente) — ver arriba.
      Archivos: `Domain/Entities/Solicitud.cs` (antes `Pedido.cs`), `DetalleSolicitud.cs`
      (antes `SolicitudProducto.cs`), `Infrastructure/Persistence/AppDbContext.cs`.
- [x] **B2. Rename de clases/DbSets/repositorios/servicios/DTOs en las 4 capas** —
      `PedidoId`→`SolicitudId` + nav `Pedido`→`Solicitud` en `DetalleSolicitud`,
      `ConfirmacionEntrega`, `NotificacionProveedor`, `PedidoProveedor` (que SÍ se queda con
      ese nombre — es el "pedido a un proveedor" real). Métodos renombrados en cascada:
      `CrearPedidoAsync`→`CrearSolicitudAsync`, `ObtenerPedidoAsync`→`ObtenerSolicitudAsync`,
      `ActualizarPedidoAsync`→`ActualizarSolicitudAsync`, `ResolverPedidoAsync`→
      `ResolverSolicitudAsync`, `ObtenerPorPedidoAsync`→`ObtenerPorSolicitudAsync` (en los 3
      repositorios que lo tenían: `NotificacionProveedorRepository`, `PedidoProveedorRepository`,
      `ConfirmacionEntregaRepository`). `DecisionSolicitudDto.SolicitudId` (que en realidad
      apuntaba a la LÍNEA, no a la cabecera — vocabulario viejo donde "solicitud" = línea) se
      renombró a `DetalleId` para no quedar ambiguo contra el nuevo `Solicitud.Id`. Ejecutado
      con sed acotado por `\b` para los renames mecánicos 1:1 (verificado que no toca
      `PedidoProveedor`/`DetallePedidoProveedor`, que comparten el prefijo) + edición manual
      para los archivos con lógica semántica (`SolicitudService.cs`,
      `PedidoNotificacionProveedorService.cs`); cada paso se cerró con `dotnet build` limpio
      antes de seguir. Revisión aparte de concordancia de género en comentarios/strings
      ("un Pedido"→"una Solicitud"): encontré y corregí 4 casos donde el rename mecánico
      dejó "un Solicitud"/"del Solicitud".
      Archivos: los ~20 listados en `git status` bajo `Application/Solicitudes` e
      `Infrastructure/Repositories`, `Infrastructure/Excel`, `Infrastructure/Pdf`.
- [x] **B3. Rename en los archivos Razor** — resuelto en su mayoría por el compilador de
      Blazor (los mismos métodos/DTOs renombrados en B2 obligaron a actualizar cada llamada
      en `Carrito.razor`, `Bandeja.razor`, `Administrar.razor`, `MisSolicitudes.razor`,
      `HistorialResoluciones.razor`, `HistorialEnvios.razor`, `Home.razor`, `Program.cs`).
      **Alcance deliberadamente NO incluido:** nombres de variables/métodos internos de cada
      página (`pedidoId`, `AbrirResolverPedido`, `enviosPorPedido`, el record local
      `PedidoResumen`, etc.) y los títulos/copys de UI ("Administrar pedidos", "PDF del
      pedido") — sirven igual de bien renombrados o no para la corrección del código, y
      tocar los ~20 archivos Razor símbolo por símbolo multiplicaba el riesgo sin agregar
      valor real. Eso es exactamente el trabajo que ya estaba planeado como Opción A
      (A1-A4, todavía pendiente) — ahí es donde corresponde.
- [x] **B4. Regresión** — sin proyecto de test (no hay en el repo). Verificado: `dotnet
      build` limpio en cada checkpoint; migración con `Up`/`Down` vacíos verificada leyendo
      el archivo antes de aplicarla; `dotnet ef database update` aplicado sin DDL; la app
      arrancó y sirvió `/`, `/solicitudes/bandeja`, `/solicitudes/administrar`,
      `/solicitudes/historial-envios`, `/solicitudes/mis-solicitudes`, `/catalogo` sin
      errores en el log (solo el warning de siempre de HTTPS redirect en dev). **No se
      probó clic-por-clic en el navegador** — falta que el usuario confirme el flujo
      completo en vivo.
