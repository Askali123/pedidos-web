# Propuestas de mejora — CatalogoPedidos

Este documento recoge oportunidades de mejora identificadas sobre el estado actual del
proyecto (flujo completo funcionando: catálogo, proveedores, solicitudes por carrito,
importación desde Excel, exportación a PDF/Excel, notificaciones en tiempo real).

No son tareas obligatorias — son candidatas a priorizar según hacia dónde quieras llevar
el proyecto (seguir aprendiendo conceptos nuevos vs. acercarlo a algo "production-ready").

---

## 1. Modelo de datos / dominio

- ~~**"Pedido" como entidad cabecera.**~~ **Hecho.** Se agregó `Pedido` (cabecera con
  `Comentario` único) y `SolicitudProducto.PedidoId`; la aprobación/rechazo sigue siendo
  por línea (un gestor puede aprobar unos productos del pedido y rechazar otros), pero
  ahora se ven agrupados en `Bandeja.razor` y `MisSolicitudes.razor`, con un botón
  "Aprobar todo/Rechazar todo" por pedido (`ISolicitudService.ResolverPedidoAsync`) y
  exportación a PDF de todo el pedido (`/api/pedidos/{id}/pdf`). Se hizo una migración
  con backfill (`AgregarPedido`) que convirtió cada `SolicitudProducto` existente en su
  propio `Pedido` de 1 ítem, sin perder el `Comentario` que antes vivía en la línea.
- ~~**Descuento de stock al aprobar.**~~ **Hecho.** `SolicitudService.DescontarStockAsync`
  resta `Cantidad` del `Producto.Stock` correspondiente cada vez que se aprueba una
  `SolicitudProducto` (individual o vía "Aprobar todo"). Rechazar no descuenta nada.
  Decisión de negocio: **no bloquea** la aprobación si no alcanza el stock — se permite
  que quede en negativo como señal de que hay que reponer, en vez de obligar al gestor a
  rechazar. El catálogo (`Catalogo.razor`) resalta en rojo el stock negativo para que se
  note a simple vista. Esto deja la puerta abierta para el siguiente punto (alerta de
  stock bajo/negativo).
- ~~**Alerta de stock bajo.**~~ **Hecho.** `Producto.StockMinimo` (opcional, por producto —
  no global) se configura al crear el producto o desde el ícono de lápiz junto al stock en
  `Catalogo.razor` (modal, solo Gestor). `SolicitudService.DescontarStockAsync` dispara una
  `Notificacion` tipo `StockBajo` solo en la **transición** hacia stock bajo (stock anterior
  > mínimo y stock nuevo ≤ mínimo) — evita reenviar la misma alerta en cada aprobación
  posterior mientras el stock siga bajo. El catálogo muestra una insignia "⚠ Bajo" junto al
  número cuando `Stock ≤ StockMinimo`.
- ~~**Recordatorio de solicitudes pendientes.**~~ **Hecho.** Se agregó
  `RecordatorioPendientesHostedService` (`BackgroundService`, primer job en segundo plano
  de la app): corre una vez al iniciar y luego cada hora, sin depender de que el gestor
  tenga la Bandeja abierta. `ISolicitudService.EnviarRecordatoriosPendientesAsync` agrupa
  las solicitudes pendientes por `Pedido`, y si un pedido lleva más de 48h y todavía no se
  le avisó (`Pedido.RecordatorioEnviado`), notifica a los gestores **una sola vez** — no
  repite el aviso en cada corrida mientras siga sin resolverse, siguiendo el mismo patrón
  de "notificar solo en la transición" que ya se usaba para stock bajo.
- ~~**Editar/desactivar proveedores.**~~ **Hecho.** `IProveedorService.ActualizarAsync` edita
  los datos de contacto y `DesactivarAsync` pone `Proveedor.Activo = false` (el repositorio
  ya filtraba por ese campo, solo faltaba cómo apagarlo). En `Proveedores.razor`: "Editar"
  abre un modal pre-cargado con los datos actuales, y "Desactivar" pide confirmación
  (`ConfirmDialog`) antes de aplicar — deja de aparecer en la lista y para asociarlo a
  productos nuevos, pero sus asociaciones `ProductoProveedor` existentes se conservan
  intactas (verificado: los productos de un proveedor desactivado se ven igual en
  `/proveedores/{id}/productos`).
