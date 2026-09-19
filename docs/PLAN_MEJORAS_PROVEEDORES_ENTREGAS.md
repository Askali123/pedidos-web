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

- [ ] **1. Mostrar qué productos le corresponden a cada proveedor en el selector de envío**
  El dropdown "Enviar a proveedor" (Bandeja/Administrar) hoy solo dice
  "Proveedor X · N líneas". Agregar el detalle (nombres de producto, o al menos un
  tooltip/expandible) para que el gestor no elija a ciegas — especialmente importante
  cuando el pedido mezcla categorías con proveedores distintos.

- [ ] **2. Evitar enviarle el mismo producto a dos proveedores distintos**
  `ObtenerProveedoresDisponiblesAsync`/`EnviarAProveedorAsync` no descartan una línea ya
  cubierta por un envío previo a OTRO proveedor. Ajustar para que, una vez que una línea
  fue efectivamente enviada (hay un `NotificacionProveedor` que la incluye), deje de
  contarse/ofrecerse para los demás proveedores candidatos de ese mismo producto.

## P1 — Confirmación de entrega por proveedor

- [ ] **3. Migrar `ConfirmacionEntrega` de 1:1 a 1:muchos por `Pedido`, con `ProveedorId` opcional**
  Cambiar `Pedido.ConfirmacionEntrega` (singular) a `Pedido.ConfirmacionesEntrega`
  (colección); agregar `ConfirmacionEntrega.ProveedorId` (`int?`) + navegación
  `Proveedor? Proveedor`; agregar `RecibidoPorNombre` (`string?`). Quitar el índice único
  en `PedidoId` (ya no aplica). Migración EF Core nueva.

- [ ] **4. Reescribir el servicio de confirmación de entrega para la nueva granularidad**
  `IConfirmacionEntregaService`/`ConfirmacionEntregaService`: `ObtenerPorPedidoAsync`
  devuelve una lista (una por proveedor + posible "sin proveedor"); `ConfirmarAsync`
  recibe qué proveedor (o `null`) se está confirmando, valida que existan líneas
  Aprobadas de ese proveedor específico sin confirmar todavía, y que no haya ya una
  confirmación para ese mismo (pedido, proveedor). Agregar un método que calcule el
  progreso agregado del pedido (cuántos "grupos" de entrega tiene y cuántos ya están
  confirmados).

- [ ] **5. UI: un botón "Confirmar entrega" por proveedor pendiente, no uno solo por pedido**
  En Bandeja.razor y Administrar.razor, reemplazar el botón único por uno por cada
  proveedor (o "sin proveedor asociado") que tenga líneas aprobadas todavía sin
  confirmar. Cada uno abre el mismo modal ya existente, pero scopeado a esas líneas.

- [ ] **6. Agregar el campo "Quién lo recibió" al formulario de confirmación**
  Input de texto libre y opcional en el modal "Confirmar entrega", junto a los campos ya
  existentes (dirección, coincide con la indicada, observaciones).

- [ ] **7. Progreso agregado visible en vez de un badge binario**
  `EntregaBadge.razor` (o su reemplazo) debe reflejar "2/3 proveedores entregados" en
  lugar de un único ✓/nada, en Bandeja, Administrar y `MisSolicitudes.razor` (donde el
  solicitante ve el estado de sus propios pedidos).

- [ ] **8. Actualizar la notificación al solicitante y el recordatorio de entrega pendiente**
  El mensaje "Tu pedido #X fue entregado..." debe aclarar A QUÉ proveedor corresponde esa
  entrega cuando el pedido tiene más de un grupo. `EnviarRecordatoriosEntregaPendienteAsync`
  (recordatorio a las 72h) debe evaluarse por grupo de entrega pendiente, no por pedido
  completo, para no re-notificar de más ni de menos.
