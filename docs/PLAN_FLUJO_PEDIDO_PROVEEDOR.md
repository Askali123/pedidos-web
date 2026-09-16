# Plan de mejoras — flujo pedido → aprobación → envío a proveedor

Checklist de trabajo, de mayor a menor prioridad, salida del análisis de punta a punta del
flujo (carrito → `CrearPedidoAsync` → Bandeja/Administrar → `ResolverAsync` → "Enviar a
proveedor" → `SmtpEmailSender`) hecho el 2026-09-16. Cada tarea se marca `[x]` al
completarse; no reordenar sin avisar — el orden es la prioridad acordada.

## P0 — Bugs de corrección (bloquean antes de seguir)

- [x] **1. Filtrar por estado antes de enviar al proveedor** — **Hecho.**
  `EnviarAProveedorAsync` y `ObtenerProveedoresDisponiblesAsync` ahora solo consideran
  líneas `Estado == Aprobada`; Pendiente/Rechazada quedan afuera del PDF/correo y no
  cuentan para armar las opciones del selector de proveedor. Si un pedido no tiene
  ninguna línea aprobada todavía, el desplegable "Enviar a proveedor" queda vacío con un
  mensaje explicando por qué ("Aprueba al menos una línea..."/"Este pedido no tiene
  líneas aprobadas..."), en vez de dejar mandar algo pendiente o rechazado por error.
  Verificado en el navegador: un pedido con una línea aprobada y una rechazada (mismo
  proveedor) mostró "Tiendas D1 · 1 prod." en el selector — no 2 — y un pedido totalmente
  pendiente no ofreció ningún proveedor para enviar.
  Archivo: `CatalogoPedidos.Application/Solicitudes/PedidoNotificacionProveedorService.cs`

- [x] **2. Confirmación antes de enviar un correo real** — **Hecho.**
  Elegir un proveedor en el desplegable ya no envía directo: abre un `ConfirmDialog`
  ("¿Enviar el pedido #N a "Proveedor" (K producto(s))? Se le manda un correo real con el
  detalle en PDF — no se puede deshacer.") con botones Cancelar/Enviar, mismo patrón que
  "Desactivar producto/proveedor". El envío real solo ocurre al confirmar.
  Verificado en el navegador: "Cancelar" no generó ningún correo (confirmado en el log);
  "Enviar" sí lo hizo y mostró el mensaje de éxito de siempre.
  Archivos: `Bandeja.razor`, `Administrar.razor` (Components/Pages/Solicitudes)

- [x] **3. Bloquear pedidos de productos desactivados** — **Hecho.**
  `CrearPedidoAsync` ahora revisa `Producto.Activo` por cada línea antes de crear el
  pedido; si algún producto ya no está activo, lanza `InvalidOperationException` con
  su nombre y no crea nada. `Carrito.razor` ya atrapaba `InvalidOperationException` y
  la muestra en un `Alert`, así que no hizo falta tocar la UI.
  Verificado en el navegador: agregué un producto al carrito, lo desactivé desde el
  Catálogo (misma sesión/circuito, sin recargar), y al enviar la solicitud mostró
  ""PRUEBA filtro estado B" ya no está disponible en el catálogo." sin crear el pedido.
  Archivo: `CatalogoPedidos.Application/Solicitudes/SolicitudService.cs`
  Archivo: `CatalogoPedidos.Application/Solicitudes/SolicitudService.cs`

## P1 — Trazabilidad / auditoría

- [ ] **4. Guardar quién envió el correo**
  `NotificacionProveedor` no guarda `GestorId`/`GestorNombre` (a diferencia de
  `SolicitudProducto`, que sí guarda quién aprobó/rechazó). Agregar los campos +
  migración, y pasarlos desde la UI (mismo patrón que `ResolverAsync`).
  Archivos: `NotificacionProveedor.cs` (Domain), `PedidoNotificacionProveedorService.cs`,
  `Bandeja.razor`, `Administrar.razor`

- [ ] **5. Mostrar el historial de envíos en la UI**
  `IPedidoNotificacionProveedorService.ObtenerEnviosAsync` ya existe pero ningún `.razor`
  lo llama — los envíos quedan grabados pero invisibles. Agregar algo tipo "Enviado a
  Tiendas D1 el 16/09 04:40 (Gestor de Catálogo)" en la tarjeta del pedido.
  Archivos: `Bandeja.razor`, `Administrar.razor`

## P2 — Pulido de UX

- [ ] **6. Cerrar el menú desplegable tras elegir proveedor**
  Hoy queda abierto después del clic (limitación del componente `Dropdown` genérico).

- [ ] **7. Indicar cuando un producto tiene más de un proveedor**
  En "Administrar pedidos" sin filtro, la columna "Proveedor · Código" muestra solo el
  preferido, sin avisar que el desplegable "Enviar a proveedor" tiene más opciones para
  ese pedido.
  Archivo: `Administrar.razor`

- [ ] **8. Validar formato de email de proveedor**
  `Proveedor.Email`/`CrearProveedorDto.Email` son texto libre, sin `[EmailAddress]` ni
  validación en el formulario. Un correo mal escrito solo se descubre cuando falla el
  envío real.
  Archivos: `CrearProveedorDto.cs`, `Proveedores.razor`

- [ ] **9. Comentario del gestor al aprobar/rechazar**
  El dominio ya soporta `ResolverSolicitudDto.ComentarioGestor`, pero la Bandeja nunca lo
  pide — el solicitante no se entera de por qué se rechazó su pedido.
  Archivo: `Bandeja.razor`

- [ ] **10. Persistencia del carrito**
  `CarritoState` vive en memoria del circuito Blazor Server — se pierde al recargar o si
  se cae la conexión. Persistir en `localStorage` o en BD por usuario.
  Archivo: `CarritoState.cs`

## P3 — Riesgo / seguridad

- [ ] **11. Límite de tasa en envíos reales**
  No hay protección contra reenviar el mismo pedido al mismo proveedor en loop — antes
  era un log simulado, ahora es correo real a un tercero.

## P4 — Funcionalidades nuevas

- [ ] **12. Pantalla de historial de envíos a proveedores**
  Filtrable por proveedor/fecha, reutilizando `NotificacionProveedor` una vez tenga
  `GestorId` (tarea 4).

- [ ] **13. Copia (CC/BCC) al gestor en el correo saliente**
  Para que quede en su propia bandeja como respaldo.

- [ ] **14. Reply-To con el correo del gestor que envió**
  Hoy el proveedor respondería al remitente genérico configurado en `Smtp:RemitenteEmail`,
  no a una persona real.

- [ ] **15. Conectar la alerta de stock bajo con "enviar a proveedor preferido"**
  Acción directa desde la notificación de stock bajo hacia el envío ya existente.

- [ ] **16. Adjuntar también Excel (opcional)**
  Se dejó afuera a propósito la primera vez (se eligió solo PDF); el generador de Excel
  ya existe (`IExcelExportService.ExportarSolicitudesPorProveedor`), así que es agregar
  el adjunto si más adelante se quiere.

---

*Se trabaja de arriba hacia abajo, una tarea a la vez. Al terminar una, se marca `[x]` y
se resume qué cambió (como ya se hace en `MEJORAS_PROPUESTAS.md`) antes de pasar a la
siguiente.*
