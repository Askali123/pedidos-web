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

- [x] **4. Guardar quién envió el correo** — **Hecho.**
  `NotificacionProveedor` ganó `GestorId`/`GestorNombre` (nullable, mismo patrón que
  `SolicitudProducto`) + migración `AgregarGestorANotificacionProveedor`.
  `EnviarAProveedorAsync` ahora recibe esos datos y los guarda al crear el registro.
  `Administrar.razor` no tenía forma de saber quién era el gestor actual (no inyectaba
  `UserManager` ni tenía el `CascadingParameter` de `AuthenticationState`) — se le agregó
  el mismo helper `DatosGestor` que ya usaba `Bandeja.razor` para `ResolverAsync`.
  Verificado con una consulta directa a la base de datos tras un envío real: el registro
  quedó con `GestorId` y `GestorNombre = "Gestor de Catálogo"` correctamente guardados.
  Archivos: `NotificacionProveedor.cs` (Domain), `PedidoNotificacionProveedorService.cs`,
  `Bandeja.razor`, `Administrar.razor`

- [x] **5. Mostrar el historial de envíos en la UI** — **Hecho.**
  Cada tarjeta de pedido (Bandeja y Administrar) muestra ahora una línea por envío ya
  hecho: "✓ Enviado a Tiendas D1 · 16/09/2026 05:49 · Gestor de Catálogo", debajo de la
  fecha/comentario del pedido. Se recarga tras cada envío nuevo para que aparezca al
  instante, sin recargar la página.
  Verificado en el navegador: pedidos con envíos previos a la tarea 4 (sin gestor
  guardado) muestran "-" en vez de romperse; los nuevos muestran el nombre del gestor;
  el orden es el más reciente primero.
  Archivos: `Bandeja.razor`, `Administrar.razor`

## P2 — Pulido de UX

- [x] **6. Cerrar el menú desplegable tras elegir proveedor** — **Hecho.**
  `Dropdown.razor` (componente compartido) ganó un parámetro opcional `CloseOnClick`
  (default `false`, sin romper otros usos): si está en `true`, cualquier clic dentro del
  contenido cierra el menú. Se activó solo en los desplegables "Enviar a proveedor" de
  Bandeja y Administrar. `Notificaciones.razor` sigue sin ese parámetro (usa su propio
  `dropdown?.Cerrar()` manual solo al abrir una notificación) — "Marcar todas como
  leídas" debía seguir dejando el menú abierto, y así quedó.
  Verificado en el navegador: elegir un proveedor cierra el menú antes de mostrar el
  `ConfirmDialog` (ya no queda superpuesto); en Notificaciones, "Marcar todas como
  leídas" sigue dejando el menú abierto (badge desaparece, panel no se cierra).
  Archivo: `Components/UI/Dropdown.razor`, `Bandeja.razor`, `Administrar.razor`

- [x] **7. Indicar cuando un producto tiene más de un proveedor** — **Hecho.**
  Nuevo `IProveedorService.ContarProveedoresPorProductoAsync` (cuenta asociaciones por
  producto). Cuando la columna "Proveedor · Código" muestra el preferido (Bandeja
  siempre, Administrar sin filtro de proveedor) y el producto tiene más de un proveedor
  asociado, aparece un badge "+N más" al lado del código — mismo criterio en ambas
  pantallas.
  Verificado en el navegador: asocié un segundo proveedor a un producto de prueba y
  apareció "+1 más" junto a su código; un producto con un solo proveedor no muestra nada.
  Archivos: `IProveedorService.cs`/`ProveedorService.cs`, `Bandeja.razor`, `Administrar.razor`

- [x] **8. Validar formato de email de proveedor** — **Hecho.**
  `CrearProveedorDto.Email` ganó `[EmailAddress]` (valida en vivo al escribir, gracias a
  `DataAnnotationsValidator` + `ValidationMessage` — mismo patrón que `Login.razor`).
  Además, `ProveedorService` valida el mismo formato del lado del servidor con el mismo
  `EmailAddressAttribute` (una sola regla, no duplicada) — por si el servicio se llama
  desde otro lado que no sea este formulario. El formulario "Nuevo proveedor" (que antes
  no atrapaba errores) ahora los muestra en un `Alert`, igual que "Editar".
  Verificado en el navegador: un email sin formato válido bloquea tanto la creación como
  la edición, mostrando el error en el campo; con un email válido, ambos flujos guardan
  normalmente.
  Archivos: `ProveedorDto.cs`, `ProveedorService.cs`, `Proveedores.razor`