- ~~**Filtrar y exportar el catálogo por proveedor, mostrando código interno y código del
  proveedor.**~~ **Hecho.** `Catalogo.razor` (solo Gestor) ahora tiene un tercer filtro
  "Todos los proveedores" junto a texto/categoría. Al elegir uno, la tabla cambia de fuente
  de datos — de `IProductoService.ObtenerCatalogoAsync` a
  `IProveedorService.ObtenerProductosDeProveedorAsync` (que ya traía la asociación
  `ProductoProveedor` con su `CodigoProveedor`) — y aparecen dos columnas nuevas: **Código
  interno** (el `Producto.Id` que el sistema asigna solo, independiente del proveedor) y
  **Código proveedor** (`ProductoProveedor.CodigoProveedor`, el código con el que ESE
  proveedor identifica el producto). El buscador de texto, al filtrar por proveedor,
  también busca por ese código. El botón "Exportar a PDF" arma la URL con los filtros
  activos (`texto`, `categoria`, `proveedorId` — antes ignoraba cualquier filtro y siempre
  exportaba todo el catálogo) y, cuando hay proveedor elegido, genera un PDF con las mismas
  dos columnas de código vía el nuevo `IPdfExportService.ExportarCatalogoPorProveedor`. Por
  seguridad, el endpoint `/api/catalogo/pdf` ignora `proveedorId` si quien lo pide no tiene
  rol Gestor (verificado: con la cuenta `usuario@catalogo.local` el PDF resultante pesa
  igual que el catálogo general, no el filtrado). Verificado en el navegador con el
  proveedor "Distribuciones Andinas": la tabla mostró código interno y código de proveedor
  por fila, y el PDF exportado con `?proveedorId=2` pesó distinto (más columnas) que el
  general.

## 2. Importación desde Excel (`ExcelProductoImportador`)

- ~~**Vista previa antes de confirmar.**~~ **Hecho.** `IProductoImportador` se dividió en
  dos pasos: `AnalizarAsync` lee el Excel y clasifica cada fila (nueva/actualización)
  **sin** tocar la base de datos, y `ConfirmarAsync` recién ahí aplica los cambios.
  `ImportarProductos.razor` muestra la vista previa completa (fila, código, producto,
  categoría, unidad, estado, y el nombre anterior cuando una actualización lo cambia) con
  los conteos "X nuevos, Y actualizaciones" antes de que el gestor confirme. Verificado con
  el Excel real: la vista previa anticipó "0 nuevos, 219 actualizaciones" y el resultado
  final coincidió exactamente.
- ~~**Detectar códigos duplicados dentro del mismo archivo.**~~ **Hecho.**
  `ExcelProductoImportador.AnalizarAsync` detecta cuando un `CODIGO` se repite en el mismo
  archivo, agrega una advertencia a `ResultadoAnalisisImportacion.Errores` (fila actual +
  en qué fila apareció la primera vez) y deja solo **una** fila por código en la vista
  previa — con los datos de la última aparición, que es lo que igual habría terminado
  guardado (ya no se procesan como si fueran dos productos distintos). Verificado con un
  archivo de prueba: 1 advertencia mostrada, la vista previa y el resultado final
  coincidieron en "2 producto(s) nuevo(s)" (no 3), y el producto quedó con los datos de la
  última fila.
- ~~**Capturar precio del proveedor.**~~ **Hecho.** El Excel acepta una columna `PRECIO`
  opcional (numérica o texto, con coma o punto decimal); si está, se guarda en
  `ProductoProveedor.PrecioProveedor` al crear o actualizar, y la vista previa la muestra.
  Regla importante: el precio **solo se toca si esa fila realmente trae uno** — si la
  columna no existe en el archivo o la celda viene vacía, no se borra un precio que el
  gestor ya hubiera cargado a mano. Se agregó `IProductoProveedorRepository.ActualizarAsync`
  (no existía). Verificado: al reimportar el mismo código con la celda de precio vacía, el
  precio ya guardado se conservó intacto en vez de borrarse.
- **Rendimiento en catálogos grandes.** Cada fila hace dos operaciones separadas
  (`IProductoRepository.CrearAsync` + `IProductoProveedorRepository.CrearAsync`), cada una
  abriendo su propio `DbContext` vía `IDbContextFactory` y su propio `SaveChanges` — bien
  para 219 filas, pero si el catálogo real crece a varios miles de productos convendría
  agrupar en una sola transacción con menos round-trips a la base de datos.

## 3. Pruebas automatizadas

