# Plan de mejoras — envío a proveedores y confirmación de entrega por proveedor

Checklist de trabajo, de mayor a menor prioridad, salida del análisis hecho el
2026-09-18 sobre la funcionalidad de "enviar a proveedor" en Bandeja/Administrar y su
conexión con la confirmación de entrega. El disparador fue una pregunta directa: cuando
un pedido tiene productos de categorías distintas (cómputo, cafetería, papelería...) que
no siempre comparten proveedor, ¿cómo sabe el sistema a qué proveedor le corresponde cada
producto, y qué tan clara es esa relación muchos-a-muchos para el gestor? Cada tarea se
marca `[x]` al completarse; no reordenar sin avisar — el orden es la prioridad acordada.

## Qué ya funciona bien (no se toca)

`PedidoNotificacionProveedorService` ya resuelve correctamente el caso básico: agrupa las
líneas **Aprobadas** de un pedido por los proveedores asociados a cada producto
(`ProductoProveedor`, la tabla muchos-a-muchos real, con su propio código por proveedor),
arma un dropdown "Enviar a proveedor" con una opción por cada proveedor que tenga algo
suyo en el pedido, y al enviar el correo (PDF/Excel adjunto) **solo** incluye las líneas
de ESE proveedor. No manda el pedido completo a un proveedor por error.

## Los dos gaps que sí hay que resolver

1. **El dropdown de envío no distingue qué producto es de quién** — solo muestra
   "Proveedor X · 3 líneas", nunca los nombres. Y nada impide mandarle el mismo producto
   a dos proveedores distintos: una vez enviada una línea a un proveedor, sigue
   apareciendo disponible (con el mismo conteo) para cualquier otro. El campo
   `EsPreferido` de `ProductoProveedor` existe pero solo se usa para decidir qué código
   mostrar en la tabla, no para nada del envío.
2. **La confirmación de entrega es una sola por pedido completo** (`ConfirmacionEntrega`
   es 1:1 con `Pedido`, con índice único en `PedidoId` — el servicio tira error si ya
   existe una). Si un pedido tiene productos de 2 proveedores que entregan en fechas
   distintas, hoy no hay forma de reflejarlo — es "todo entregado" o "nada entregado".
   Tampoco existe un campo para "quién lo recibió" como persona distinta de quién lo
   confirmó desde la app (el gestor logueado).

## Decisiones de diseño ya acordadas

- **Multi-proveedor en el envío**: se prioriza mostrar el detalle de qué productos le
  corresponden a cada proveedor candidato en el selector de envío, y evitar el envío
  duplicado — una vez que una línea fue enviada a un proveedor, deja de ofrecerse para
  otro. No se fuerza un "proveedor único" a nivel de catálogo (eso habría cambiado la
  gestión de productos/aprobación, un alcance mucho mayor); la ambigüedad de origen (un
  producto con varios proveedores) sigue existiendo, pero deja de ser invisible ni
  explotable por accidente.
- **Confirmación de entrega por proveedor**: `ConfirmacionEntrega` pasa de 1:1 con
  `Pedido` a 1:muchos, agregando `ProveedorId` (nullable). Cada proveedor al que se le
  envió algo del pedido tiene su propia confirmación, con su propia fecha/dirección/quién
  recibió. `ProveedorId = null` es el "cajón" para líneas aprobadas que nunca se asociaron
  a ningún proveedor (compra local/manual, sin correo de por medio) — sigue siendo
  confirmable como hoy, no se pierde esa posibilidad. El pedido muestra un **progreso
  agregado** (ej. "2/3 entregados") calculado a partir de estas confirmaciones, no un
  campo nuevo y manual de "completo/parcial" — así no puede quedar desincronizado de la
  realidad. La uniqueness (un proveedor no se puede confirmar dos veces en el mismo
  pedido) se valida en el servicio, no con un índice único de base de datos — mismo
  criterio que ya usa el resto de la app (ej. NIT de proveedor) para no pelear con la
  semántica de NULL de SQL Server en índices únicos.
- **"Quién lo recibió"**: campo de texto libre y opcional (`RecibidoPorNombre`), separado
  de `ConfirmadoPorNombre` (que sigue siendo, como hoy, el gestor que llena el formulario
  desde la app — no necesariamente quien recibió físicamente los insumos).

## P0 — Claridad y seguridad en el envío a proveedores