- [x] **9. Comentario del gestor al aprobar/rechazar** — **Hecho.**
  "Aprobar"/"Rechazar" (línea y pedido completo) ahora abren un modal con un campo de
  comentario opcional antes de resolver, en vez de disparar directo — mismo `Modal` que ya
  usaba "Configurar alerta de stock" en el Catálogo. El comentario viaja en
  `ResolverSolicitudDto.ComentarioGestor`, que el dominio ya soportaba.
  Alcance ampliado sobre lo previsto: `MisSolicitudes.razor` **tampoco mostraba nunca**
  `ComentarioGestor` — sin eso, capturar el comentario no le llegaba al solicitante, que
  era el objetivo real de la tarea. Se agregó debajo del estado de cada línea.
  Verificado en el navegador: rechacé una solicitud con comentario ("No hay presupuesto
  disponible...") y apareció en "Mis solicitudes" del solicitante bajo el badge
  "Rechazada"; aprobar sin comentario sigue funcionando igual de simple, sin fricción
  extra cuando no hace falta explicar nada.
  Archivos: `Bandeja.razor`, `MisSolicitudes.razor`

- [x] **10. Persistencia del carrito** — **Hecho.**
  Se eligió `localStorage` (sobre BD) — resuelve el problema real (recarga forzada, caída
  de la conexión SignalR) sin necesitar tabla/migración nueva. Namespaced por
  `usuarioId` (`carrito:{usuarioId}`) para que dos cuentas en el mismo navegador no se
  mezclen. Solo guarda `ProductoId`+`Cantidad`, nunca el `Producto` completo — al
  restaurar se vuelve a pedir cada uno a `IProductoService`, así el precio/stock/nombre
  siempre sale actualizado, y un producto desactivado mientras tanto simplemente no
  vuelve (mismo criterio que la tarea 3). La restauración se dispara una sola vez por
  circuito desde `Layout/Carrito.razor` (`OnAfterRenderAsync(firstRender)`, el único
  momento en que Blazor Server ya tiene JS interop disponible), y cada mutación
  (agregar/quitar/cambiar cantidad/vaciar) se guarda en segundo plano (best-effort — si
  falla, el carrito en memoria sigue funcionando igual esa sesión).
  Verificado en el navegador: agregué 2 productos, forcé una recarga completa de la
  página (nuevo circuito de Blazor Server) y el carrito reapareció con ambos productos
  y cantidades intactas; confirmé también el contenido real de `localStorage`
  (`carrito:{id}` → `[{"ProductoId":11,"Cantidad":1},...]`) y que "Vaciar carrito" lo
  deja en `[]`.
  Archivos: `CarritoState.cs`, `Components/Layout/Carrito.razor`

## P3 — Riesgo / seguridad

- [x] **11. Límite de tasa en envíos reales** — **Hecho.**
  `EnviarAProveedorAsync` ahora revisa, antes de armar el correo, el envío más reciente
  a ese mismo proveedor para ese mismo pedido (reutilizando `envios.ObtenerPorPedidoAsync`,
  que ya venía ordenado por fecha descendente — sin query nueva). Si pasaron menos de 5
  minutos (`CooldownReenvio`), corta ahí: no arma el PDF, no llama al `IEmailSender` y no
  crea un registro nuevo en `NotificacionProveedor` — devuelve
  `Enviado = false` con el motivo y los minutos restantes, que la UI ya sabía mostrar
  (mismo mecanismo que "El proveedor no tiene correo registrado."). Se eligió un cooldown
  fijo simple (no un límite de cantidad por hora) porque el problema real es el loop de
  clics accidental, no un volumen alto legítimo.
  Verificado en el navegador: envié el pedido #53 a "Tiendas D1" (envío real, quedó
  registrado a las 08:08) y al intentar reenviarlo de inmediato al mismo proveedor
  apareció "No se pudo enviar a Tiendas D1: Ya se envió este pedido a Tiendas D1 hace
  poco. Espera 4 minuto(s) antes de reenviarlo." — sin crear una fila nueva de envío (el
  historial del pedido siguió mostrando solo los envíos reales). Para confirmar que no se
  rompió el caso normal, envié el pedido #48 (sin ningún envío previo) a "Distribuciones
  Andinas" y funcionó igual que siempre.
  Archivo: `CatalogoPedidos.Application/Solicitudes/PedidoNotificacionProveedorService.cs`