- El proyecto no tiene ningún test (ya señalado como pendiente en `NOTA_PROYECTO.md`). Los
  candidatos de mayor riesgo para empezar son:
  - `ExcelProductoImportador`: mapeo de columnas por nombre de encabezado, manejo de
    códigos numéricos grandes, comportamiento de upsert.
  - `ProductoService.CrearAsync`: validación de código de proveedor duplicado.
  - `SolicitudService.CrearVariasAsync` / `ResumirNombres`: que el mensaje de notificación
    nunca exceda el límite de la columna `Mensaje` (esto ya causó una excepción real en
    producción de pruebas — ver sección de bugs corregidos abajo).
  - `SolicitudService.ResolverAsync`: transición de estados (no permitir resolver dos
    veces).

## 4. Rendimiento / escalabilidad

- **Paginación de `Catalogo.razor` en memoria.** `ObtenerCatalogoAsync` trae *todos* los
  productos que matchean el filtro y `Catalogo.razor` pagina con `Skip`/`Take` sobre la
  lista en el cliente. Con un catálogo de unos cientos de productos no se nota, pero no
  escala — convendría paginar en la consulta SQL (`IProductoRepository` recibiendo
  `pagina`/`tamanoPagina` y devolviendo también el total).
- **SignalR de un solo nodo.** `INotificacionBroadcaster` es un singleton en memoria del
  proceso. Funciona perfecto en un solo servidor, pero si algún día la app se despliega en
  más de una instancia (para más carga), las notificaciones en tiempo real dejarían de
  propagarse entre nodos — requeriría un backplane (Redis, Azure SignalR).

## 5. Seguridad

- **Confirmación de cuenta desactivada** (`RequireConfirmedAccount = false`) y contraseña
  mínima de 6 caracteres sin exigir complejidad — razonable para un proyecto de práctica,
  pero a documentar como riesgo si el proyecto se lleva a un entorno real.
- **Sin límite de tasa** en los endpoints de exportación (`/api/catalogo/pdf`,
  `/api/solicitudes/reporte/pdf` y `/excel`) — cualquier usuario autenticado puede
  generar reportes repetidamente sin límite.
- **Sin registro de auditoría** de quién aprueba/rechaza/importa — hoy se guarda
  `GestorId`/`GestorNombre` en cada solicitud resuelta, pero no hay un log central de
  acciones administrativas (ej. quién creó/editó un proveedor, quién importó un catálogo).

## 6. UX / Frontend

- **Extender el Design System a Identity.** Ya señalado en `NOTA_PROYECTO.md`: solo Login
  quedó restilizado con el sistema de Tailwind propio; Register y las páginas de
  `Manage/*` siguen con el markup scaffolded original de Microsoft.
- **Carrito no persistente.** `CarritoState` vive en memoria del circuito de Blazor Server
  — se pierde si se recarga la página a la fuerza, se pierde la conexión SignalR, o se
  cierra la pestaña. Para un caso de uso donde el usuario arma un pedido grande a lo largo
  de varias sesiones, convendría persistirlo en `localStorage` (por dispositivo) o en la
  base de datos (por usuario, sobreviviendo a cualquier dispositivo/sesión).
- **Recordar que el CSS de Tailwind no se regenera solo.** Ya documentado en el `README.md`
  pero vale la pena reforzarlo: cualquier clase nueva usada en un `.razor` requiere correr
  `npm run build:css` — esto causó un bug visual real durante el desarrollo (inputs sin el
  ancho fijo esperado porque la utilidad `w-20` no estaba en el `app.css` compilado).

## 7. Observabilidad / operación

- **Sin health checks.** No hay un endpoint `/health` que verifique la conexión a SQL
  Server — útil para saber si el contenedor de base de datos está caído antes de que falle
  una request de usuario.
- **Sin reintentos ante fallos transitorios de SQL Server.** `UseSqlServer(...)` no tiene
  configurado `EnableRetryOnFailure`, recomendado incluso para un solo nodo de SQL Server
  en Docker (reinicios del contenedor, hiccups de red).
- **Logging de eventos de negocio.** Hoy el único logging visible es el que emite EF Core
  por cada comando SQL. Sería útil un logger de aplicación para eventos como "solicitud
  creada", "importación completada con N errores", "producto marcado inactivo".

## 8. Manejo de errores (aprendido durante el desarrollo)

Durante las pruebas de esta sesión aparecieron dos fallas reales que vale la pena mantener
en mente como patrón general para el resto del código:

1. **Un error de una operación secundaria no debería tumbar la operación principal.** El
   envío de notificaciones (best-effort) originalmente podía lanzar una excepción de SQL
   (`String or binary data would be truncated`) que abortaba toda la creación de
   solicitudes — aunque las solicitudes ya estaban guardadas. Se corrigió envolviendo el
   envío de notificaciones en un `try/catch` y acortando el mensaje generado. Vale la pena
   revisar si hay otros puntos del código con el mismo riesgo (por ejemplo, si algún día
   se agrega envío de email en `ResolverAsync`).
