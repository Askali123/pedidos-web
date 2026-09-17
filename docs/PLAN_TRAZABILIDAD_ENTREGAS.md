# Plan de mejoras — trazabilidad de solicitudes, dirección y confirmación de entrega

Checklist de trabajo, de mayor a menor prioridad, salida del análisis hecho el 2026-09-16
sobre dos problemas relacionados: (1) el mensaje que recibe el solicitante cuando el
gestor resuelve **una sola línea** de un pedido dice "Pedido rechazado/aprobado" como si
se hubiera resuelto el pedido completo, y (2) los PDF exportados no dicen a qué
**dirección** hay que llevar los insumos ni dejan constancia de si realmente se
**entregaron** ahí. Cada tarea se marca `[x]` al completarse; no reordenar sin avisar —
el orden es la prioridad acordada.

## Decisiones de diseño ya acordadas

- **Dirección de entrega**: el usuario guarda una dirección por defecto en su perfil
  (`ApplicationUser.DireccionPredeterminada`); al armar el carrito se autocompleta pero
  es editable, y lo que quede en ese momento se congela en `Pedido.DireccionEntrega`
  (mismo patrón que ya usa el sistema con `SolicitanteNombre`/`GestorNombre`: snapshot al
  momento del evento, no una referencia viva al perfil).
- **Confirmación de entrega**: entidad nueva y separada `ConfirmacionEntrega` ligada al
  `Pedido` (1:1 — sin registro significa "todavía no entregado"), con
  `FechaEntrega`, `ConfirmadoPorId`/`ConfirmadoPorNombre`, `DireccionEntregada`,
  `CoincideConDireccionIndicada` (bool) y `Observaciones` (opcional). No se toca el enum
  `EstadoSolicitud` ni la lógica de aprobación/descuento de stock ya existente — mismo
  criterio que se usó para `NotificacionProveedor` (registro de auditoría aparte, no un
  nuevo estado en la máquina de estados de la solicitud).

## P0 — Bug de corrección

- [x] **1. Corregir el título de la notificación al resolver una sola línea** — **Hecho.**
  `NotificarResolucionAsync` ahora recibe el título ya armado por el caller en vez de
  derivarlo genéricamente de `aprobada`. `ResolverAsync` (una línea) arma
  "Solicitud aprobada"/"Solicitud rechazada" con el mensaje "Tu solicitud de \"X\" fue
  aprobada/rechazada..." (antes decía "Tu pedido de..."); `ResolverPedidoAsync` (pedido
  completo) sigue armando "Pedido aprobado"/"Pedido rechazado" — ese caso sí es correcto
  tal cual estaba. El toast que ve el gestor en `Bandeja.razor` ya distinguía bien los dos
  casos y no se tocó.
  Verificado en el navegador: creé un pedido con 2 líneas, rechacé solo una desde la
  Bandeja (botón "Rechazar" de esa línea, no "Rechazar todo") y la notificación que le
  llegó al solicitante dijo **"Solicitud rechazada" — "Tu solicitud de \"Abrasivo
  REGULAR\" fue rechazada por Gestor de Catálogo."** Como prueba de regresión, aprobé otro
  pedido de 2 líneas con "Aprobar todo" y la notificación siguió diciendo correctamente
  **"Pedido aprobado" — "Tu pedido (2 productos: ...) fue aprobado..."**
  Archivo: `CatalogoPedidos.Application/Solicitudes/SolicitudService.cs`

## P1 — Trazabilidad / auditoría (caso original + fundamento de dirección)