- [x] **1. Mostrar qué productos le corresponden a cada proveedor en el selector de envío** — Hecho.
  El dropdown "Enviar a proveedor" (Bandeja/Administrar) solo decía "Proveedor X · N
  líneas", sin nombrar los productos — el gestor elegía a ciegas, sobre todo cuando el
  pedido mezcla categorías con proveedores distintos o un mismo producto tiene más de un
  proveedor asociado.

  `ProveedorDelPedidoDto` (Application) ganó una lista `Productos` (`ProductoId`,
  `Nombre`, `Cantidad`); `PedidoNotificacionProveedorService.ObtenerProveedoresDisponiblesAsync`
  ahora arma esa lista al agrupar las líneas aprobadas por proveedor asociado (ya tenía
  `Producto` cargado vía `Include` en `ObtenerPedidoAsync`, no hizo falta tocar el
  repositorio). En la UI (`Bandeja.razor` y `Administrar.razor`, mismo cambio en ambas):
  cada opción del dropdown ahora muestra, debajo del nombre del proveedor y el conteo, los
  nombres de los productos que le tocan (`w-60` → `w-72` para que entre el texto); y el
  `ConfirmDialog` de "Enviar a proveedor" — el último paso antes del envío real e
  irreversible — agrega una lista con nombre y cantidad de cada producto, en vez de solo
  el conteo.

  Verificado en el navegador logueado como Gestor, con el Pedido #1 (línea de "Laptop 14\""
  aprobada, asociada a DOS proveedores — TechImport SAS y Distribuciones Andinas, el caso
  ambiguo real que motivó esta tarea): el dropdown en Administrar mostró "Distribuciones
  Andinas · 1 prod. · Laptop 14\"" y "TechImport SAS · 1 prod. · Laptop 14\"" — ahora se ve
  clarísimo que ambos compiten por el mismo producto. Al hacer clic, el modal de
  confirmación mostró "Laptop 14\" · cantidad: 1" antes del botón "Enviar" (cancelado sin
  enviar, para no disparar un correo real de prueba). Repetido en Bandeja con el Pedido
  #61 (línea "Abrasivo REGULAR" aprobada, proveedor único) — mostró correctamente
  "Distribuciones Andinas · 1 prod. · Abrasivo REGULAR", distinguiendo bien esa línea
  aprobada de la otra línea pendiente del mismo pedido que se ve en la tabla. Sin errores
  nuevos en el log del servidor (solo el falso positivo ya conocido de
  `HttpsRedirectionMiddleware`).
  Archivo(s): `ProveedorDelPedidoDto.cs`, `PedidoNotificacionProveedorService.cs`,
  `Bandeja.razor`, `Administrar.razor`

- [x] **2. Evitar enviarle el mismo producto a dos proveedores distintos** — Hecho.
  `ObtenerProveedoresDisponiblesAsync`/`EnviarAProveedorAsync` no descartaban una línea ya
  cubierta por un envío previo a OTRO proveedor — el mismo producto seguía ofreciéndose
  (con el mismo conteo) a cualquier otro proveedor candidato, sin límite.

  Se agregó `ObtenerCoberturaPreviaAsync` (privado, reusado por los dos métodos): mira los
  `NotificacionProveedor` ya registrados del pedido y calcula qué `ProductoId` quedaron
  "cubiertos" por esos envíos, sin importar a cuál proveedor específico. Con eso:
  - `ObtenerProveedoresDisponiblesAsync` excluye, para cada proveedor candidato, las
    líneas ya cubiertas por OTRO proveedor — pero sigue mostrando las líneas ya cubiertas
    por ESE MISMO proveedor (permite reenviar, protegido aparte por el cooldown de 5 min
    que ya existía).
  - `EnviarAProveedorAsync` aplica el mismo filtro como última línea de defensa (no
    depende solo de que la UI no lo ofrezca): si después de filtrar no queda ninguna línea
    para enviar, devuelve un resultado con `Enviado = false` y el motivo ("Las líneas de
    este pedido asociadas a {proveedor} ya se enviaron a otro proveedor"), sin llegar a
    tocar el envío de correo.

  Verificado con el Pedido #1 (línea "Laptop 14\"" aprobada, asociada a TechImport SAS y
  Distribuciones Andinas): se simuló un envío previo a TechImport SAS insertando
  directamente en la base un `NotificacionProveedor` de prueba (sin usar el botón real,
  para no disparar un correo real — el SMTP de este proyecto está configurado contra una
  cuenta de Gmail real). Con eso, el dropdown "Enviar a proveedor" pasó de mostrar los 2
  proveedores a mostrar solo "TechImport SAS" — Distribuciones Andinas quedó
  correctamente excluido. Se completó el flujo hasta "Enviar" contra TechImport SAS (que
  no tiene email cargado en la base, así que el servicio corta antes de llamar al SMTP) y
  el resultado fue el esperado: "No se pudo enviar a TechImport SAS: El proveedor no tiene
  correo registrado." — confirma que la línea de "Laptop 14\"" sí seguía disponible para
  reenviarle al mismo proveedor que ya la tenía. Se borró el registro de prueba al
  terminar; el dropdown volvió a mostrar ambos proveedores. Sin errores nuevos en el log
  del servidor (solo el falso positivo ya conocido de `HttpsRedirectionMiddleware`).
  Archivo(s): `PedidoNotificacionProveedorService.cs`