2. **Límites de columnas de base de datos como caso de prueba.** `Notificacion.Mensaje`
   tiene `nvarchar(500)`; cualquier lógica que concatene texto dinámico (nombres de
   productos, comentarios largos) sin acotar el resultado es una fuente de bugs de este
   tipo. Al escribir un método nuevo que arme texto para guardar en base de datos, conviene
   preguntarse siempre "¿qué pasa si la entrada es muy grande?".

## 9. CI/CD y entorno de desarrollo

- **Sin pipeline de CI.** No hay ninguna acción configurada (GitHub Actions, etc.) que
  corra `dotnet build`/`dotnet test` en cada cambio.
- **Onboarding de un nuevo desarrollador.** Hoy requiere levantar SQL Server manualmente en
  Docker siguiendo instrucciones externas al repo
  (`Documentos/Repaso de Proyectos/Configuracion SQL Server.txt`). Un `docker-compose.yml`
  en el propio repo (SQL Server + variables de entorno para la cadena de conexión) haría
  que cualquiera pudiera levantar el proyecto completo con un solo comando.

## 10. Próximo nivel de arquitectura

- **MediatR / CQRS**, ya mencionado como exploración futura en `NOTA_PROYECTO.md`. Tiene
  sentido una vez que Application empiece a sentirse recargado (por ejemplo, si se agrega
  el `Pedido` como agregado con varias reglas de negocio por caso de uso).