## P4 — Funcionalidades nuevas

- [x] **12. Pantalla de historial de envíos a proveedores** — **Hecho.**
  Nueva pantalla `/solicitudes/historial-envios` (solo Gestor), enlazada en el sidebar
  bajo "Gestión". Reutiliza `NotificacionProveedor` tal cual quedó tras la tarea 4 (ya
  tenía `GestorId`/`GestorNombre`) — no hizo falta tocar el dominio. Se agregó
  `INotificacionProveedorRepository.BuscarAsync` + `FiltroEnviosProveedorDto`
  (`ProveedorId`, `FechaDesde`, `FechaHasta`), mismo patrón que
  `ISolicitudRepository.BuscarAsync`/`FiltroSolicitudesDto` que ya usaba Administrar
  pedidos. La tabla muestra fecha, pedido, proveedor, correo real al que se mandó,
  cantidad de líneas, gestor y un acceso directo al PDF del pedido; paginada de a 20.
  Verificado en el navegador: sin filtros mostró los 18 envíos reales hechos hasta ahora
  (ordenados del más reciente); filtrar por "Tiendas D1" los redujo a 5 (todos de ese
  proveedor); poner "Desde" en un día futuro mostró el `EmptyState` "Sin envíos"; y
  "Limpiar" restauró los 18.
  Archivos: `FiltroEnviosProveedorDto.cs`, `INotificacionProveedorRepository.cs`,
  `NotificacionProveedorRepository.cs`, `IPedidoNotificacionProveedorService.cs`,
  `PedidoNotificacionProveedorService.cs`, `HistorialEnvios.razor` (nuevo),
  `Components/UI/Icon.razor` (ícono `clock` nuevo), `Components/Layout/Sidebar.razor`

- [x] **13. Copia (CC/BCC) al gestor en el correo saliente** — **Hecho.**
  `IEmailSender.EnviarAsync` ganó un parámetro opcional `copiaA` (CC, no BCC — se eligió CC
  a propósito: así el proveedor también ve que hay copia, más transparente que una copia
  oculta). `PedidoNotificacionProveedorService.EnviarAProveedorAsync` recibe ahora
  `gestorEmail` (opcional) y lo pasa como CC; se omite si coincide con el destinatario,
  para no duplicar el correo. `Bandeja.razor`/`Administrar.razor` ya tenían un helper
  `DatosGestor` que resolvía `GestorId`/`GestorNombre` desde el `ApplicationUser` actual —
  se le agregó `GestorEmail` (mismo objeto, no hizo falta una consulta nueva) y se
  propaga en la llamada a `EnviarAProveedorAsync`. `SmtpEmailSender` agrega el CC al
  `MailMessage` real; `LoggingEmailSender` lo deja en el log simulado igual que el resto.
  Verificado en el navegador: envié el pedido #47 (15 líneas) a "Distribuciones Andinas"
  logueado como `gestor@catalogo.local`, el envío fue exitoso, y el log de la app confirmó
  `Correo enviado a vennov34@gmail.com (CC: gestor@catalogo.local) — Pedido de
  reabastecimiento #47 — Distribuciones Andinas`.
  Archivos: `IEmailSender.cs`, `SmtpEmailSender.cs`, `LoggingEmailSender.cs`,
  `IPedidoNotificacionProveedorService.cs`, `PedidoNotificacionProveedorService.cs`,
  `Bandeja.razor`, `Administrar.razor`

- [x] **14. Reply-To con el correo del gestor que envió** — **Hecho.**
  Reutilizó el mismo dato que ya viajaba para la tarea 13 (`gestorEmail`/`copiaA`) — no
  hizo falta un parámetro nuevo ni tocar la UI. `SmtpEmailSender` ahora también agrega
  `mensaje.ReplyToList` con ese correo (misma condición que el CC: se omite si coincide
  con el destinatario). `LoggingEmailSender` refleja el mismo dato en el log simulado.
  Verificado en el navegador: envié el pedido #46 a "Distribuciones Andinas" (logueado
  como `gestor@catalogo.local`) y el log de la app confirmó `Correo enviado a
  vennov34@gmail.com (CC: gestor@catalogo.local, Reply-To: gestor@catalogo.local) —
  Pedido de reabastecimiento #46 — Distribuciones Andinas`.
  Archivos: `IEmailSender.cs`, `SmtpEmailSender.cs`, `LoggingEmailSender.cs`

