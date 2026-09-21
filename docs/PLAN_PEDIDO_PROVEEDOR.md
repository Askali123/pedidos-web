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

| Producto | Proveedor | Código del proveedor |
|---|---|---|
| Tornillo 10mm | Proveedor A | `TOR-001` |
| Tornillo 10mm | Proveedor B | `T-10` |

## 1. Diagnóstico del modelo actual

| Concepto de negocio | Entidad | Tabla | Estado |
|---|---|---|---|
| Solicitud (cabecera) | `Pedido` | `Pedidos` | Existe |
| Detalle de solicitud | `SolicitudProducto` | `Solicitudes` | Existe |
| Producto | `Producto` | `Productos` | Existe |
| Proveedor | `Proveedor` | `Proveedores` | Existe |
| Relación Producto↔Proveedor (con código propio) | `ProductoProveedor` | `ProductoProveedores` | Existe |
| **PedidoProveedor (cabecera del pedido al proveedor)** | — | — | **No existe** |
| **DetallePedidoProveedor** | — | — | **No existe** |

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

- [ ] **G1. No existe `PedidoProveedor` ni `DetallePedidoProveedor`** — el pedido al proveedor
      no queda como documento persistido (imposible auditar "qué se le pidió a X" en el pasado).
- [ ] **G2. `CodigoProveedor` no es snapshot** — un cambio de código en el catálogo altera
      pedidos/reportes viejos.
- [ ] **G3. No existe "editar asociación"** — `AsociarProveedorDto` solo da de alta;
      `QuitarAsociacionAsync` hace **DELETE físico**. No hay flag `Activo` en `ProductoProveedor`.
- [ ] **G4. No hay decisión explícita por línea en el pedido** — hoy no se puede "quitar un
      producto de ESTE pedido a proveedor" por decisión del gestor (la exclusión es automática
      e implícita). Quitar de un pedido NO debe tocar el catálogo.
- [ ] **G5. No hay estado del documento** — más allá de `ConfirmacionEntrega` posterior.

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

| Campo en DetallePedidoProveedor | Tipo | Motivo |
|---|---|---|
| `PedidoProveedorId`, `ProveedorId`, `ProductoId` | Referencia (FK) | joins, badges, filtros, navegación |
| `ProductoNombre` | **Snapshot** | identificar en PDF/histórico aunque renombren/desactiven el producto |
| `Cantidad` | **Snapshot** | refleja lo acordado en el momento |
| `CodigoProveedor` | **Snapshot** | requisito central: cambiar el SKU no altera pedidos pasados |
| `PrecioProveedor` | Snapshot opcional | útil para reportes de gasto; omitir si no se necesita ya |
| `UnidadMedida` | Snapshot opcional | barato, útil en PDF |

`PedidoProveedor` se crea **inmutable al primer envío**: mismo pedido+proveedor reusa el mismo
documento, y el reenvío crea otra `NotificacionProveedor`, no otro documento. Respeta el
cooldown de 5 min actual.

## 6. Reglas de negocio

- [ ] Solo líneas `Aprobada` entran al `PedidoProveedor` (regla actual, se mantiene).
- [ ] Producto sin asociación con el proveedor → no entra, pero se **avisa** en el panel
      (badge "sin proveedor").
- [ ] Producto con varios proveedores → el gestor elige (dropdown actual, ya existe).
- [ ] Quitar una línea de un `PedidoProveedor` concreto → flag `Excluido` en el detalle;
      **jamás** toca `ProductoProveedor`.
- [ ] Eliminar/desactivar una asociación del catálogo → no altera `PedidoProveedor` previos
      (snapshots); sí oculta futuros.
- [ ] Una línea enviada a otro proveedor del mismo pedido → excluida del proceso (regla
      actual, ahora explicitada en el detalle).

## 7. Decisiones que faltan validar antes de codear

- [ ] ¿`PedidoProveedor` inmutable o editable? (recomendado: inmutable; lo nuevo = otra versión)
- [ ] ¿Estado del documento? (recomendado: derivar de `NotificacionProveedor` + `ConfirmacionEntrega`,
      sin campos nuevos de estado)
- [ ] ¿Precio y UnidadMedida se snapshottean ya o se dejan para después?
- [ ] Cómo migrar los envíos históricos (no tienen documento): aceptar que un reenvío crea el
      documento con datos actuales.

## 8. Plan por etapas

- **Etapa 0 — Modelo (ESTE DOCUMENTO):** validar puntos 5/6/7 antes de migrar.
- **Etapa 1 — Persistencia:** migración con `PedidoProveedor` + `DetallePedidoProveedor`
  (+ `Activo` en `ProductoProveedor`, FK `NotificacionProveedor.PedidoProveedorId`).
- **Etapa 2 — Servicio de pedido a proveedor:** al `EnviarAProveedorAsync`, perseguir el
  documento con snapshots a partir de la intersección ya calculada; reutilizarlo para
  PDF/Excel/email.
- **Etapa 3 — Administración de asociaciones:** editar + desactivar (no DELETE) en las
  pantallas de producto-y-proveedor.
- **Etapa 4 — UI del pedido a proveedor:** checkboxes por línea en la confirmación (excluir),
  historial de envíos con detalle real, avisos "sin proveedor".
- **Etapa 5 — Reportes/casos límite:** PDF/Excel leen snapshots; alertas de stock y badges
  coherentes.

## 9. Casos de prueba (xUnit — hoy no hay ningún test en el repo)

- [ ] Intersección: pedido con A,B,C + Proveedor X con A,B → documento solo con A,B y códigos.
- [ ] Producto con varios proveedores → documento con el proveedor elegido; el otro sigue libre.
- [ ] Producto sin proveedor → excluido + aviso; el resto del pedido sigue.
- [ ] Cambio de código en catálogo después del pedido → el documento viejo conserva el código viejo.
- [ ] Desactivar asociación después del pedido → histórico intacto; pedidos nuevos lo excluyen.
- [ ] Quitar línea del pedido → `Excluido = true`; catálogo intacto.
- [ ] Reenvío en cooldown → rechazado con motivo y sin duplicar documento.

## 10. API y frontend

- **Backend:** nuevo `IPedidoProveedorService` (crear/listar) sobre la base del
  `PedidoNotificacionProveedorService`; DTOs `PedidoProveedorDto` / `DetallePedidoProveedorDto`;
  `ActualizarAsociacionAsync` + `DesactivarAsociacionAsync` en proveedores.
- **Frontend:** "Enviar a proveedor" en `Administrar.razor` → confirmación con checkbox por
  línea → persiste el documento → recién ahí el correo. `HistorialEnvios` lee los detalles
  reales. Badges "+N más" ganan redirección a la vista por proveedor.
- **Minimal APIs:** solo endpoints internos nuevos, nada público.