- **Validación centralizada.** Hoy las reglas de negocio ("cantidad > 0", "código de
  proveedor obligatorio si hay proveedor", etc.) viven como `if` + `throw` dentro de cada
  método de servicio. Funciona bien a esta escala; si crece, FluentValidation (u otra
  librería) puede ordenar esas reglas en un solo lugar por comando/DTO.

## 11. Login y Registro (funcionalidad)

- ~~**Asignar el rol "Usuario" al registrarse.**~~ **Hecho.** `Register.razor` llama a
  `UserManager.AddToRoleAsync(user, Roles.Usuario)` justo después de crear la cuenta,
  con un `Logger.LogWarning` como respaldo si la asignación fallara. Verificado
  registrando una cuenta nueva por el formulario: el log de EF Core mostró el
  `INSERT INTO [AspNetUserRoles]` y, tras iniciar sesión, el menú lateral solo mostró
  las opciones de nivel Usuario (sin la sección "Gestión").
- ~~**Pedir "Nombre completo" en el registro.**~~ **Hecho.** Se agregó el campo
  "Nombre completo" como primero del formulario de registro (`[Required]`), que se
  guarda en `ApplicationUser.NombreCompleto` al crear la cuenta. Para que el dato
  capturado realmente se vea en la app (y no quede guardado sin uso), se creó
  `ApplicationUserExtensions.NombreParaMostrar()` — devuelve el `NombreCompleto` si
  existe, o cae de vuelta al nombre de usuario/email para cuentas antiguas que no lo
  tienen — y se conectó en los tres lugares que antes mostraban `Identity.Name`:
  `UserMenu.razor` (avatar, nombre en el topbar y en el dropdown), `Carrito.razor`
  (nombre del solicitante al crear un pedido) y `Bandeja.razor` (nombre del gestor al
  aprobar/rechazar). Verificado en el navegador: registrando una cuenta nueva
  ("Carlos Prueba Registro") y enviando un pedido desde el carrito, la Bandeja del
  gestor mostró "Pedido #48 · Carlos Prueba Registro" (no el email), y el topbar del
  gestor mostró "Gestor de Catálogo" en vez de "gestor@catalogo.local".
- **Habilitar el bloqueo por intentos fallidos en el login.** `Login.razor` llama a
  `SignInManager.PasswordSignInAsync(..., lockoutOnFailure: false)`, lo que desactiva el
  bloqueo por fuerza bruta que Identity ya trae configurado por defecto (5 intentos
  fallidos). Hoy se pueden probar contraseñas sin límite.
- ~~**Restilizar `Register.razor` con el Design System propio**~~ **Hecho.** Se
  reescribió con `Card`/`FormField`/`Button`/`Alert`, igual que `Login.razor`, en vez del
  markup scaffolded de Microsoft (Bootstrap: `btn-primary`, `form-floating`). De paso se
  creó un `AuthLayout.razor` propio: sin sidebar ni topbar, solo el logo y el formulario
  centrados — antes estas páginas heredaban el `MainLayout` completo (sidebar con
  "Catálogo" visible incluso sin sesión, topbar con carrito/notificaciones). Este layout
  se extendió a **toda** la familia de pantallas de Identity que se navegan sin sesión
  iniciada, no solo Login/Register, para que el sidebar/topbar no reaparezca al seguir
  esos flujos: `ForgotPassword`, `ForgotPasswordConfirmation`, `ResendEmailConfirmation`,
  `ResetPassword`, `ResetPasswordConfirmation`, `Lockout`, `InvalidPasswordReset` (estas,
  además, con el mismo restyle Card/FormField/Alert y traducción al español), y también
  `RegisterConfirmation`, `ConfirmEmail`, `ConfirmEmailChange`, `LoginWith2fa`,
  `LoginWithRecoveryCode`, `ExternalLogin`, `InvalidUser` (solo se les quitó el layout con
  sidebar, sin rediseño visual completo, por ser pantallas de borde que hoy casi no se
  alcanzan — sin 2FA activado, sin proveedores externos y sin envío real de correo).
  `AccessDenied.razor` y las páginas de `Account/Manage/*` se dejaron con el `MainLayout`
  normal a propósito: ahí el usuario ya tiene sesión iniciada y le sirve poder navegar a
  otra parte de la app. También se ocultó el texto en inglés de `ExternalLoginPicker`
  ("There are no external authentication services configured...") cuando no hay
  proveedores externos configurados, ya que no aporta nada en este proyecto. Verificado
  en el navegador: Login, Register, "¿Olvidaste tu contraseña?" y "Reenviar confirmación
  de email" muestran únicamente el formulario centrado (sin sidebar ni topbar); un
  registro completo sigue creando la cuenta, asignando el rol Usuario y llevando al
  usuario ya autenticado al inicio; y un login normal sigue funcionando y mostrando el
  sidebar/topbar completos una vez autenticado.
- ~~**Traducir `Register.razor` al español.**~~ **Hecho.** Título, subtítulo, etiquetas de
  campo y mensajes de validación quedaron en español ("Crear cuenta", "Nombre completo",
  "La contraseña y su confirmación no coinciden", etc.), consistente con el resto de la
  app y con `Login.razor`.
- **Aclarar que la confirmación de email no envía nada real.** `IdentityNoOpEmailSender`
  es un no-op (no manda correos de verdad). Como `RequireConfirmedAccount = false` esto no
  bloquea el login, pero el mensaje que ve el usuario tras registrarse ("revisa tu correo")
  es engañoso — convendría decir explícitamente que la confirmación de email está
  desactivada en este entorno de práctica.
- **Decidir cómo se asigna el rol "Gestor".** Hoy no hay ningún mecanismo para que alguien
  se registre como Gestor (ni por invitación, ni por dominio de correo, ni que un Gestor
  pueda promover a otro usuario después) — vale la pena definir la regla antes de que haga
  falta en la práctica.
- ~~**Elegir el rol al iniciar sesión ("Solicitante" / "Gestor"), sin depender del
  correo.**~~ **Hecho.** El login ahora muestra un selector tipo pestañas ("Solicitante" /
  "Gestor") antes de los campos de email y contraseña. La cuenta sigue teniendo un único
  rol real (como siempre), pero ahora esa intención se declara explícitamente: si las
  credenciales son correctas pero la cuenta no tiene el rol elegido, se cierra la sesión
  recién iniciada y se muestra un error claro ("Esta cuenta no tiene permisos de
  Solicitante/Gestor") en vez de dejar entrar con el rol real sin avisar. El selector se
  implementó con `InputRadioGroup`/`InputRadio` (radios nativos ocultos con `sr-only` +
  `<label>` estilizado como botón segmentado vía CSS `:checked`), no con `@onclick` en
  botones sueltos — se probó esa vía primero y no funcionaba, porque `Login.razor` se
  renderiza como SSR estático (necesario para que el `EditForm` funcione sin JavaScript),
  y ese modo no tiene circuito interactivo para atender eventos de clic en C#; los radios
  nativos sí funcionan porque el navegador resuelve el `:checked` sin necesitar Blazor.
  Verificado en el navegador: con la cuenta `gestor@catalogo.local`, elegir "Gestor" entra
  normalmente; elegir "Solicitante" con esas mismas credenciales muestra el error y no deja
  entrar.

---

*Última actualización: reflejando el estado del proyecto tras completar la sección 1
(modelo de datos), los primeros tres puntos de importación desde Excel (vista previa,
duplicados, precio del proveedor), y agregar la sección 11 (Login y Registro).*