- [x] **2. Revisar y alinear el wording de PDF/Excel exportado (línea vs pedido)** — **Hecho.**
  `ExportarSolicitud` y `ExportarPedido` ya estaban bien tituladas ("Solicitud N.º"/"Pedido
  N.º"), pero encontré dos problemas reales al revisar: (1) `ExportarSolicitud` rotulaba
  el comentario del pedido (compartido por todas sus líneas) como "Comentario del
  solicitante", dando a entender que era algo propio de esa única línea — ahora dice
  "Comentario del pedido". (2) `ExportarSolicitudesPorProveedor` (PDF y Excel) se llamaba
  "Pedido a proveedor" — nombre engañoso, porque este mismo export lo usa tanto el envío
  real de UN pedido puntual como el reporte filtrado por proveedor de "Administrar
  pedidos", que puede traer líneas de decenas de pedidos distintos a la vez. Se renombró a
  "Detalle de productos para proveedor" (PDF) / "Detalle por proveedor" (pestaña de Excel)
  y se agregó una columna "Pedido" al PDF (el Excel ya la tenía) para que quede explícito
  a qué pedido pertenece cada línea.
  Verificado en el navegador: exporté el PDF y el Excel filtrados por "Distribuciones
  Andinas" (97 líneas en 47 pedidos distintos) — el título ya no dice "Pedido a
  proveedor" y la columna "Pedido" muestra #56, #55, #51, #50... confirmando que de
  verdad son varios pedidos. También exporté la solicitud #110 (del pedido #51, que
  tiene comentario "pedido para Aurotech sas") y el PDF individual mostró correctamente
  "Comentario del pedido: pedido para Aurotech sas".
  Archivos: `PdfExportService.cs`, `ExcelExportService.cs`