## P1 — Confirmación de entrega por proveedor

- [x] **3. Migrar `ConfirmacionEntrega` de 1:1 a 1:muchos por `Pedido`, con `ProveedorId` opcional** — Hecho.
  `Pedido.ConfirmacionEntrega` (singular) pasó a `Pedido.ConfirmacionesEntrega`
  (`ICollection<ConfirmacionEntrega>`). `ConfirmacionEntrega` ganó `ProveedorId` (`int?`)
  + navegación `Proveedor? Proveedor` (`null` = líneas sin proveedor asociado, sigue
  siendo confirmable como antes) y `RecibidoPorNombre` (`string?`, sin usar todavía — lo
  conecta la tarea 6). En `AppDbContext`: la relación con `Pedido` pasó de `WithOne` a
  `WithMany`; se agregó el FK a `Proveedor` (mismo patrón `Restrict` que
  `NotificacionProveedor`); se sacó el índice único de `PedidoId` (ya no aplica — la
  unicidad por proveedor se valida en el servicio en la tarea 4, no con un índice, porque
  SQL Server trata cada NULL como distinto y no protegería el caso "sin proveedor").
  Migración EF Core `ConfirmacionEntregaPorProveedor`, aplicada a la base de desarrollo.

  Esta tarea es puramente de esquema — no cambia el comportamiento observable todavía
  (eso es la tarea 4). Para que el resto del código siguiera compilando se tocaron 3
  lugares más, de la forma menos invasiva posible: `SolicitudRepository.ObtenerPedidoAsync`
  (`Include` de la colección + `Proveedor`), `ObtenerAprobadosSinEntregaAsync` (la
  condición pasó a "sin ninguna confirmación todavía" — con un `TODO` explícito apuntando
  a la tarea 8, porque un pedido con confirmación PARCIAL todavía no se detecta ahí) y
  `PdfExportService.ExportarPedido` (la sección "Entrega" del PDF ahora itera las
  confirmaciones y muestra a qué proveedor corresponde cada una, en vez de asumir una
  sola). `ConfirmacionEntregaRepository`/`ConfirmacionEntregaService` no se tocaron —
  siguen devolviendo una sola confirmación (la primera que encuentren) hasta que la
  tarea 4 los reescriba para la nueva granularidad.

  Verificado en el navegador logueado como Gestor: el Pedido #61, que ya tenía una
  confirmación de entrega creada ANTES de esta migración, se sigue viendo igual en
  Bandeja (badge "✓ Entregado el 16/09/2026 19:40 por Gestor de Catálogo") — los datos
  existentes sobrevivieron la migración sin pérdida. Se descargó el PDF del pedido y,
  revisando el log del servidor, la consulta EF generada trae correctamente el nuevo
  `LEFT JOIN` a `Proveedores` a través de `ConfirmacionEntrega.ProveedorId` junto con
  `RecibidoPorNombre`, sin ninguna excepción. Sin errores nuevos en el log del servidor
  (solo el falso positivo ya conocido de `HttpsRedirectionMiddleware`).
  Archivo(s): `ConfirmacionEntrega.cs`, `Pedido.cs`, `AppDbContext.cs`,
  `SolicitudRepository.cs`, `PdfExportService.cs`, migración
  `ConfirmacionEntregaPorProveedor`