- [x] **15. Conectar la alerta de stock bajo con "enviar a proveedor preferido"** — **Hecho.**
  No existe (ni tenía sentido crear) un "enviar directo desde la alerta" sin pasar por un
  pedido — el envío real siempre va sobre líneas Aprobadas de un `Pedido` existente. En su
  lugar, la alerta ahora es un deep link a "Administrar pedidos" ya filtrado por ESE
  producto y, si tiene uno, su proveedor preferido (`ObtenerPreferidosPorProductosAsync`,
  mismo criterio ya usado en el resto del sistema) — al gestor le queda el botón "Enviar a
  proveedor" que ya existía a un clic, en vez de tener que ir a buscar manualmente el
  pedido correspondiente. `SolicitudService.AlertarStockBajoAsync` arma la URL
  (`/solicitudes/administrar?productoId=X&proveedorId=Y`, o solo `productoId` sin
  preferido) en lugar de apuntar siempre a `/catalogo`; `Administrar.razor` la lee con
  `[SupplyParameterFromQuery]` y pre-carga esos dos filtros antes de la primera búsqueda.
  Verificado en el navegador: creé un producto de prueba (stock 5, mínimo 3, proveedor
  preferido Tiendas D1), pedí 2 unidades como solicitante y las aprobé como gestor — llegó
  la notificación "Stock bajo" ("...quedó con stock 3..."); al hacer clic aterrizó en
  `/solicitudes/administrar?productoId=236&proveedorId=4` con "PRUEBA alerta tarea 15" y
  "Tiendas D1" ya seleccionados en los filtros, mostrando el pedido #54 aprobado con
  "Enviar a proveedor" listo para usar (abrí el diálogo de confirmación para confirmar que
  el botón funciona; no lo envié de verdad por ser un producto de prueba).
  Archivos: `SolicitudService.cs`, `Administrar.razor`

- [x] **16. Adjuntar también Excel (opcional)** — **Hecho.**
  `IEmailSender.EnviarAsync` pasó de un `adjunto` único a `adjuntos` (lista) — el único
  caller (`PedidoNotificacionProveedorService`) arma esa lista con el PDF (siempre) y,
  si `incluirExcel` viene en `true`, también el Excel vía el generador que ya existía
  (`IExcelExportService.ExportarSolicitudesPorProveedor`, mismo patrón que el PDF). Es
  opcional por decisión explícita del gestor en cada envío, no una config global: el
  `ConfirmDialog` de "Enviar a proveedor" ganó un checkbox "También adjuntar el detalle
  en Excel" (para eso, `ConfirmDialog` ganó un slot `ExtraContent` reusable — parámetro
  nuevo, no rompe los otros 4 usos existentes del componente). El cuerpo del correo
  también menciona el Excel cuando se incluye.
  Verificado en el navegador: envié el pedido #54 con el checkbox marcado y el log
  confirmó ambos adjuntos (`Pedido-54-Tiendas D1.pdf, Pedido-54-Tiendas D1.xlsx`); envié
  el pedido #48 sin marcarlo (default) y el log mostró solo el PDF — confirmando que el
  comportamiento previo (solo PDF) sigue siendo el default.
  Archivos: `IEmailSender.cs`, `SmtpEmailSender.cs`, `LoggingEmailSender.cs`,
  `IPedidoNotificacionProveedorService.cs`, `PedidoNotificacionProveedorService.cs`,
  `Components/UI/ConfirmDialog.razor`, `Bandeja.razor`, `Administrar.razor`

---

**Las 16 tareas de este plan están completas.** Cada una quedó verificada en vivo en el
navegador y documentada arriba con el detalle de qué se hizo y cómo se probó.

---

*Se trabaja de arriba hacia abajo, una tarea a la vez. Al terminar una, se marca `[x]` y
se resume qué cambió (como ya se hace en `MEJORAS_PROPUESTAS.md`) antes de pasar a la
siguiente.*