- [x] **3. Campo de dirección predeterminada en el perfil del usuario** — **Hecho.**
  Nuevo campo `ApplicationUser.DireccionPredeterminada` (opcional) + migración
  `AgregarDireccionPredeterminada`. Editable en `Register.razor` (opcional al
  registrarse) y en la página de perfil (`Account/Pages/Manage/Index.razor`).
  Al revisar la página de perfil encontré y corregí, de paso, **dos bugs preexistentes
  que impedían guardar CUALQUIER cambio ahí** (no específicos de mi campo nuevo — ya
  rompían el guardado del teléfono también, nadie los había notado porque nadie había
  probado esa pantalla):
  1. El form-post deja `Input.PhoneNumber` en `""` (no `null`) cuando el campo llega
     vacío, y `[Phone]` sí valida `""` como inválido — bloqueaba el guardado con "The
     Phone number field is not a valid phone number" para cualquier usuario sin
     teléfono. Se normaliza a `null` en `OnInitializedAsync` antes de que corra la
     validación.
  2. `Manage/Index.razor` comparte layout con `UserMenu.razor` (el menú "Sesión
     iniciada como..." del header), que también llama a `UserManager.GetUserAsync` en
     su propio `OnInitializedAsync` — ambos corren concurrentemente sobre el mismo
     `AppDbContext` Scoped que usa Identity (ver comentario en
     `DependencyInjection.cs`), y EF Core no tolera eso ("A second operation was
     started on this context instance..."). Se aisló `Manage/Index.razor` en su propio
     scope de DI para el `UserManager`/`SignInManager` (mismo patrón que ya usaba
     `IdentityRevalidatingAuthenticationStateProvider` para lo mismo), y se cuidó de
     volver a pedir el usuario con el `UserManager` de ESE scope en `OnValidSubmitAsync`
     en vez de reusar el objeto cargado en `OnInitializedAsync` (si no, Identity
     terminaba rastreando dos instancias distintas con el mismo Id en el mismo
     `DbContext` al validar).
  Verificado en el navegador: registré un usuario nuevo con dirección — quedó guardada
  y se vio precargada en su perfil. Antes de los dos fixes, guardar cualquier cambio en
  el perfil (aunque fuera solo la dirección) tiraba "Internal Server Error"
  consistentemente, 100% de las veces; después de los fixes, edité la dirección desde
  el perfil dos veces seguidas (con recarga limpia entre medio) y ambas quedaron
  guardadas sin error, con el mensaje "Your profile has been updated".
  Archivos: `ApplicationUser.cs`, migración `AgregarDireccionPredeterminada`,
  `Register.razor`, `Manage/Index.razor`

- [x] **4. Dirección de entrega por pedido (snapshot editable en el carrito)** — **Hecho.**
  Nuevo campo `Pedido.DireccionEntrega` + migración `AgregarDireccionEntregaPedido`.
  `Carrito.razor` la precarga desde `ApplicationUser.DireccionPredeterminada` al entrar
  (campo nuevo "Dirección de entrega" en la tarjeta "Enviar al gestor"), pero es editable
  antes de enviar; lo que quede en ese momento es lo que viaja a
  `SolicitudService.CrearPedidoAsync` (nuevo parámetro opcional `direccionEntrega`) y
  queda grabado en el pedido — snapshot, no una referencia al perfil.
  Al implementar esto apareció el MISMO bug de concurrencia de la tarea 3
  (`UserManager.GetUserAsync` chocando con `UserMenu.razor` del header), esta vez en
  `Carrito.razor` — se aplicó el mismo arreglo (scope aislado vía `IServiceScopeFactory`
  para esa consulta puntual).
  Verificado en el navegador y en la base: (1) con la cuenta de la tarea 3 (que sí tiene
  dirección predeterminada), el carrito la precargó, la edité a "Bodega temporal para
  este pedido, Calle 99 #1-1" antes de enviar, y el pedido #57 quedó en la base con
  exactamente ese texto editado (no el del perfil) — confirma el snapshot. (2) con
  `usuario@catalogo.local` (sin dirección predeterminada), el campo apareció vacío sin
  errores, envié el pedido #58 sin completarlo, y quedó `NULL` en la base sin romper
  nada — confirma que el campo es genuinamente opcional.
  Archivos: `Pedido.cs`, migración `AgregarDireccionEntregaPedido`,
  `ISolicitudService.cs`, `SolicitudService.cs`, `Carrito.razor`

- [x] **5. Mostrar la dirección de entrega en Bandeja/Administrar/Mis solicitudes** — **Hecho.**
  Las tres pantallas ya agrupaban las líneas por un `record PedidoResumen` (con
  `Comentario` incluido) — se le agregó `DireccionEntrega` al record y al `GroupBy` de
  cada una, y una línea "Dirección de entrega: ..." junto al comentario del pedido,
  visible solo cuando el pedido tiene una cargada (sin línea vacía para los pedidos
  creados antes de esta tarea, que tienen `NULL`).
  Verificado en el navegador con el pedido #57 (el de la tarea 4, con dirección
  editada): apareció correctamente en Bandeja y en Administrar (vista del gestor) y en
  Mis solicitudes (vista del solicitante); los pedidos #55/#56/#58 (sin dirección) no
  muestran la línea, como se esperaba.
  Archivos: `Bandeja.razor`, `Administrar.razor`, `MisSolicitudes.razor`

- [x] **6. Incluir la dirección de entrega en los PDFs existentes** — **Hecho.**
  `ExportarSolicitud` y `ExportarPedido` (`PdfExportService.cs`) ahora imprimen
  "Dirección de entrega: ..." junto al comentario del pedido, solo cuando el pedido
  tiene una cargada.
  Verificado en el navegador: descargué el PDF del pedido #57 (`/api/pedidos/57/pdf`) y
  el de su línea individual (`/api/solicitudes/119/pdf`) — ambos muestran "Dirección de
  entrega: Bodega temporal para este pedido, Calle 99 #1-1" en el lugar esperado.
  Archivo: `PdfExportService.cs`

- [x] **7. Agregar "quién revisó" al PDF "por proveedor"** — **Hecho.**
  `ExportarSolicitudesPorProveedor` (PDF) ganó una columna "Gestor" con
  `s.GestorNombre ?? "-"` — mismo criterio que ya usaban `ExportarPedido`/
  `ExportarSolicitudes`. Alcance acotado al PDF, tal como decía la tarea (la versión
  Excel de este mismo reporte quedó igual, fuera de este alcance).
  Verificado en el navegador: descargué el PDF filtrado por "Distribuciones Andinas"
  (99 productos) desde Administrar pedidos — la nueva columna "Gestor" muestra
  "Gestor de Catálogo" en las líneas ya resueltas y "-" en las que siguen Pendiente
  (ej. pedido #58, #57), coherente con que a esas todavía no se les asignó gestor.
  Archivo: `PdfExportService.cs`

## P2 — UX pulido (resolución de solicitudes)

- [x] **8. Deep link más preciso en la notificación de resolución** — **Hecho.**
  `Mis solicitudes` lista TODOS los pedidos del solicitante sin paginar ni filtrar — no
  hay un query param de "pedido" que filtrar como en la alerta de stock bajo (tarea 15
  del plan anterior), así que el criterio análogo acá es un ancla de fragmento: cada
  tarjeta de pedido ahora tiene `id="pedido-{PedidoId}"`, y `NotificarResolucionAsync`
  arma la URL como `/solicitudes/mis-solicitudes#pedido-{pedidoId}` (antes mandaba
  siempre a la lista genérica sin apuntar a nada). `ResolverAsync`/`ResolverPedidoAsync`
  le pasan el `PedidoId` correspondiente.
  Verificado en el navegador: rechacé una línea del pedido #55 (que para ese solicitante
  ya NO es el más reciente — hay pedidos #56/#57/#58 después) y al hacer clic en la
  notificación la URL quedó en `...mis-solicitudes#pedido-55` y la página hizo scroll
  automático hasta esa tarjeta específica, salteándose las más recientes que aparecen
  primero en la lista — confirma que el ancla apunta al pedido correcto, no solo a la
  lista en general.
  Archivos: `SolicitudService.cs`, `MisSolicitudes.razor`

- [x] **9. Indicador de "pedido parcialmente resuelto" en Mis solicitudes** — **Hecho.**
  Nuevo helper `ResumenEstado` que arma un badge en el encabezado del pedido, pero solo
  cuando aporta algo que los badges por línea no dicen ya: pedidos de una sola línea, sin
  nada resuelto todavía, o resueltos de forma pareja (todo aprobado o todo rechazado) no
  muestran nada — ahí el detalle por línea ya cuenta toda la historia. Dos casos sí lo
  muestran: (1) progreso parcial — "X de N resueltas" (badge celeste) cuando algunas
  líneas ya se resolvieron y otras siguen pendientes; (2) resultado mixto — "X
  aprobada(s), Y rechazada(s)" (badge amarillo) cuando el pedido ya está 100% resuelto
  pero con una mezcla de aprobaciones y rechazos.
  Verificado en el navegador con un pedido de 3 líneas: tras aprobar 1 de 3, apareció
  "1 de 3 resueltas"; tras resolver las 3 (2 aprobadas + 1 rechazada), cambió a "2
  aprobada(s), 1 rechazada(s)"; un pedido de 2 líneas ambas aprobadas (sin mezcla) no
  mostró ningún badge, como se esperaba.
  Archivo: `MisSolicitudes.razor`

- [x] **10. Notificación agrupada al resolver una mezcla de líneas de un tirón** — **Hecho.**
  Al revisar el código se confirmó que `ResolverPedidoAsync` ("Aprobar todo"/"Rechazar
  todo") ya mandaba una sola notificación resumen — quedó así de las tareas P1. El hueco
  real era otro: no existía forma de resolver una MEZCLA de decisiones (algunas líneas
  aprobadas, otras rechazadas) en un solo paso — solo se podía línea por línea, cada clic
  con su propia notificación inmediata, o "todo" con una única decisión uniforme.
  Se agregó `ResolverVariasAsync` en `SolicitudService`, que resuelve una lista de
  decisiones (`SolicitudId` + `Aprobar` por línea) y manda UNA sola notificación armada
  por el nuevo helper `ArmarResolucionPedido`: título/mensaje "aprobado"/"rechazado" si
  todas las líneas comparten la decisión (mismo texto que antes), o "Pedido resuelto" con
  el desglose "X aprobado(s) (...), Y rechazado(s) (...)" si es mixto.
  `ResolverPedidoAsync` ahora es un caso particular que arma una lista de decisiones
  uniformes y delega en `ResolverVariasAsync`, así ambos caminos comparten la misma
  lógica de notificación.
  En `Bandeja.razor` se agregó un botón "Resolver mezcla…" (solo visible en pedidos de
  más de una línea) que activa un modo de selección: cada línea muestra botones
  Aprobar/Rechazar tipo toggle para marcar la decisión sin resolver todavía, y una barra
  al pie del pedido con "N marcada(s)" + "Confirmar selección", que abre el mismo modal
  de comentario y llama a `ResolverVariasAsync` con todas las decisiones marcadas.
  Verificado en el navegador end-to-end: se creó el pedido #60 (3 líneas) como
  `usuario@catalogo.local`; como `gestor@catalogo.local` se activó "Resolver mezcla…",
  se marcó Aprobar en 2 líneas y Rechazar en 1, y el modal mostró "2 aprobación(es) y 1
  rechazo(s)" antes de confirmar. Tras confirmar, la bandeja mostró "Pedido #60 resuelto
  (3 producto(s))" y el pedido desapareció de pendientes. Se confirmó por SQL que solo se
  insertó UNA fila en `Notificaciones` para el pedido (título "Pedido resuelto", mensaje
  "Tu pedido fue resuelto por Gestor de Catálogo: 2 aprobado(s) (2 productos: 1x Abrasivo
  REGULAR, 1x Ajax Bicarbonato x 2000 ml), 1 rechazado(s) (1x Alcohol Antiseptico Fire x
  3750 ml)."). Como el solicitante, la campana mostró esa única notificación (no 3), el
  clic navegó al deep link `#pedido-60`, y "Mis solicitudes" mostró el pedido con el
  badge de la tarea 9 ("2 aprobada(s), 1 rechazada(s)") y cada línea con su estado
  correcto.
  Archivo(s): `SolicitudDto.cs`, `ISolicitudService.cs`, `SolicitudService.cs`,
  `Bandeja.razor`

## P3 — Confirmación de entrega (funcionalidad nueva, núcleo)

- [ ] **11. Entidad `ConfirmacionEntrega` (dominio + migración + repositorio)**
  Campos: `PedidoId`, `FechaEntrega`, `ConfirmadoPorId`, `ConfirmadoPorNombre`,
  `DireccionEntregada`, `CoincideConDireccionIndicada`, `Observaciones`. Repositorio con
  `ObtenerPorPedidoAsync`/`CrearAsync`, mismo patrón que `NotificacionProveedorRepository`.

- [ ] **12. Servicio + UI: "Confirmar entrega" en Bandeja/Administrar**
  Botón visible cuando el pedido tiene al menos una línea Aprobada. Abre un modal (mismo
  patrón que "Enviar a proveedor"): dirección entregada prellenada con
  `Pedido.DireccionEntrega`, checkbox "¿Coincide con la dirección indicada?" y
  observaciones opcionales.

- [ ] **13. Validar que solo se confirme entrega de pedidos con algo Aprobado**
  Regla de negocio: no debería poder confirmarse la entrega de un pedido totalmente
  Pendiente o totalmente Rechazado — no hay nada que entregar.

- [ ] **14. Notificación al solicitante cuando se confirma la entrega**
  Nuevo mensaje (y eventualmente `TipoNotificacion.EntregaConfirmada`) avisando que su
  pedido fue entregado, con la fecha y si coincidió con la dirección indicada.

- [ ] **15. Badge de estado de entrega en Mis solicitudes/Administrar/Bandeja**
  "✓ Entregado el X por Y" o, si no coincidió la dirección, algo que llame la atención y
  muestre las observaciones del gestor.

- [ ] **16. Incluir la confirmación de entrega en el PDF del pedido**
  Sección "Entrega" en `ExportarPedido` (fecha, quién confirmó, dirección entregada, si
  coincidió, observaciones) — solo cuando ya exista el registro de `ConfirmacionEntrega`.


## P4 — Nuevas funcionalidades adicionales

- [ ] **17. Historial/auditoría de resoluciones para el gestor**
  Filtro por gestor/fecha de quién resolvió qué y cuándo, mismo espíritu que la pantalla
  de historial de envíos a proveedor ya construida (tarea 12 del plan anterior).

- [ ] **18. Filtro "pendientes de confirmar entrega" en Administrar pedidos**
  Para que el gestor no pierda de vista los pedidos ya aprobados que todavía no tienen
  `ConfirmacionEntrega`.

- [ ] **19. Recordatorio si un pedido aprobado lleva mucho tiempo sin entrega confirmada**
  Mismo patrón que `Pedido.RecordatorioEnviado` (ya existe para pedidos sin resolver) —
  aplicado ahora a pedidos aprobados que llevan demasiado tiempo sin `ConfirmacionEntrega`.

---

*Se trabaja de arriba hacia abajo, una tarea a la vez. Al terminar una, se marca `[x]` y
se resume qué cambió y cómo se verificó en el navegador (mismo formato que
`docs/PLAN_FLUJO_PEDIDO_PROVEEDOR.md`) antes de pasar a la siguiente.*