- [x] **4. Reescribir el servicio de confirmación de entrega para la nueva granularidad** — Hecho.
  `IConfirmacionEntregaRepository`/`IConfirmacionEntregaService.ObtenerPorPedidoAsync`
  ahora devuelven `List<ConfirmacionEntrega>` (antes una sola).

  Se agregó el concepto de **"grupo de entrega"** (`GrupoEntregaDto`, nuevo): las líneas
  Aprobadas de un pedido que realmente se le enviaron a un proveedor puntual (hay un
  `NotificacionProveedor` que las cubre) forman un grupo con ese proveedor; lo que quede
  sin cubrir por ningún envío (sin asociación a proveedor, o con asociación pero sin
  enviar todavía) cae en un grupo "sin proveedor" (`ProveedorId = null`) — mismo criterio
  de "cobertura" que ya usa `PedidoNotificacionProveedorService` para la tarea 2, para no
  duplicar lógica de forma inconsistente. `ObtenerGruposDeEntregaAsync` (nuevo, público)
  calcula estos grupos; `ObtenerProgresoAsync` (nuevo) resume cuántos hay y cuántos ya
  están confirmados, para la tarea 7. `ConfirmarAsync` ahora recibe `int? proveedorId`:
  valida que ese grupo exista con líneas sin cubrir y que no tenga ya una confirmación —
  si no, `InvalidOperationException` con un mensaje específico para cada caso. Se
  eligió NO hacer que `ConfirmacionEntregaService` dependa de
  `IPedidoNotificacionProveedorService` (para mantener el patrón ya existente en la app de
  que los servicios de Application dependen de repositorios, no entre sí) — en cambio
  inyecta directamente `IProductoProveedorRepository`/`INotificacionProveedorRepository`,
  los mismos repos que ya usa el otro servicio para el mismo cálculo.

  También se agregó `ConfirmarEntregaDto.RecibidoPorNombre` (sin conectar a la UI
  todavía — eso es la tarea 6, pero el campo de dominio ya existía desde la tarea 3 y
  tenía sentido threadearlo por el servicio de una vez).

  Como el cambio de firma rompe a los tres consumidores actuales, se los adaptó al mínimo
  indispensable para seguir compilando y funcionando — **sin** el rediseño de UI que
  corresponde a las tareas 5 y 7 (un `TODO` con la tarea correspondiente marca cada punto):
  `Bandeja.razor`, `Administrar.razor` y `MisSolicitudes.razor` siguen mostrando un único
  badge (toman `.FirstOrDefault()` de la lista) y el botón único de "Confirmar entrega"
  sigue existiendo, ahora apuntando explícitamente a `proveedorId: null` (el grupo "sin
  proveedor", donde caen hoy casi todos los pedidos reales ya que ninguno mezclaba
  confirmación con envíos reales a proveedor).

  Verificado en el navegador logueado como Gestor: (a) camino feliz — el Pedido #2 (Mouse
  inalámbrico, aprobado, sin ningún proveedor asociado) se confirmó con el botón único de
  siempre sin ningún cambio de comportamiento visible ("Entrega del pedido #2
  confirmada.", badge "✓ Entregado..." correcto) — se revirtió después de verificar,
  ya que era solo para la prueba; (b) el guardrail nuevo — se simuló (por SQL, sin
  disparar un correo real) un envío real a un proveedor para el Pedido #1, dejando SIN
  NADA el grupo "sin proveedor"; el botón único (que hoy solo sabe confirmar ese grupo)
  tiró exactamente el error esperado — confirmado en el log del servidor:
  "`InvalidOperationException: Este pedido no tiene líneas aprobadas pendientes de entrega
  para ese proveedor.`" (la UI no lo maneja con gracia todavía — mostró la pantalla de
  error genérica de Blazor Server, un gap preexistente que ya tenía el código original
  para el caso análogo de "ya fue confirmada", no algo introducido acá; la tarea 5 lo
  soluciona de raíz dejando de ofrecer un botón que no puede funcionar). Se limpiaron
  todos los datos de prueba (el envío simulado, la confirmación y la notificación interna
  de la prueba (a)) al terminar. Sin errores nuevos en el log del servidor aparte de la
  excepción esperada de la prueba (b) y el falso positivo ya conocido de
  `HttpsRedirectionMiddleware`.
  Archivo(s): `IConfirmacionEntregaRepository.cs`, `ConfirmacionEntregaRepository.cs`,
  `IConfirmacionEntregaService.cs`, `ConfirmacionEntregaService.cs`,
  `ConfirmarEntregaDto.cs`, `GrupoEntregaDto.cs` (nuevo), `Bandeja.razor`,
  `Administrar.razor`, `MisSolicitudes.razor`

- [x] **5. UI: un botón "Confirmar entrega" por proveedor pendiente, no uno solo por pedido** — Hecho.
  En `Bandeja.razor` y `Administrar.razor`, el botón único se reemplazó por uno por cada
  grupo de entrega pendiente (`gruposEntregaPorPedido`, cargado con
  `ConfirmacionesEntrega.ObtenerGruposDeEntregaAsync`): "Confirmar entrega — {proveedor}"
  para los que tienen proveedor, "Confirmar entrega — sin proveedor asociado" para el
  resto. Cada botón pasa el `GrupoEntregaDto` completo a `AbrirConfirmarEntrega`, que
  ahora guarda también `proveedorIdEntregaPendiente` y arma el título del modal
  (`tituloModalEntrega`) con el nombre del proveedor — mismo modal de siempre (dirección,
  checkbox de coincidencia, observaciones), solo que ahora scopeado al grupo elegido.
  `ConfirmarEntregaSubmit` pasa ese `proveedorIdEntregaPendiente` a `ConfirmarAsync` en vez
  del `null` fijo que había quedado como parche temporal en la tarea 4.

  También se actualizó el filtro "Solo pendientes de confirmar entrega" de
  `Administrar.razor` (usaba el chequeo viejo de una sola confirmación) para que revise si
  el pedido tiene ALGÚN grupo sin confirmar, y se refresca `gruposEntregaPorPedido` justo
  después de un envío a proveedor exitoso — un envío nuevo puede hacer aparecer un grupo
  de entrega nuevo, y el botón correspondiente debe aparecer sin esperar a la próxima
  carga completa de la página.

  La tarea 7 (progreso agregado) queda pendiente — por ahora el badge de arriba
  (`EntregaBadge`) sigue mostrando solo la primera confirmación, no las N que puede haber.

  Verificado en el navegador logueado como Gestor, con el mismo Pedido #1 de la tarea 4
  (simulando otra vez, solo por SQL, un envío real a TechImport SAS — sin correo real de
  por medio): el botón pasó de "Confirmar entrega" genérico a "Confirmar entrega —
  TechImport SAS", y NO apareció ningún botón "sin proveedor asociado" (correcto, todo el
  pedido quedó cubierto por ese envío). Al hacer clic, el modal mostró el título "Confirmar
  entrega — TechImport SAS"; se confirmó con una dirección de prueba y funcionó de punta a
  punta ("Entrega del pedido #1 confirmada.", badge "✓ Entregado..." correcto, el botón
  desapareció después de confirmar). Se limpiaron todos los datos de prueba (el envío
  simulado, la confirmación creada y la notificación interna) al terminar. Sin errores
  nuevos en el log del servidor (solo el falso positivo ya conocido de
  `HttpsRedirectionMiddleware`).
  Archivo(s): `Bandeja.razor`, `Administrar.razor`

- [x] **6. Agregar el campo "Quién lo recibió" al formulario de confirmación** — Hecho.
  El campo de dominio (`ConfirmacionEntrega.RecibidoPorNombre`) y el del DTO
  (`ConfirmarEntregaDto.RecibidoPorNombre`) ya existían desde las tareas 3 y 4 — faltaba
  conectarlos a la UI. Se agregó un `FormField` de texto libre y opcional ("Quién lo
  recibió (opcional)", con hint aclarando que no hace falta llenarlo si el propio gestor
  fue quien recibió) en el modal "Confirmar entrega" de `Bandeja.razor` y
  `Administrar.razor`, entre el checkbox de "¿Coincide con la dirección?" y las
  Observaciones. Se agregó el estado local `recibidoPorPendiente` (reseteado a `null` en
  `AbrirConfirmarEntrega`, igual que los demás campos del formulario) y se lo pasa por
  `ConfirmarEntregaSubmit` al armar el `ConfirmarEntregaDto`.

  Verificado en el navegador logueado como Gestor: se confirmó la entrega del Pedido #1
  (grupo "sin proveedor asociado") completando "Quién lo recibió" con un valor de prueba
  — el modal mostró el campo en el lugar esperado, el submit funcionó
  ("Entrega del pedido #1 confirmada.") y, consultando la base directamente, el valor
  quedó guardado correctamente en la columna `RecibidoPorNombre`. La lectura de ese campo
  en el PDF exportado (`PdfExportService`) ya se había implementado en la tarea 3, así que
  no hizo falta tocarla de nuevo. Se borraron la confirmación y la notificación de prueba
  al terminar. Sin errores nuevos en el log del servidor (solo el falso positivo ya
  conocido de `HttpsRedirectionMiddleware`).
  Archivo(s): `Bandeja.razor`, `Administrar.razor`

- [x] **7. Progreso agregado visible en vez de un badge binario** — Hecho.
  `EntregaBadge.razor` se reescribió para recibir la lista completa de confirmaciones y
  de grupos de entrega (`Confirmaciones`/`Grupos`, en vez del único `Confirmacion?` de
  antes). Con un solo grupo (el caso más común) se ve exactamente igual que antes — mismo
  badge simple, sin cambios visuales para no romper la experiencia habitual. Con más de
  uno, se ve una badge de progreso — "⏳ 1/2 entregado" (variante Info) mientras falta
  alguno, "✓ 2/2 entregados" (variante Success) cuando ya está todo — seguida de un
  desglose por grupo: nombre del proveedor (o "Sin proveedor asociado"), con ✓ y la fecha
  si ya se confirmó, o el ícono de reloj y "pendiente" si no.

  Se actualizaron los tres consumidores: `Bandeja.razor`, `Administrar.razor` y
  `MisSolicitudes.razor` (donde el solicitante ve sus propios pedidos) ahora cargan
  `confirmacionesPorPedido` como `List<ConfirmacionEntrega>` completa (no más
  `.FirstOrDefault()`) y `gruposEntregaPorPedido`, quitando los parches temporales `TODO`
  que había dejado la tarea 4.

  Verificado en el navegador logueado como Gestor, con el Pedido #43 (2 líneas
  aprobadas: una asociada a un proveedor, otra sin ninguno — se simuló un envío real a
  Distribuciones Andinas solo por SQL, sin correo real, para armar el segundo grupo): al
  confirmar únicamente el grupo de Distribuciones Andinas, el badge mostró "⏳ 1/2
  entregado" con el desglose ("✓ Distribuciones Andinas — entregado el ..." /
  "🕐 Sin proveedor asociado — pendiente") y quedó un solo botón "Confirmar entrega"
  visible. Al confirmar también el grupo restante, el badge pasó a "✓ 2/2 entregados"
  (verde) con ambas fechas, y ya no quedó ningún botón "Confirmar entrega" para ese
  pedido. De paso se confirmó que el caso más común (un solo grupo) sigue viéndose
  exactamente igual que antes (probado con Pedido #62, con dirección distinta a la
  indicada). Se limpiaron todos los datos de prueba (envío, confirmaciones y
  notificaciones) al terminar. Sin errores nuevos en el log del servidor (solo el falso
  positivo ya conocido de `HttpsRedirectionMiddleware`).
  Archivo(s): `EntregaBadge.razor`, `Bandeja.razor`, `Administrar.razor`,
  `MisSolicitudes.razor`

- [x] **8. Actualizar la notificación al solicitante y el recordatorio de entrega pendiente** — Hecho.
  **Notificación al solicitante** (`NotificarEntregaAsync`): con un solo grupo (el caso
  común) el mensaje queda exactamente igual que siempre — "Tu pedido #X fue entregado el
  [fecha]...". Con más de un grupo, ahora distingue dos casos: si todavía queda algún OTRO
  grupo sin confirmar, "Ya te entregaron la parte de tu pedido #X correspondiente a
  [proveedor] (o 'sin proveedor asociado'), el [fecha]. Todavía falta el resto[...]"; si
  esta confirmación fue la última que faltaba, "Tu pedido #X ya fue entregado por completo
  — lo último llegó el [fecha][...]". El resto del mensaje (dirección entregada, aviso si
  no coincidió) se sigue agregando igual que antes en los tres casos.

  **Recordatorio de 72h** (`EnviarRecordatoriosEntregaPendienteAsync`): el bug real estaba
  en `SolicitudRepository.ObtenerAprobadosSinEntregaAsync`, que excluía cualquier pedido
  con `ConfirmacionesEntrega.Any()` — con confirmación por proveedor, un pedido con una
  confirmación PARCIAL (un proveedor ya entregó, otro no) quedaba con `Any() == true` y
  jamás volvía a evaluarse, así que nunca se le recordaba lo que sí seguía pendiente. Se
  sacó esa condición de la consulta SQL (ahora solo filtra por `!RecordatorioEntregaEnviado`
  y algo Aprobado) y se movió la evaluación real a `EnviarRecordatoriosEntregaPendienteAsync`:
  por cada candidato calcula `ObtenerGruposDeEntregaAsync` y solo manda el recordatorio si
  queda al menos un grupo sin confirmar. De paso, el mensaje del recordatorio
  ("tiene N producto(s) aprobado(s)...") pasó de contar TODAS las líneas aprobadas del
  pedido a contar solo las de los grupos todavía pendientes — para no exagerar cuánto
  falta cuando ya se entregó una parte (el "ni de menos, ni de más" del enunciado de la
  tarea).

  Verificado con el Pedido #43 (2 líneas aprobadas: una con proveedor, otra sin ninguno):
  se simuló por SQL un envío real y su confirmación para el grupo con proveedor (dejando
  el grupo "sin proveedor" pendiente), confirmando en la tabla `Notificaciones` que el
  mensaje al solicitante decía exactamente "Ya te entregaron la parte de tu pedido #43
  correspondiente a Distribuciones Andinas, el [fecha]. Todavía falta el resto."; al
  confirmar también el segundo grupo desde el navegador, el mensaje pasó a "Tu pedido #43
  ya fue entregado por completo — lo último llegó el [fecha].". Para el recordatorio, se
  reconstruyó el mismo escenario de confirmación parcial y se adelantó `FechaResolucion`
  más de 72h; como el `RecordatorioPendientesHostedService` corre una vez al iniciar la
  app (sin esperar el intervalo de 1h), reiniciar el servidor alcanzó para disparar el job
  real (no simulado): se generó correctamente la notificación "El pedido #43 de
  usuario@catalogo.local tiene **1** producto aprobado hace más de 72h sin confirmar la
  entrega." (1, no 2 — confirma que solo cuenta lo pendiente) y `RecordatorioEntregaEnviado`
  quedó en 1 — antes de este fix, este pedido jamás habría entrado a la evaluación por
  tener ya una confirmación (aunque fuera parcial). Se limpiaron todos los datos de
  prueba y se restauró el estado original del Pedido #43 al terminar. Sin errores nuevos
  en el log del servidor (solo el falso positivo ya conocido de
  `HttpsRedirectionMiddleware`).
  Archivo(s): `ConfirmacionEntregaService.cs`, `SolicitudRepository.cs`

- [x] **9. Bug real: desactivar un Proveedor no le impedía seguir recibiendo correos** —
  Hecho (2026-09-24).

  El usuario pidió revisar por qué no estaban llegando correos "a donde quiero" y notó que
  no sabía qué pasaba cuando un proveedor queda desactivado — sospecha correcta, había un
  bug real.

  **Diagnóstico, confirmado con datos reales antes de tocar nada:** en la base de
  desarrollo, los 7 proveedores del catálogo estaban `Activo = 0`, pero 447 de sus 449
  asociaciones `ProductoProveedor` seguían `Activo = 1`. Causa: `ProveedorService.DesactivarAsync`
  nunca cascadeaba a sus asociaciones (mismo tipo de gap ya corregido para Empresa→Sede en
  `docs/PLAN_EMPRESAS_FILIALES.md`, Etapa 9, pero nunca replicado acá). Y aunque cascadeara,
  `PedidoNotificacionProveedorService` (`ObtenerProveedoresDisponiblesAsync`/
  `EnviarAProveedorAsync`) nunca revisaba `Proveedor.Activo` en ningún lado — los únicos 2
  chequeos `.Activo` de todo el archivo eran sobre la asociación, ninguno sobre el
  proveedor. Resultado: el sistema seguía ofreciendo (y permitiendo enviar correos reales
  a) proveedores ya desactivados, sin ningún aviso — explicación directa y verificada del
  reporte "no llegan a donde quiero".

  **Sobre la "tabla intermedia" que pidió el usuario para la relación muchos-a-muchos**:
  ya existe y funciona — `PedidoProveedor` (cabecera, uno por Solicitud+Proveedor) +
  `DetallePedidoProveedor` (línea por producto, con cantidad/código congelados al
  momento del envío) es exactamente eso. Se verificó con datos reales antes de proponer
  nada nuevo: la Solicitud #19 mandó "Abrasivo REGULAR" solo al proveedor "Suministros y
  Empaques del Norte", nunca al proveedor "Productos y Suministros S.A.S." aunque el
  resto de esa misma solicitud sí fue a ese otro proveedor — la regla "no partir un mismo
  producto entre dos proveedores" (tarea 2 de este plan, reforzada por la tarea 21 de
  `docs/PLAN_MEJORAS_UI_UX.md`) está funcionando correctamente. No se creó ninguna tabla
  nueva.

  **Arreglo, 3 partes:**
  1. `ProveedorService.DesactivarAsync`: cascada — desactiva también las asociaciones
     `ProductoProveedor` activas de ese proveedor (mismo patrón que Empresa→Sede).
  2. `PedidoNotificacionProveedorService`: chequeo defensivo agregado en
     `ObtenerProveedoresDisponiblesAsync` y `EnviarAProveedorAsync` — ahora exigen
     `a.Proveedor!.Activo`, no solo `a.Activo` de la asociación, para no depender
     únicamente de que la cascada se haya ejecutado (defensa en profundidad, por si los
     datos vuelven a divergir).
  3. Corrección de datos, autorizada explícitamente por el usuario antes de ejecutarse:
     `UPDATE` acotado (`ProductoProveedores.Activo = 0` donde el `Proveedor` asociado ya
     estaba `Activo = 0`) — 447 filas corregidas, verificado por SQL que no quedó ninguna
     asociación huérfana después.

  Agregué un test nuevo
  (`ObtenerProveedoresDisponibles_ProveedorDesactivado_NoApareceAunqueSuAsociacionSigaActiva`)
  que reproduce el bug exacto (asociación activa + proveedor inactivo) y confirma que
  `ObtenerProveedoresDisponiblesAsync` lo excluye y `EnviarAProveedorAsync` lo rechaza. Los
  16 tests anteriores del archivo siguen en verde.

  Verificado con `dotnet build` (0 errores/advertencias), `dotnet test` (17/17), la
  corrección de datos verificada por SQL (447 filas, 0 huérfanas restantes), y smoke test
  no interactivo de `/proveedores`, `/solicitudes/administrar` y `/solicitudes/bandeja`
  (sin excepciones en el log). **No se probó clic-por-clic en el navegador** (ver memoria
  `feedback-no-browser-testing`).
  Archivos: `Application/Proveedores/ProveedorService.cs`,
  `Application/Solicitudes/PedidoNotificacionProveedorService.cs`,
  `Application.Tests/Solicitudes/PedidoNotificacionProveedorServiceTests.cs`.

- [x] **10. Un proveedor desactivado no se veía en ningún lado ni se podía reactivar** —
  Hecho (2026-09-24).

  El usuario preguntó por qué no podía ver los proveedores desactivados en la UI —
  sospechaba que se habían borrado al desactivarlos.

  **Diagnóstico**: no se borró nada — confirmado por SQL, los 7 proveedores siguen en la
  tabla `Proveedores` con `Activo = 0`. El problema real es que `Proveedores.razor` nunca
  tuvo forma de verlos: `ProveedorRepository.ObtenerTodosAsync` filtraba
  `Where(p => p.Activo)` de forma fija, sin ningún parámetro para incluir inactivos, y
  `IProveedorService` **no tenía ningún `ReactivarAsync`** en absoluto — a diferencia de
  `Producto`, que ya había tenido exactamente este mismo problema y se corrigió en la
  tarea 18 de `docs/PLAN_MEJORAS_UI_UX.md` (reactivar + checkbox "Mostrar desactivados" +
  badge). Ese arreglo nunca se replicó para `Proveedor`.

  **Arreglo, mismo patrón que la tarea 18 de UI/UX:**
  - `IProveedorRepository`/`IProveedorService`.`ObtenerTodosAsync` gana un parámetro
    `incluirInactivos` (default `false`, no rompe a los callers existentes).
  - `IProveedorService.ReactivarAsync` nuevo (mismo patrón que `DesactivarAsync`, pone
    `Activo = true`) — **a propósito no reactiva sus asociaciones producto-proveedor**:
    reactivar el proveedor no implica que todo lo que tenía asociado siga siendo
    correcto, el Gestor las reactiva una por una desde `/proveedores/{id}/productos` (ya
    existía `ReactivarAsociacionAsync` para eso).
  - `Proveedores.razor` gana un checkbox "Mostrar desactivados" (mismo componente
    `HeaderActions` de `Card` recién estrenado en la tarea 24 de `docs/PLAN_MEJORAS_UI_UX.md`),
    fila atenuada (`opacity-60`) + badge "Desactivado" para las inactivas, y el botón
    "Desactivar" se reemplaza por "Reactivar" en esas filas.

  Verificado con `dotnet build` (0 errores/advertencias), `dotnet test` (17/17 sin
  regresiones) y smoke test no interactivo de `/proveedores` (sin excepciones en el log).
  **No se probó clic-por-clic en el navegador** (ver memoria `feedback-no-browser-testing`).
  Archivos: `Application/Proveedores/{IProveedorRepository,IProveedorService,ProveedorService}.cs`,
  `Infrastructure/Repositories/ProveedorRepository.cs`, `Proveedores.razor`.
