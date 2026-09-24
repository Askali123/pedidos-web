# Plan de mejoras — UI y UX

Checklist de trabajo, de mayor a menor prioridad, salida del análisis hecho el 2026-09-18
sobre la interfaz de la aplicación (recorrido de código de los componentes compartidos en
`Components/UI/`/`Components/Layout/` + navegación en vivo por las pantallas principales,
logueado como Gestor y como Solicitante, en desktop y en mobile). El proyecto ya tiene una
base de diseño madura y bien pensada — `Button`/`Card`/`Alert`/`Badge`/`FormField`/
`EmptyState`/`Modal`/`ConfirmDialog`/`Pagination`/`Dropdown`, tokens de color semánticos,
un solo set de íconos, anillos de foco globales. El problema no es falta de sistema, es
**adopción inconsistente** de ese sistema, más dos bugs reales encontrados navegando la
app: `NuevoProducto.razor` guarda productos con el nombre vacío sin ningún error (se
reprodujo en vivo — quedó un producto "" real en el catálogo de desarrollo, Id 237, ver
tarea 1), y los botones de acción principales de Catálogo/Proveedores/Bandeja quedan fuera
de pantalla detrás de un scroll horizontal sin ninguna señal visual, tanto en desktop como
en mobile — en Bandeja, en mobile, el botón "Aprobar" queda literalmente cortado a la
mitad (ver tarea 2). Cada tarea se marca `[x]` al completarse; no reordenar sin avisar —
el orden es la prioridad acordada.

## Decisiones de diseño ya acordadas

- **Tablas fuera de pantalla (Catálogo/Proveedores/Bandeja)**: en vez de solo agregar una
  señal visual de que hay scroll horizontal (fade/sombra en el borde — arreglo rápido pero
  no resuelve que las acciones principales queden escondidas), se rediseñan esas tablas a
  un layout de tarjetas apiladas por debajo de cierto ancho, priorizando las columnas y
  acciones más importantes de cada pantalla. Más trabajo, pero ataca la causa real en vez
  de taparla.
- **Dashboard de Inicio**: hoy son 3 tarjetas KPI y después una zona vacía. Se invierte en
  agregarle widgets reales de acceso directo a lo accionable de cada rol (para un
  Solicitante, sus solicitudes pendientes; para un Gestor, lo que tiene para aprobar) en
  vez de dejarlo como está — convierte el Inicio en un punto de partida útil, no una
  pantalla decorativa.

## P0 — Bugs rotos hoy

- [x] **1. Validar el nombre (y precio/stock) al crear o editar un producto** — Hecho.
  `CrearProductoDto` (`CatalogoPedidos.Application.Productos.ProductoDto`) no tenía
  ninguna DataAnnotation, y `ProductoService.CrearAsync`/`ActualizarAsync` tampoco
  validaban nada del lado servidor. `NuevoProducto.razor` marcaba "Nombre*" como
  obligatorio visualmente pero no tenía `ValidationMessage`/`ValidationSummary` en
  absoluto, así que nada lo exigía de verdad. Reproducido en vivo antes del fix: el
  formulario con Nombre vacío guardaba igual, con el toast literal `Producto ''
  guardado correctamente.` — quedó un producto sin nombre (Id 237, `Activo = 1`) en la
  base de desarrollo, ya eliminado (sin referencias en `Solicitudes` ni
  `ProductoProveedores`, confirmado por SQL antes de borrarlo).

  Se agregó `[Required]` + `[StringLength(200, MinimumLength = 1)]` a `Nombre` (200 es
  el mismo `HasMaxLength` que ya tenía la columna en `AppDbContext` — la tabla nunca
  rechazaba una cadena vacía porque `IsRequired()` de EF Core solo dice "no nulo", no
  "no vacío") y `[Range(0, double.MaxValue)]` a `Precio`. **Stock quedó deliberadamente
  sin `[Range]`**: el stock negativo es un estado real que ya usa la app (aprobaciones
  que superan lo disponible) — se confirmó en vivo editando "Abrasivo REGULAR" (Id 11,
  `Stock = -27`, dato real, no un artefacto) que agregar ese límite habría bloqueado
  poder guardar cualquier edición futura de ese producto sin antes forzar a corregirle
  el stock, una regresión real. Del lado servidor, `ProductoService` ahora tiene un
  `ValidarDatosBasicos` privado (mismo patrón manual con `InvalidOperationException` que
  ya usa `ProveedorService`, no infraestructura nueva) que repite el chequeo de
  Nombre/Precio — cualquier otro caller del servicio, no solo este formulario, queda
  cubierto. Del lado del formulario, se agregó `ValidationSummary` +
  `ValidationMessage` para Nombre y Precio (ya estaba `DataAnnotationsValidator`, pero
  sin nada que mostrara el error).

  Verificado en el navegador, en creación y en edición: (1) enviar "Nuevo producto"
  con Nombre vacío mostró "El nombre es obligatorio." (resumen + debajo del campo) y no
  guardó nada; (2) con Nombre válido y Precio `-50` mostró "El precio no puede ser
  negativo." y tampoco guardó; (3) con datos válidos guardó normalmente ("Producto
  'Producto prueba validacion' guardado correctamente.", producto de prueba borrado
  después); (4) editando "Abrasivo REGULAR" (Stock real `-27`) y borrando el Nombre,
  el mismo error apareció y bloqueó el guardado — confirmado por SQL que el producto
  no quedó modificado en la base. Sin errores en el log del servidor durante toda la
  prueba.
  Archivo(s): `ProductoDto.cs`, `ProductoService.cs`, `NuevoProducto.razor`

- [x] **2. Tablas responsive en Catálogo, Proveedores y Bandeja de solicitudes** — Hecho.
  Las tres (y también `Administrar.razor`, confirmado durante la implementación —
  comparte el mismo patrón) envolvían su tabla en `.table-wrap` con
  `overflow-x: auto` y ninguna señal visual de que hubiera más contenido. En
  `Proveedores.razor` el problema aparecía **incluso en desktop** (`scrollWidth` 1040
  vs `clientWidth` 684 a 1568px) porque la tabla competía por espacio en un grid de
  3 columnas junto al formulario "Nuevo proveedor" — un breakpoint de viewport normal
  no lo hubiera detectado, ya que el problema no era el ancho de pantalla sino el
  ancho del contenedor.

  Implementado con **CSS container queries** (`container-type: inline-size` en
  `.table-wrap` + `@container (max-width: 640px)` en `Styles/app.tailwind.css`), no
  con los breakpoints `md`/`lg` habituales de Tailwind (que reaccionan al viewport,
  no al contenedor — no hubieran resuelto el caso de Proveedores). Por debajo de ese
  ancho de contenedor, cada `<tr>` se re-apila como una tarjeta con borde propio, el
  `<thead>` se oculta, y cada `<td>` muestra su valor junto a una etiqueta tomada de
  un atributo `data-label` agregado en el markup de cada página (las celdas de
  acciones, sin `data-label`, se muestran como un bloque simple sin repetir
  "Acciones"; las celdas vacías se ocultan solas vía `:empty`). Se agregó `flex-wrap`
  a los grupos de botones de acción de las cuatro páginas para que, si no entran en
  una sola línea, se acomoden en varias en vez de desbordar. Es un único cambio de
  CSS compartido por las cuatro páginas (y por cualquier tabla futura que use
  `.table-enterprise`), no cuatro implementaciones de tarjetas por separado.

  Para `Proveedores.razor` específicamente, además del CSS, se sacó la tabla del
  grid de 3 columnas: ahora ocupa el ancho completo de la página, y el formulario
  "Nuevo proveedor" quedó debajo (en un `<div class="max-w-xl">`, mismo patrón que
  ya usa `NuevoProducto.razor`) en vez de al costado — esto resuelve la causa de raíz
  del problema en desktop, no solo lo tapa con las tarjetas.

  Verificado en el navegador: no se logró forzar el viewport del propio Chrome a un
  ancho angosto de forma confiable con las herramientas de automatización
  disponibles (`resize_window` no afectaba el `window.innerWidth` real en este
  entorno), así que se verificó de la forma más directa posible — angostando el
  contenedor `.table-wrap` en sí mismo vía `style.maxWidth = '380px'` desde la
  consola, que es exactamente la condición que la *container query* observa. Con
  eso: (1) en Proveedores, sin achicar nada, a 1568px de ancho de página real,
  `scrollWidth === clientWidth` (1060 = 1060) — el bug de desktop ya no ocurre; (2)
  angostando el contenedor a 380px en las cuatro páginas, las cuatro se vieron
  correctamente como listas de tarjetas con etiquetas y sin ningún scroll
  horizontal — Bandeja mostró "Aprobar"/"Rechazar"/"PDF" completos (el caso más
  claro del hallazgo original), Catálogo mostró Nombre/Categoría/Unidad/Precio/Stock
  con sus botones envueltos en dos líneas, y Administrar mostró
  N.º/Producto/Proveedor·Código/Cantidad/Estado/Gestor apilados. Prueba funcional:
  con el contenedor angosto, hice clic real en "Aprobar" de Bandeja y el modal
  "Aprobar solicitud" se abrió normalmente — confirma que el cambio de markup no
  rompió los manejadores de evento (cancelado sin confirmar, era solo una prueba de
  humo). Sin errores en el log del servidor durante toda la prueba.
  Archivo(s): `Styles/app.tailwind.css` (y su salida compilada `wwwroot/app.css`,
  recompilada con `npm run build:css`), `Catalogo.razor`, `Proveedores.razor`,
  `Bandeja.razor`, `Administrar.razor`

## P1 — Inconsistencias de fondo amplio / estados faltantes

- [x] **3. Truncar la paginación cuando hay muchas páginas** — Hecho.
  `Pagination.razor` renderizaba un `<button>` por página con un `@for` plano, sin
  lógica de truncamiento. Con el catálogo actual (232 productos, 29 páginas) esto
  generaba una fila de 29 botones que necesitaba su propio scroll horizontal incluso
  a los 1568px de desktop.

  Se reemplazó por un patrón de paginación con ventana: primera y última página
  siempre visibles, más la página actual y una vecina a cada lado; el resto se
  resume con "…" (no clickeable). Con 7 páginas o menos se siguen listando todas
  (nunca hay hueco que resumir, mismo comportamiento de antes). Implementado como un
  método privado `PaginasVisibles()` que devuelve `List<int?>` (`null` = "…"), sin
  tocar la firma pública del componente (`CurrentPage`/`TotalPages`/
  `CurrentPageChanged` quedan igual) — ningún caller (`Catalogo.razor` es el único
  hoy) necesitó cambios.

  Verificado en el navegador contra el catálogo real (232 productos, 29 páginas):
  en la página 1 se ve "1 2 … 29"; navegando a la página 15 (con 14 clics
  automatizados en “siguiente”) se ve "1 … 14 15 16 … 29", con las dos elipsis y la
  ventana centrada en la página actual, todo en una sola línea sin scroll
  horizontal; haciendo clic en "16" navegó correctamente; en la página 29 (el
  extremo final) se ve "1 … 28 29" sin elipsis de más, y la flecha "siguiente" queda
  deshabilitada, igual que antes del cambio. Sin errores en el log del servidor.
  Archivo(s): `Pagination.razor`

- [x] **4. Traducir y estilizar `Account/Manage/*` (Perfil, Email, Contraseña, 2FA,
  Passkeys, Datos personales)** — Hecho.
  Era scaffolding de Identity sin tocar: `<h1>`/`<p>` planos, grid de Bootstrap
  (`row`/`col-xl-6`/`form-floating`), navegación lateral sin estado activo, todo en
  inglés ("Manage your account", "Two-factor authentication", "No passkeys are
  registered", botón "Save") — conviviendo dentro del mismo layout con sidebar/header
  que el resto de la app, que sí está completamente traducida y estilada. Resultaron
  ser 14 archivos `.razor` en total (los 6 del menú — Perfil, Email, Contraseña, 2FA,
  Passkeys, Datos personales — más sub-flujos que no tienen entrada propia en el menú:
  `SetPassword`, `EnableAuthenticator`, `Disable2fa`, `ResetAuthenticator`,
  `GenerateRecoveryCodes`, `RenamePasskey`, `DeletePersonalData`, `ExternalLogins`),
  más los 4 compartidos (`ManageLayout`, `ManageNavMenu`, `StatusMessage`,
  `ShowRecoveryCodes`).

  Se reescribieron todos con los componentes existentes (`Card`, `FormField`,
  `Button`, `Alert`, `EmptyState`) y se tradujo la totalidad del texto visible,
  incluyendo los `[Display(Name=...)]`/mensajes de error de las DataAnnotations y los
  strings que arma cada `RedirectToCurrentPageWithStatus`/`RedirectToWithStatus` del
  lado servidor (cuidando mantener el prefijo literal `"Error: "` en los que
  corresponde, porque `StatusMessage.razor` decide el color del Alert mirando si el
  mensaje empieza con esa palabra). Páginas con una acción destructiva o irreversible
  (`Disable2fa`, `ResetAuthenticator`, `GenerateRecoveryCodes`, `DeletePersonalData`)
  quedaron con `Alert Variant="AlertVariant.Warning"` explicando la consecuencia y
  `Button Variant="ButtonVariant.Danger"`, mismo criterio que ya usa el resto de la
  app para acciones irreversibles (ej. `ConfirmDialog` de Desactivar producto).

  Además de la traducción/estilo pedidos, aparecieron dos gaps de fondo al mirar estos
  archivos de cerca, corregidos como parte de la misma tarea (no ampliaba el pedido
  original, era necesario para que "usar los componentes existentes" tuviera sentido):
  ninguna de las 14 páginas llamaba a `PageHeaderState.HeaderState.Set(...)`, así que
  el título/breadcrumb del topbar quedaba con lo que hubiera dejado la última página
  visitada antes de entrar acá — se le agregó a cada una, con breadcrumbs de 2 o 3
  niveles según corresponda (`Mi cuenta > Passkeys`, `Mi cuenta > Autenticación en dos
  pasos > Configurar app autenticadora`); y el nav lateral (`ManageNavMenu.razor`) no
  tenía ningún estado activo — se agregó una clase `nav-link-light`/
  `nav-link-light-active` nueva al CSS compartido (mismo criterio que ya usa
  `.sidebar-link`/`.sidebar-link-active` para el sidebar oscuro, pero pensada para
  navegación secundaria sobre fondo claro — reusable si aparece otro caso similar).
  `StatusMessage.razor` (que ya coincidía por casualidad en los nombres de clase
  `alert`/`alert-danger` con el sistema de diseño) se reescribió para usar
  `<Alert>` directamente, así queda con el ícono y estructura idénticos al resto de
  alertas de la app.

  Verificado en el navegador, logueado como Solicitante, en las 14 páginas: `Perfil`
  guardó teléfono/dirección correctamente ("Tu perfil se actualizó correctamente.",
  Alert verde); `Email`, `Contraseña`, `Autenticación en dos pasos` (con su botón
  "Agregar app autenticadora"), `Configurar app autenticadora` (breadcrumb de 3
  niveles, clave + QR + formulario de verificación), `Passkeys` (con `EmptyState`
  cuando no hay ninguna), `Restablecer app autenticadora`, `Inicios de sesión
  externos` y `Datos personales` (con su botón "Descargar" real, que sí generó la
  descarga) se vieron completamente traducidas y estiladas, con el nav lateral
  marcando la sección activa en cada una. `Eliminar datos personales` se revisó solo
  visualmente (Alert de advertencia + botón rojo "Eliminar mis datos y cerrar mi
  cuenta") — **a propósito no se probó el envío real**, porque hubiera borrado la
  cuenta semilla de prueba. `Disable2fa`/`GenerateRecoveryCodes` no se pudieron
  ejercitar en vivo porque su propio código ya tira una excepción si el 2FA no está
  activo en la cuenta (guardia pre-existente, no tocada) — se revisaron por lectura de
  código en su lugar. Sin errores nuevos en el log del servidor durante toda la
  prueba.
  Archivo(s): los 14 `.razor` de `Account/Pages/Manage/`, `ManageLayout.razor`,
  `ManageNavMenu.razor`, `StatusMessage.razor`, `ShowRecoveryCodes.razor`,
  `Styles/app.tailwind.css`

- [x] **5. Agregar un link a "Mi cuenta" desde el menú de usuario** — Hecho.
  No había ninguna forma de llegar a `Account/Manage` desde la UI: el dropdown de
  `UserMenu.razor` solo tenía "Sesión iniciada como X" y "Cerrar sesión". Perfil,
  cambio de contraseña, 2FA y registro de passkeys solo eran alcanzables escribiendo
  la URL a mano. Se agregó un ítem "Mi cuenta" (con el mismo ícono `user` que usa el
  resto de la app) entre el bloque de "Sesión iniciada como" y "Cerrar sesión", con el
  mismo estilo que ya usan los demás ítems del dropdown (`Bandeja.razor`'s "Enviar a
  proveedor", por ejemplo). Dependía de la tarea 4 para que la pantalla a la que lleva
  no desentonara — ya estaba hecha.

  Verificado en el navegador: abrí el menú de usuario (esquina superior derecha) y se
  ve "Sesión iniciada como / Usuario de Prueba", después "Mi cuenta" y "Cerrar
  sesión"; al hacer clic en "Mi cuenta" navegó correctamente a `/Account/Manage`
  (Perfil), con el layout completo de la tarea 4. Sin errores en el log del servidor.
  Archivo(s): `UserMenu.razor`

- [x] **6. Sacar el breadcrumb redundante con el título en 8 páginas** — Hecho.
  `HeaderState.Set(titulo, [(titulo, null)])` se llamaba con el mismo texto repetido
  en `Catalogo.razor`, `Administrar.razor`, `Bandeja.razor`, `MisSolicitudes.razor`,
  `HistorialResoluciones.razor`, `Proveedores.razor`, `GestionRoles.razor` y
  `HistorialEnvios.razor` (esta última con un mismatch real además: el breadcrumb
  decía "Historial de envíos" pero el título/`<h1>` decían "Historial de envíos a
  proveedores").

  Arreglo de un solo punto, como planteaba la tarea: `Topbar.razor` ahora renderiza
  `<Breadcrumb>` solo cuando `HeaderState.Breadcrumb.Count > 1` (antes era `> 0`) —
  con 0 o 1 ítems no hay ninguna ruta real que mostrar. Además, en las 8 páginas se
  sacó el array de un solo ítem directamente (`HeaderState.Set("Catálogo")` en vez de
  `HeaderState.Set("Catálogo", [("Catálogo", null)])`), replicando el mismo patrón
  que ya usaba `Home.razor` — no solo dejar de mostrarlo, sino no seguir construyendo
  un array que no se usa para nada. De paso, el texto de `HistorialEnvios.razor` quedó
  consistente (un solo `HeaderState.Set("Historial de envíos a proveedores")`, sin el
  texto viejo del breadcrumb dando vueltas en el código aunque ya no se mostrara).

  Se verificó que las páginas con breadcrumbs de 2+ niveles genuinos
  (`Carrito.razor`, `ImportarProductos.razor`, `NuevoProducto.razor`,
  `ProveedoresDeProducto.razor`, y las 14 de `Account/Manage/*` de la tarea 4) no se
  tocaron — siguen mostrando su ruta real sin cambios.

  Verificado en el navegador, logueado como Gestor, en las 8 páginas de la lista:
  Catálogo, Bandeja de solicitudes, Administrar pedidos, Proveedores, Historial de
  resoluciones, Gestión de roles, Mis solicitudes e Historial de envíos a
  proveedores — todas muestran el título una sola vez, sin el breadcrumb duplicado
  apilado arriba. De paso, al navegar Catálogo/Proveedores en un ancho intermedio
  (~1129px con sidebar expandido) se confirmó que el layout responsive de la tarea 2
  sigue funcionando correctamente sin la ayuda del truco de JS usado en esa tarea.
  Sin errores en el log del servidor.
  Archivo(s): `Topbar.razor`, `Catalogo.razor`, `Administrar.razor`, `Bandeja.razor`,
  `MisSolicitudes.razor`, `HistorialResoluciones.razor`, `Proveedores.razor`,
  `GestionRoles.razor`, `HistorialEnvios.razor`

- [x] **7. Mostrar el gestor de forma consistente en "Mis solicitudes"** — Hecho.
  La hipótesis original del hallazgo (que hubiera dos caminos de código distintos
  resolviendo el nombre del gestor de forma diferente) **resultó ser incorrecta** —
  investigado a fondo antes de tocar nada. Hay un solo camino de resolución de
  solicitudes en toda la app: `Bandeja.razor` → su helper `DatosGestor()` →
  `SolicitudService.ResolverAsync`/`ResolverVariasAsync`/`ResolverPedidoAsync`
  (los tres únicos call sites de esos métodos, confirmado por grep). `DatosGestor()`
  ya resuelve el nombre con `appUser.NombreParaMostrar(...)` de forma consistente — no
  hay ningún otro lugar del código que setee `Solicitudes.GestorNombre` con un valor
  distinto.

  La causa real es un dato histórico, no un bug de código: `GestorNombre` es un
  snapshot que se graba una sola vez, al momento de resolver cada solicitud (no se
  recalcula después). `NombreParaMostrar` cae al username/email como respaldo
  "para cuentas creadas antes de ese cambio" (comentario ya existente en
  `ApplicationUserExtensions.cs`, sobre el momento en que se hizo obligatorio cargar
  `NombreCompleto` al registrarse). Confirmado por SQL: la cuenta
  `gestor@catalogo.local` HOY tiene `NombreCompleto = 'Gestor de Catálogo'`, pero 68
  de las 127 solicitudes resueltas (todas de ese mismo `GestorId`, resueltas entre el
  13 y el 15/09/2026) habían capturado el email en su momento, porque en esas fechas
  la cuenta todavía no tenía `NombreCompleto` cargado — probablemente de antes de que
  `Seed.cs` incluyera ese campo. Ningún otro campo denormalizado similar
  (`NotificacionesProveedor.GestorNombre`, `ConfirmacionesEntrega.ConfirmadoPorNombre`)
  tenía el mismo problema (0 filas afectadas en ambos, confirmado por SQL).

  No hizo falta ningún cambio de código — el camino de resolución actual ya es
  robusto para cualquier cuenta nueva, porque `NombreCompleto` es obligatorio desde
  el registro. Se corrigió el dato histórico con un único `UPDATE` acotado
  (`GestorId` = el de `gestor@catalogo.local` AND `GestorNombre` = el email exacto),
  68 filas afectadas, verificado por SQL que quedaron 0 filas con email después.

  Verificado en el navegador logueado como Solicitante: recorrida toda la lista de
  "Mis solicitudes" (pedidos #43 al #63), todas las filas de la columna GESTOR
  muestran "Gestor de Catálogo" — ninguna muestra el email. Sin errores en el log del
  servidor.
  Archivo(s): ninguno de código — corrección de datos vía SQL sobre `Solicitudes`.

- [x] **8. Dashboard de Inicio con accesos directos reales** — Hecho.
  Debajo de las 3 tarjetas KPI y el botón "Ir al catálogo", se agregó un widget de
  acceso directo distinto según el rol (uno u otro, no ambos — un Gestor ve el suyo,
  un Solicitante el suyo):

  - **Gestor**: Card "Pendientes de aprobar" con los primeros 5 pedidos de la bandeja
    (agrupados por `PedidoId`, mismo criterio que `Bandeja.razor` — reusa la misma
    llamada a `ObtenerBandejaAsync()` que ya hacía la tarjeta KPI "Pedidos
    pendientes", sin pedirla dos veces), cada uno con solicitante, cantidad de líneas,
    fecha, y el mismo badge "Urgente" que ya usa Bandeja (>48h pendiente); si hay más
    de 5, un link "Ver los N pedidos pendientes →"; si no hay ninguno, el mismo
    `EmptyState` "Todo al día" que ya usa Bandeja.razor. Cada fila linkea a
    `/solicitudes/bandeja`.
  - **Solicitante**: Card "Mis solicitudes en curso" con sus primeros 5 pedidos en
    estado Pendiente (`ObtenerMisSolicitudesAsync` + filtro + agrupado por
    `PedidoId`), con badge "Pendiente" y el mismo link "Ver los N..." si aplica;
    vacío, `EmptyState` "Sin solicitudes en curso". Cada fila linkea a
    `/solicitudes/mis-solicitudes`.

  Alcance decidido al empezar (la tarea dejaba esto abierto): 5 ítems visibles como
  tope, con link a la pantalla completa solo cuando hay más; un widget por rol, no los
  dos a la vez para un Gestor (aunque técnicamente también podría tener pedidos
  propios) — mantiene el Inicio enfocado en lo más urgente de cada rol en vez de
  duplicar todo lo que ya muestran Bandeja/Mis solicitudes.

  Verificado en el navegador en ambos roles: como Gestor, "Pendientes de aprobar"
  mostró los 3 pedidos pendientes reales (mismo número que la tarjeta KPI), con
  solicitante/cantidad/fecha, y el clic en un pedido navegó correctamente a
  `/solicitudes/bandeja`; como Solicitante, "Mis solicitudes en curso" mostró los 2
  pedidos pendientes de esa cuenta con badge "Pendiente", y el clic navegó
  correctamente a `/solicitudes/mis-solicitudes`. El caso vacío (`EmptyState`) no se
  forzó en vivo — reusa el mismo componente ya verificado en `Bandeja.razor`/
  `MisSolicitudes.razor` en tareas anteriores. Sin errores en el log del servidor.
  Archivo(s): `Home.razor`

## P2 — Consistencia / pulido

- [x] **9. Estilizar el input de archivo en "Importar productos"** — Hecho.
  `ImportarProductos.razor` ya tenía `class="form-input"` en el `<InputFile>`, pero
  seguía viéndose como chrome nativo del navegador sin estilar — es una limitación
  conocida de CSS: el botón "Seleccionar archivo" de un `<input type="file">` es un
  pseudo-elemento (`::file-selector-button`) que las propiedades normales de
  `.form-input` (padding, border, etc.) no tocan.

  Se optó por la utilidad `file:*` que Tailwind expone justo para este caso (en vez
  de escribir el CSS del pseudo-elemento a mano), aplicada directo en el markup ya
  que este es el único `<InputFile>` de toda la app (no ameritaba una clase
  compartida nueva): el botón queda con el mismo aspecto que `.btn-secondary` (fondo
  blanco, borde gris, texto gris, hover a gris claro) y el texto "Ningún archivo
  seleccionado" en gris neutral, igual que el resto de la app. Se mantuvo el estado
  deshabilitado (`disabled:opacity-50`) mientras no se eligió proveedor, como ya
  hacía antes.

  Verificado en el navegador logueado como Gestor: con el proveedor sin seleccionar,
  el botón "Seleccionar archivo" se ve atenuado (deshabilitado); al elegir un
  proveedor, el botón pasa a verse idéntico a los botones secundarios del resto de
  la app (mismo que "Editar"/"Ver productos" en Proveedores), ya no como el control
  nativo azul/gris del navegador. Sin errores en el log del servidor.
  Archivo(s): `ImportarProductos.razor` (y `wwwroot/app.css`, recompilado con
  `npm run build:css` para que Tailwind detecte las clases `file:*` nuevas — no se
  tocó `Styles/app.tailwind.css`, no hizo falta ninguna clase compartida nueva)

- [x] **10. `NotFound.razor` con `EmptyState` y traducido** — Hecho.
  Era `<h1>Not Found</h1>` + `<p>` en inglés, sin ícono, sin link de vuelta a ningún lado
  — el mismo problema de scaffolding sin tocar que tenía `AccessDenied.razor`. Se siguió
  el mismo patrón de `Carrito.razor`: la página ahora usa `EmptyState.razor` (ícono
  `search`, título "Página no encontrada", descripción "La página que buscás no existe o
  fue movida.") con un botón "Volver al inicio" en el slot `<Action>`, todo envuelto en un
  `<div class="card">` centrado (`min-h-[60vh] flex items-center justify-center p-6`,
  `max-w-md`).

  El centrado/padding propio (en vez de depender del layout, como hacen las demás
  páginas) es necesario porque `MainLayout.razor` solo agrega el sidebar/topbar/padding
  dentro de `<AuthorizeView><Authorized>`; para un visitante sin sesión el `<NotAuthorized>`
  es simplemente `@Body` sin ningún chrome alrededor — sin el wrapper propio, la tarjeta
  habría quedado pegada al borde superior izquierdo de una página en blanco.

  **Hallazgo durante la prueba:** al probar el caso realmente anónimo (sin sesión, tanto
  con una navegación de página completa a una URL nueva como con un click interno —
  navegación "enhanced" de Blazor, para descartar que fuera solo un efecto de recarga
  completa) la app redirige a `/Account/Login?ReturnUrl=...` en vez de mostrar
  `NotFound.razor` directamente. La causa es la `FallbackPolicy` con
  `RequireAuthenticatedUser()` de `Program.cs` (agregada en el plan de autenticación,
  "autenticado por defecto"): actúa a nivel de endpoint de ASP.NET Core, sobre el endpoint
  catch-all que Blazor usa para rutas no mapeadas, *antes* de que el router llegue a
  evaluar el `[AllowAnonymous]` propio de `NotFound.razor`. En la práctica esto es
  correcto y coherente con esa política ya decidida (no se puede navegar a nada sin
  sesión salvo lo explícitamente público) — el wrapper sin-chrome sigue siendo el diseño
  correcto para cuando la página sí se renderiza en contexto anónimo, solo que hoy ese
  contexto no es alcanzable navegando a una URL rota desde cero. Se verificó el circuito
  completo igual: login → `ReturnUrl` → aterriza en la URL original y ahí sí se ve
  `NotFound.razor` (con el chrome completo, por estar ya autenticado).

  Verificado en el navegador: (a) URL inexistente estando logueado como Gestor → tarjeta
  centrada con ícono de lupa, título, descripción y botón "Volver al inicio", dentro del
  layout normal (sidebar + topbar); (b) URL inexistente sin sesión (navegación completa y
  navegación interna) → redirige a Login con `ReturnUrl`, como se espera por la política
  de autenticación; (c) tras loguearse, la redirección aterriza en la URL original y
  muestra `NotFound.razor` correctamente; (d) el botón "Volver al inicio" lleva a `/`. Sin
  errores nuevos en el log del servidor (los únicos hallazgos fueron los falsos positivos
  ya conocidos, más un "Invalid anti-forgery token" en un clic de "Cerrar sesión" que
  coincidió con la misma flakiness de renderer-no-hidratado que ya afecta las capturas de
  pantalla en este entorno de pruebas — no relacionado con este cambio, no reproducible en
  un segundo intento normal).
  Archivo(s): `NotFound.razor`

- [x] **11. Unificar el peso visual del "Iniciar sesión" del header en la landing** — Hecho.
  En `Home.razor` (landing pública), el "Iniciar sesión" de la esquina superior derecha era
  un link de texto plano (`class="text-sm font-medium text-gray-600 hover:text-primary-700"`),
  mientras que "Iniciar sesión"/"Crear una cuenta" del hero ya eran botones estilados para
  la misma acción — inconsistencia menor pero visible en la primera pantalla que ve
  cualquier visitante nuevo.

  Se cambió la clase del link del header a `btn btn-outline btn-sm`, reutilizando la misma
  clase que ya usa el botón "Iniciar sesión" del hero (`btn btn-outline btn-lg`), solo que
  en tamaño chico para no competir visualmente con el resto del header. No hizo falta
  ninguna clase nueva ni recompilar Tailwind, ya que `.btn`/`.btn-outline`/`.btn-sm` ya
  estaban compiladas en `wwwroot/app.css` (se usan en otras páginas).

  Verificado en el navegador, sin sesión iniciada: el "Iniciar sesión" del header ahora se
  ve como un botón outline (borde y texto azul, fondo blanco) del mismo tamaño y estilo
  familiar que el resto de los botones de la app, en vez de un link de texto suelto. Sin
  errores nuevos en el log del servidor (solo el falso positivo ya conocido de
  `HttpsRedirectionMiddleware`).
  Archivo(s): `Home.razor`

- [x] **12. Auditar las páginas que usan `class="card"` en vez de `<Card>`** — Hecho.
  Se revisaron una por una las 8 páginas (`Bandeja.razor`, `MisSolicitudes.razor`,
  `Carrito.razor`, `Administrar.razor`, `GestionRoles.razor`, `HistorialEnvios.razor`,
  `HistorialResoluciones.razor`, `Catalogo.razor`). `Card.razor` en realidad no aporta
  ningún padding propio — es `.card` (borde/fondo/sombra) más `.card-body` (`p-5`) o
  `.card-header` (`px-5 py-4`) alrededor de `@ChildContent`; el `class="card"` crudo solo
  es un problema real si lo que queda adentro no trae su propio padding equivalente.

  Casos encontrados, todos justificados salvo uno:
  - `<div class="card"><Loading /></div>` y `<div class="card"><EmptyState .../></div>`
    (la mayoría de los casos): correctos — `Loading.razor` ya trae `py-10` y
    `EmptyState.razor` ya trae `py-12 px-4`, así que el resultado visual es equivalente a
    usar `<Card>`.
  - `<div class="card"><Pagination .../></div>` (`Administrar.razor`,
    `HistorialEnvios.razor`, `HistorialResoluciones.razor`): correcto y consistente en las
    3 páginas — `Pagination.razor` ya trae su propio `px-4 py-3 border-t`.
  - `<div class="card table-wrap"><table>...</table></div>` (`GestionRoles.razor`):
    correcto — mismo patrón ya usado con tablas en `Catalogo.razor`/`Proveedores.razor`
    (sin padding interno a propósito, para que la tabla llegue al borde).
  - `Carrito.razor` (estado de carrito vacío): **única divergencia real.** El botón "Ir al
    catálogo" estaba como hermano de `<EmptyState>` en vez de ir en su slot `<Action>`
    (a diferencia de `MisSolicitudes.razor`, que sí usa `<Action>` para el mismo patrón de
    carrito/lista vacía) — al no heredar el `px-4` de `EmptyState`, quedaba pegado al
    borde inferior de la tarjeta, sin el aire que sí tiene en `MisSolicitudes.razor`. Se
    movió el `<a>` adentro de `<Action>` para igualar la convención ya establecida.

  No se migró ninguna página a `<Card>` — el `class="card"` crudo en los demás casos ya es
  visualmente idéntico y algunos (`Pagination`, `table-wrap`) ni siquiera podrían usar
  `<Card>` sin agregarle padding no deseado.

  Verificado en el navegador logueado como Solicitante: el carrito vacío ahora muestra el
  botón "Ir al catálogo" con espacio hasta el borde inferior de la tarjeta, igual que
  "Mis solicitudes" con su estado vacío. Sin errores nuevos en el log del servidor (solo
  el falso positivo ya conocido de `HttpsRedirectionMiddleware`).
  Archivo(s): `Carrito.razor`

- [x] **13. Mostrar "Sin precio" en vez de "$0" cuando el producto no tiene precio** — Hecho.
  224 de los 232 productos del catálogo tienen `Precio = 0` (datos reales de una
  importación masiva que no incluía columna de precio, no es un bug de datos) pero la UI
  mostraba "$0", indistinguible de un ítem que de verdad cuesta cero.

  El único lugar donde el catálogo muestra el precio "plano" del producto (no el
  `PrecioProveedor`, que en `ProductosDeProveedor.razor`/`ProveedoresDeProducto.razor` ya
  es nullable y ya cae a "-") es la tabla de `Catalogo.razor`. Se cambió
  `@p.Precio.ToString("C")` por `@(p.Precio > 0 ? p.Precio.ToString("C") : "Sin precio")`
  — sin estilo especial, siguiendo la misma convención ya usada en la app para valores
  ausentes (un simple "-" en texto normal, sin gris atenuado).

  Verificado en el navegador logueado como Solicitante: los productos con `Precio = 0`
  (la gran mayoría) muestran "Sin precio" en la columna Precio; un producto con precio real
  (`Laptop 14"`, buscado por nombre) sigue mostrando el formato de moneda normal
  ("$ 3.200.000"), confirmando que el cambio no afecta productos con precio real. Sin
  errores nuevos en el log del servidor (solo el falso positivo ya conocido de
  `HttpsRedirectionMiddleware`).
  Archivo(s): `Catalogo.razor`

## P3 — Nice-to-have / a confirmar

- [x] **14. Revisar la señal visual de stock bajo vs. stock negativo** — Hecho.
  Valores de stock negativos (`-27`, `-45`, etc.) se ven en rojo, y el badge "Bajo" aparece
  solo en algunas filas — no estaba claro a simple vista si "Bajo" significa "por debajo del
  mínimo configurado" o algo distinto de "cualquier negativo".

  Se revisó la lógica en `Catalogo.razor` antes de tocar nada: son dos señales ya
  independientes por diseño, no un bug — `text-danger-600` en el número aparece cuando
  `Stock < 0` (cualquier negativo, sin importar configuración), y el badge "⚠ Bajo"
  aparece solo cuando el producto tiene `StockMinimo` configurado explícitamente (vía
  "Configurar alerta de stock") y `Stock <= StockMinimo`. Se confirmó contra la base real:
  de 236 productos, 41 tienen stock negativo pero solo 3 tienen `StockMinimo` configurado
  (y de esos, solo 2 cumplen la condición de "Bajo") — por eso en la práctica casi todas
  las filas rojas no muestran el badge, lo que a simple vista se puede leer como
  inconsistencia cuando en realidad es que casi ningún producto tiene una alerta de mínimo
  configurada todavía.

  Como la lógica ya estaba bien separada, la revisión de umbral no requirió cambios — el
  ajuste fue puramente de UI, agregando un tooltip (`title`) a cada señal para que el
  significado se entienda con solo pasar el mouse, sin adivinar: el número en rojo explica
  "Stock negativo: se aprobaron más solicitudes que el stock disponible", y el badge
  explica "Por debajo del mínimo configurado (@StockMinimo)" con el valor exacto
  configurado. El badge no tiene un parámetro `Title`/`AdditionalAttributes` propio, así que se
  envolvió en un `<span title="...">` en vez de modificar el componente compartido
  `Badge.razor` (cambio de alcance mínimo, sin tocar un componente usado en toda la app).

  Verificado en el navegador logueado como Solicitante: "Abrasivo REGULAR" (stock -27,
  mínimo 50 — el único producto con ambas señales a la vez) muestra el número en rojo y el
  badge "⚠ Bajo" juntos, cada uno con su propio tooltip correcto (confirmado leyendo el
  atributo `title` del DOM); un producto con stock positivo y sin mínimo configurado
  (`Laptop 14"`, stock 8) no muestra ninguna señal ni tooltip. Sin errores nuevos en el log
  del servidor (solo el falso positivo ya conocido de `HttpsRedirectionMiddleware`).
  Archivo(s): `Catalogo.razor`

- [x] **15. Confirmar el estado del Pedido #61 en Bandeja** — Hecho.
  Se veía con badge verde "Entregado" pero su línea todavía tenía los botones Aprobar/
  Rechazar activos.

  Se trazó en la base real antes de tocar nada: el Pedido #61 tiene 2 líneas — la
  solicitud #127 (`Estado = Aprobada`) y la #128 (`Estado = Pendiente`) — y sí existe una
  `ConfirmacionEntrega` para el pedido (confirmada el 17/09 por el Gestor de Catálogo). Es
  decir: **es un estado real, no un bug** — un pedido puede tener algunas líneas ya
  aprobadas y entregadas mientras otras líneas del mismo pedido siguen pendientes de
  decisión (por ejemplo, se agregaron después, o quedaron sin resolver en una aprobación
  parcial). `Bandeja.razor` además solo lista líneas `Pendiente` (`ObtenerBandejaAsync`),
  así que cualquier pedido que aparezca ahí con el badge "Entregado" es, por definición,
  siempre este caso mixto — la línea con Aprobar/Rechazar activos es una línea distinta,
  todavía sin resolver, no un resto de acciones ya resueltas.

  Como el estado es real pero no estaba aclarado (el badge "Entregado" no dejaba claro que
  se refería solo a la parte ya aprobada del pedido, no al pedido completo), se agregó una
  nota en `Bandeja.razor` debajo del `<EntregaBadge>`, visible solo cuando el pedido tiene
  una confirmación de entrega: "La entrega corresponde a las líneas ya aprobadas de este
  pedido — las de abajo son otras líneas que todavía están pendientes de resolver."

  Verificado en el navegador logueado como Gestor: el Pedido #61 en Bandeja muestra el
  badge "✓ Entregado el 16/09/2026 19:40 por Gestor de Catálogo" seguido de la nueva nota
  aclaratoria, y debajo la línea de "Agua OXIGENADA * 120 cc" (la #128, todavía pendiente)
  con sus botones Aprobar/Rechazar activos — ahora se entiende por qué conviven ambas
  cosas. Sin errores nuevos en el log del servidor (solo el falso positivo ya conocido de
  `HttpsRedirectionMiddleware`).
  Archivo(s): `Bandeja.razor`

- [x] **16. Confirmar si el Gestor debería ver el ícono de carrito en el header** —
  **Revisado el 2026-09-22: la decisión cambió.**

  La resolución original (dejarlo sin cambios, ver texto tachado abajo) partía de la
  premisa "un Gestor puede armar y enviar su propia solicitud igual que un Solicitante".
  El usuario confirmó explícitamente que esa premisa ya no aplica: el Gestor administra el
  catálogo y aprueba/envía solicitudes ajenas, pero no arma solicitudes propias. Se
  revirtió en las mismas 4 capas que la resolución anterior había verificado como
  consistentes:
  - `Layout/Carrito.razor`: el ícono del carrito pasó de `<AuthorizeView>` (sin rol) a
    `<AuthorizeView Roles="@Roles.Gestor"><NotAuthorized>...</NotAuthorized></AuthorizeView>`
    — se muestra a cualquier autenticado que NO sea Gestor.
  - `Catalogo.razor`: el botón "Agregar"/"En el carrito" quedó envuelto en el mismo patrón
    (antes se mostraba a todos sin condición de rol).
  - `Pages/Solicitudes/Carrito.razor`: además de que ya no hay ningún link visible hacia
    ahí para un Gestor, se agregó un chequeo en `OnInitializedAsync` que redirige a
    `/catalogo` si `authState.User.IsInRole(Roles.Gestor)` — defensa contra entrar por URL
    directa (la página seguía con `[Authorize]` simple, sin restricción de rol).

  Nota de implementación: en este sistema los roles son aditivos, no excluyentes —
  `Register.razor` le da `Roles.Usuario` a todo el que se registra, y `UsuarioRolService`
  (la pantalla de gestión de roles) solo AGREGA `Roles.Gestor` encima, nunca quita
  `Usuario`. Por eso el chequeo es "¿tiene el rol Gestor?" (lo bloquea sin importar si
  también tiene Usuario) y no "¿tiene el rol Usuario?" (que fallaría para un Gestor
  promovido que sí conserva Usuario).

  Verificado: `dotnet build` limpio. No probado clic-por-clic en el navegador.
  Archivos: `Layout/Carrito.razor`, `Catalogo.razor`, `Pages/Solicitudes/Carrito.razor`.

  <details><summary>Resolución original (2026-09-18), ya no vigente</summary>

  Hecho, sin cambios de código. `Layout/Carrito.razor` (el ícono del carrito en el
  topbar) aparece también para un usuario con rol Gestor, cuyo trabajo principal es
  administrar pedidos, no solicitarlos.

  Se confirmó en el código, antes de suponer que era un descuido: `Catalogo.razor` solo
  tiene `[Authorize]` (sin restricción de rol), el botón "Agregar" (agregar al carrito) no
  está condicionado por rol en ningún lado del markup, la página `/solicitudes/carrito`
  tampoco restringe por rol, y `Layout/Carrito.razor` usa `<AuthorizeView>` sin `Roles` —
  se muestra a cualquier usuario autenticado. Es decir, era intencional y consistente en
  las 4 capas (página de catálogo, botón de agregar, página de carrito, ícono del header):
  un Gestor podía armar y enviar su propia solicitud igual que un Solicitante, no solo
  administrar las de otros.

  Verificado en el navegador logueado como Gestor: el catálogo mostraba el botón "Agregar"
  en cada producto (junto con "Editar"/"Proveedores"/"Desactivar", exclusivos de Gestor) y
  el ícono de carrito del header estaba presente y funcional. No se hizo ningún cambio de
  código en ese momento — el comportamiento se consideraba correcto entonces.

  </details>

- [x] **17. Alertar sobre NIT duplicado al crear un proveedor** — Hecho.
  Dos proveedores convivían con el mismo NIT ("1001": "Tiendas Ara" / "Tiendas Ara
  S.A.S") — mismo patrón de falta de validación de unicidad que la tarea 1, pero de menor
  impacto (no es un campo vacío, son datos legítimos con probable duplicado real). A
  diferencia de la tarea 1 (que sí bloqueó con validación dura), acá el pedido explícito
  era un aviso, no necesariamente un bloqueo — son datos reales que pueden ser
  legítimamente iguales (ej. razón social vs. nombre comercial del mismo NIT).

  Se agregó `ProveedoresConMismoNit(string? nit, int? excluirId = null)` en
  `Proveedores.razor`, que compara contra la lista de proveedores ya cargada en la página
  (sin ir a la base ni tocar `ProveedorService`/la capa de Application — no hace falta,
  el propósito es solo informar, no validar en el servidor). Se usa en los dos formularios
  de la página: "Nuevo proveedor" (sin excluir nada) y el modal "Editar proveedor"
  (excluyendo el propio Id que se está editando, para no dispararse consigo mismo). Debajo
  del campo NIT aparece, quieta hasta que el campo pierde el foco, una nota en ámbar (no
  roja, para no leerse como un error de validación bloqueante): "Ya existe un proveedor
  con este NIT: [nombre]." o "Ya existen proveedores con este NIT: [nombre1], [nombre2]."
  según haya uno o más coincidencias — sin bloquear el guardado.

  Verificado en el navegador logueado como Gestor: escribir "1001" en "Nuevo proveedor"
  muestra "Ya existen proveedores con este NIT: Tiendas Ara, Tiendas Ara S.A.S."; escribir
  "1002" (un solo proveedor existente) muestra la variante en singular correctamente
  ("Ya existe un proveedor..."); se guardó un proveedor de prueba con NIT duplicado para
  confirmar que el aviso NO bloquea el submit (se guardó igual, sin error) — se lo borró
  después por SQL, era solo para la prueba. En el modal "Editar proveedor" de "Tiendas
  Ara" (NIT 1001), el aviso muestra únicamente "Tiendas Ara S.A.S." — confirma que la
  exclusión por Id funciona y no se dispara consigo mismo. Sin errores nuevos en el log
  del servidor (solo el falso positivo ya conocido de `HttpsRedirectionMiddleware`).
  Archivo(s): `Proveedores.razor` (y `wwwroot/app.css`, recompilado con `npm run
  build:css` para que Tailwind incluya la clase nueva `text-warning-700`)

- [x] **18. Desactivar un producto era un camino sin retorno y sin rastro** — Hecho
  (2026-09-22).

  El usuario reportó: "cuando un producto es desactivado no se ve a dónde va". Se
  confirmó en el código, no era una percepción: `ProductoService.DesactivarAsync` pone
  `Activo = false`, pero **no existía `ReactivarAsync`** (sí existe para asociaciones
  producto-proveedor, `ReactivarAsociacionAsync`, pero nunca se replicó para el Producto
  en sí). `ObtenerCatalogoAsync` filtra `Where(p => p.Activo)` a nivel de repositorio y no
  había ningún toggle/filtro "ver desactivados" en ninguna pantalla — un producto
  desactivado desaparecía sin dejar ningún rastro navegable, y era irreversible salvo
  editando la base directamente.

  - **Reactivar:** `IProductoService.ReactivarAsync`/`ProductoService.ReactivarAsync`
    nuevos (mismo patrón que `DesactivarAsync`, solo que pone `Activo = true`).
  - **Verlo y reactivarlo:** `ObtenerCatalogoAsync` ganó un parámetro
    `incluirInactivos` (default `false`, no rompe a los callers existentes). En
    `Catalogo.razor`, el Gestor tiene un checkbox "Mostrar desactivados" (deshabilitado si
    hay un filtro de proveedor activo, porque esa rama usa otra consulta que no lo
    soporta) que trae activos e inactivos juntos — fila atenuada (`opacity-60`) + badge
    "Desactivado" junto al nombre, y el botón "Desactivar" se reemplaza por "Reactivar"
    para esas filas.
  - **Avisar en el historial:** `MisSolicitudes.razor`, `Bandeja.razor`,
    `Administrar.razor` y `HistorialResoluciones.razor` muestran el mismo badge
    "Desactivado" junto al nombre del producto cuando `s.Producto?.Activo == false`, para
    que quede claro por qué esa línea no se puede volver a pedir.
  - **`ProveedoresDeProducto.razor`** (antes solo mostraba el estado de la *asociación*,
    nunca el del producto padre) gana un `Alert` de advertencia arriba de la tabla cuando
    el producto está desactivado, con link directo al catálogo para reactivarlo.

  No se tocó `ProductosDeProveedor.razor` (la vista desde el lado del proveedor) — ese
  listado ya filtra por asociación activa y no es el lugar natural para gestionar el
  estado del producto.

  Verificado: `dotnet build` limpio en cada paso; `dotnet test` sigue en 7/7. No probado
  clic-por-clic en el navegador.
  Archivos: `Application/Productos/IProductoService.cs`, `ProductoService.cs`,
  `IProductoRepository.cs`, `Infrastructure/Repositories/ProductoRepository.cs`,
  `Catalogo.razor`, `ProveedoresDeProducto.razor`, `MisSolicitudes.razor`, `Bandeja.razor`,
  `Administrar.razor`, `HistorialResoluciones.razor`.

- [x] **19. Quitar el PDF por línea; dejar por solicitud y por pedido; agregar Excel del
  pedido a proveedor** — Hecho (2026-09-23).

  Pedido del usuario: sacar la exportación PDF de una línea individual (`DetalleSolicitud`)
  — quedaba un botón "PDF" por cada fila en Bandeja/Administrar/Mis solicitudes/Historial
  de resoluciones, redundante con el PDF de la solicitud completa (que ya existía) — y
  agregar una exportación Excel del documento por proveedor con el detalle de códigos
  (el equivalente al PDF de `ExportarPedidoProveedor`, que hasta ahora solo se generaba
  para el adjunto del correo, sin botón de descarga propio).

  - **PDF por línea, eliminado:** `IPdfExportService.ExportarSolicitud(DetalleSolicitud)` +
    su implementación en `PdfExportService`, el endpoint `GET /api/solicitudes/{id}/pdf` en
    `Program.cs`, y los 4 botones "PDF" (uno por línea) en `Administrar.razor`,
    `Bandeja.razor`, `HistorialResoluciones.razor` y `MisSolicitudes.razor` (columnas de
    tabla correspondientes también removidas). Se mantienen intactos:
    - **Por solicitud:** `ExportarPedido(Solicitud)` vía `GET /api/pedidos/{id}/pdf` —
      botón "PDF de la solicitud" en Bandeja/Administrar/Mis solicitudes.
    - **Por pedido (documento a un proveedor):** `ExportarPedidoProveedor` vía
      `GET /api/pedidos-proveedor/{id}/pdf` — botón "PDF del pedido" en `HistorialEnvios.razor`.
  - **Excel del pedido a proveedor, agregado:** nuevo endpoint
    `GET /api/pedidos-proveedor/{id}/excel` en `Program.cs` (mismo patrón que su hermano
    PDF: recarga el documento por Id, descarta líneas `Excluido`, usa
    `IExcelExportService.ExportarPedidoProveedor` — que ya existía para el adjunto de
    correo, solo le faltaba un endpoint de descarga propio). Botón "Excel" nuevo en
    `HistorialEnvios.razor`, al lado de "PDF del pedido" — solo aparece cuando el envío
    tiene un documento `PedidoProveedor` real (`PedidoProveedorId` no nulo); las filas
    legacy sin documento (previas a esta funcionalidad) no lo muestran, porque no hay
    snapshot por proveedor del que generarlo.

  Verificado: `dotnet build` limpio, `dotnet test` 7/7, grep sin referencias colgantes al
  método/endpoint eliminados. No probado clic-por-clic en el navegador.
  Archivos: `Application/Exportacion/IPdfExportService.cs`,
  `Infrastructure/Pdf/PdfExportService.cs`, `Program.cs`, `Administrar.razor`,
  `Bandeja.razor`, `HistorialResoluciones.razor`, `MisSolicitudes.razor`,
  `HistorialEnvios.razor`.

- [x] **20. Administrar solicitudes: agrupar por proveedor + gestionar asociaciones
  inline + Categoría en el Excel/PDF del pedido** — Hecho (2026-09-23).

  Pedido del usuario, con un diseño propuesto y confirmado antes de tocar código (3
  opciones con mockups; eligió la opción "tabla agrupada por proveedor" con una vuelta de
  tuerca: que también se pudiera asociar/editar/quitar proveedor sin salir de la pantalla).

  **Categoría en el detalle del pedido a proveedor** — `DetallePedidoProveedor` gana un
  campo `Categoria` (snapshot, igual criterio que `ProductoNombre`/`UnidadMedida`: congelada
  al momento del envío, no se lee en vivo del catálogo). Migración nueva
  `AgregarCategoriaADetallePedidoProveedor` (`ALTER TABLE ADD COLUMN` nullable, aplicada).
  Columna "Categoría" agregada tanto al Excel (`ExcelExportService.ExportarPedidoProveedor`)
  como al PDF hermano, para que ambos formatos queden consistentes.
  Archivos: `Domain/Entities/DetallePedidoProveedor.cs`, `AppDbContext.cs`,
  `PedidoNotificacionProveedorService.cs` (`ConstruirSnapshots`),
  `ExcelExportService.cs`, `PdfExportService.cs`, migración nueva.

  **Administrar solicitudes reorganizada** (solo en la vista SIN filtro de proveedor — con
  un proveedor filtrado se mantiene la tabla plana de siempre, que ya tiene sentido ahí):
  la única tabla plana por solicitud se reemplazó por una lista de secciones, una por
  proveedor de destino (usando la misma cobertura que ya calculaba
  `ObtenerProveedoresDisponiblesAsync` — nada de lógica nueva de negocio, solo de
  presentación), cada una con su propio botón "Enviar a este proveedor"/"Reenviar" en vez
  del dropdown único de antes (que queda solo para la vista filtrada). Se agregan dos
  secciones más para no perder ninguna línea de vista: "Sin proveedor asociado" (con un
  botón "Asociar proveedor" inline si el producto no tiene ninguna asociación de catálogo,
  o un link a `/catalogo/{id}/proveedores` si la tiene pero no está disponible ahora —
  desactivada o excluida) y "Pendientes / rechazadas" (informativa, sin acciones — resolver
  líneas sigue siendo trabajo de Bandeja).

  **Gestión de asociación inline** — cada línea dentro de un grupo de proveedor gana un
  ícono de lápiz que abre un modal para editar código/precio/preferido o quitar la
  asociación (busca el Id de la asociación al vuelo con
  `ObtenerProveedoresDeProductoAsync`, ya que las líneas de esta pantalla no lo traían
  precargado). El modal reutiliza el mismo `IProveedorService`/`AsociarProveedorDto` que
  `/catalogo/{id}/proveedores` — ni un mecanismo aparte ni una tabla paralela: es el mismo
  dominio, solo expuesto sin salir de Administrar solicitudes (se pierde el contexto de
  filtros/página si hay que navegar a otra pantalla para cada producto huérfano de un
  pedido con varios).

  Verificado: `dotnet build` limpio, `dotnet test` 7/7, migración aplicada sin errores, la
  app arrancó y `/solicitudes/administrar` respondió sin excepciones en el log. No probado
  clic-por-clic en el navegador.
  Archivos: `Administrar.razor` (reescritura grande de la sección de tabla + nuevo modal +
  métodos `LineasSinCobertura`/`YaEnviadoA`/`TieneAsociacionEnCatalogo`/
  `AbrirAsociarProveedor`/`AbrirEditarAsociacion`/`GuardarAsociacion`/
  `DesactivarAsociacionDesdeModal`).

- [x] **21. Bug real encontrado probando la tarea 20 en el navegador: un producto con una
  asociación de catálogo vieja/inactiva quedaba bloqueado para SIEMPRE** — Hecho
  (2026-09-23).

  El usuario pidió "probalo en el navegador y contame qué falla". Con Gestor logueado:
  asocié un producto huérfano ("Abrasivo REGULAR") a un proveedor nuevo desde el modal de
  la tarea 20 — la asociación se creó bien (verificado por SQL), pero el producto se quedó
  en "Sin proveedor asociado" en vez de pasar a un grupo nuevo. Confirmé que no era solo
  visual: `EnviarAProveedorAsync` habría rechazado el envío con el mismo motivo, porque usa
  el mismo cálculo.

  **Causa raíz:** `ObtenerCoberturaPreviaAsync` marcaba un producto como "ya cubierto" por
  un proveedor si tenía **cualquier** asociación de catálogo con él (activa o no, usada o
  no), en vez de mirar qué contenía el documento que **de verdad** se le envió. Bastaba con
  una asociación vieja y jamás usada, con un proveedor que ya recibió cualquier otra cosa
  de la misma solicitud, para bloquear el producto de ofrecerse a cualquier proveedor —
  incluso uno recién asociado.

  **Arreglo:** `ObtenerCoberturaPreviaAsync` ahora calcula la cobertura desde
  `PedidoProveedor.Items` (el snapshot real de lo enviado, ya cargado vía
  `pedidosProveedor.ObtenerPorSolicitudAsync`) en vez de desde `ProductoProveedor` (el
  catálogo). De paso corrige otro efecto secundario del mismo bug: una línea `Excluido`
  ahora sí queda libre para ofrecerse a otro proveedor (antes tampoco lo estaba, por el
  mismo cálculo de más).

  Agregué un test nuevo (`ObtenerProveedoresDisponibles_ProductoConAsociacionInactivaAOtroProveedorYaConEnvio_SigueOfreciendoseANuevoProveedor`)
  que reproduce el bug exacto y falla sin el arreglo. Los 7 tests anteriores siguen en
  verde — confirmé además con SQL que un producto que SÍ estaba genuinamente en el
  documento ya enviado (no solo asociado) sigue bloqueado correctamente, como debe ser.

  Verificado: `dotnet build` limpio, `dotnet test` 8/8, y en el navegador con Gestor.
  Archivos: `PedidoNotificacionProveedorService.cs` (`ObtenerCoberturaPreviaAsync` y sus 2
  call sites), `PedidoNotificacionProveedorServiceTests.cs` (test nuevo).

## P4 — Refinamiento visual y de navegación (ronda 2026-09-23)

Pedido del usuario: análisis de cómo mejorar la UI/UX para que se sienta más
familiarizada/intuitiva — "los botones de eliminar y el verde o el rojo chillón" y
"opciones de navegación muy intuitivas", manteniendo el estilo moderno ya existente.
Recorrido de `Styles/app.tailwind.css` + `Components/UI/` + `Sidebar.razor` antes de
proponer nada: el sistema de diseño ya es maduro (21 tareas de este mismo plan) — no hacía
falta un rediseño amplio, dos hallazgos puntuales explicaban el pedido.

- [x] **22. Botones Success/Danger sólidos "chillones" — inconsistentes con el resto del
  propio sistema de diseño** — Hecho.

  Causa raíz: el sistema YA resuelve esto bien en casi todos lados — `Badge`, `StatCard`,
  la barra de acento del ítem activo del sidebar — con un patrón consistente de tinte
  translúcido (15% de opacidad) en vez de relleno sólido saturado (el propio comentario
  del CSS del nav activo ya lo dice: "más elegante que un relleno sólido"). `.btn-success`/
  `.btn-danger` eran la excepción: relleno sólido a saturación completa (verde/rojo
  estándar de Tailwind vía alias) con texto blanco, aclarando aún más al hover (`-600`
  base → `-500` hover) — literal opuesto a la restricción visual que el resto de la app ya
  se autoimpone.

  Dos cambios, no uno:
  1. **Relleno sólido, menos "neón"**: `.btn-success`/`.btn-danger` pasan de base `-600`/
     hover `-500` a base `-700`/hover `-600` — más profundos, oscurecen al hover en vez de
     aclarar (mismo criterio "considerado" que ya usa el resto de la paleta oscura).
  2. **Nueva variante `ButtonVariant.DangerSubtle`** (`.btn-danger-subtle`, mismo patrón
     que `.btn-outline` ya usa para Primary — borde + texto tintado, fondo transparente),
     para los botones que **disparan** una acción destructiva (abren un `ConfirmDialog` o
     un modal) en vez de ejecutarla directo. El rojo sólido queda reservado para la acción
     **real**: el botón "Confirmar" de `ConfirmDialog`, o cualquier acción de un solo clic
     sin paso de confirmación intermedio.

  Revisé los 13 usos de `Danger` y los 7 de `Success` uno por uno antes de decidir cuáles
  tocar — no se aplicó una regla mecánica. Pasaron a `DangerSubtle` los 10 que son
  disparadores o acciones frecuentes/reversibles sin confirmación: "Desactivar" en
  Catálogo/Proveedores/Empresas/Sedes/`ProductosDeProveedor`/`ProveedoresDeProducto` (los 6
  abren `ConfirmDialog`), "Rechazar todo"/"Rechazar" en Bandeja (abren el modal de
  resolución, 2 lugares) y su toggle de selección dentro del modal (3er lugar en Bandeja,
  no es la confirmación final), y "Quitar" del carrito (ícono suelto, sin confirmación,
  pero de bajo riesgo — se puede volver a agregar). Quedaron en `Danger` sólido, sin
  cambios, los 3 que sí son la acción final: "Quitar proveedor" en el modal de
  `Administrar.razor` (ejecuta directo, sin otro paso), el botón "Aprobar"/"Rechazar" que
  literalmente confirma el modal de Bandeja, y "Quitar gestor" en `GestionRoles.razor`
  (acción directa, sin diálogo intermedio — no se le agregó uno, estaba fuera del pedido).
  `Success` no ganó una variante subtle propia: sus 7 usos son "Reactivar"/"Activar" (un
  solo clic, sin confirmación, y aprobar es una acción de menor riesgo que desactivar/
  eliminar) — no había la misma asimetría de severidad que sí justificaba separar Danger en
  dos niveles.

  Verificado con `dotnet build` (0 errores/advertencias), `npm run build:css` (confirmé con
  grep que `.btn-danger-subtle` quedó en `wwwroot/app.css` compilado), `dotnet test` (16/16
  sin regresiones) y smoke test no interactivo de 4 rutas con botones tocados (Catálogo,
  Bandeja, Proveedores, Empresas — sin excepciones en el log). **No se verificó visualmente
  en el navegador** (el ajuste es de color/opacidad, no de lógica — corresponde revisarlo
  quien lo pueda ver renderizado; ver memoria `feedback-no-browser-testing`).
  Archivos: `Components/UI/ButtonVariant.cs`, `Components/UI/Button.razor`,
  `Styles/app.tailwind.css` (y su salida compilada `wwwroot/app.css`), `Catalogo.razor`,
  `ProveedoresDeProducto.razor`, `Empresas.razor`, `Sedes.razor`, `ProductosDeProveedor.razor`,
  `Proveedores.razor`, `Bandeja.razor` (3 lugares), `Carrito.razor`.

- [x] **23. Sidebar sin agrupar — 11 ítems bajo un solo encabezado "Gestión"** — Hecho.

  Entre lo que ya había (Bandeja, Administrar, 2 Historiales, Nuevo producto, Importar,
  Proveedores, Gestión de roles) y lo agregado en la sesión del plan de Empresas (Empresas,
  Sedes, Consumo por empresa — ver `docs/PLAN_EMPRESAS_FILIALES.md`), el menú de Gestor
  había crecido a una lista plana de 11 ítems sin ninguna jerarquía — difícil de escanear,
  la misma "adopción inconsistente" que motivó este plan, esta vez en la propia navegación.

  Se reagrupó en 6 sub-secciones, reusando el mismo patrón de encabezado que ya existía
  para "Gestión" (cero CSS nuevo): **Solicitudes** (Bandeja/Administrar/2 Historiales),
  **Catálogo** (Nuevo producto/Importar), **Proveedores**, **Organización** (Empresas/
  Sedes), **Reportes** (Consumo por empresa), **Usuarios** (Gestión de roles). Se extrajo
  el patrón repetido (`@if (!Collapsed) { <p>...</p> } else { <div class="border-t">...` })
  a un componente nuevo `SidebarGroupLabel.razor` — antes solo se usaba una vez, ahora se
  repite 6 veces, dejarlo inline habría significado seis copias idénticas del mismo
  `@if`/`@else`. **(Nota: este componente se reemplazó por `SidebarGroup.razor` en la
  tarea 26 de esta misma ronda — ver esa tarea.)**

  No se tocaron: la estructura de rutas (ningún link cambió de URL), el ítem "Catálogo" ni
  "Mis solicitudes" (fuera del bloque de Gestor, sin agrupar a propósito — son de un solo
  ítem cada uno). Las ideas C del análisis (buscador tipo "Cmd+K", mover Nuevo
  producto/Importar dentro de Catálogo.razor como acciones en vez de entradas de menú)
  quedaron fuera de esta tarea — se marcaron como "a validar" en el análisis, no se
  implementaron sin confirmar.

  Verificado con `dotnet build` (0 errores/advertencias), `dotnet test` (16/16 sin
  regresiones) y smoke test no interactivo de 4 rutas (sin excepciones en el log). **No se
  verificó visualmente en el navegador** (ver memoria `feedback-no-browser-testing`).
  Archivos: `Components/Layout/Sidebar.razor`, `Components/Layout/SidebarGroupLabel.razor`
  (nuevo, reemplazado después).

- [x] **24. "Mi cuenta" sin forma explícita de salir** — Hecho.

  El usuario reportó: "no se puede salir de ahí sino dando clic en otro lado". Confirmado
  en el código: `ManageNavMenu.razor` (la tarjeta "Mi cuenta" que acompaña las 14 páginas
  de `Account/Manage/*`) solo tenía links a las sub-secciones internas (Perfil, Email,
  Contraseña, etc.) — ningún link de "volver"/"cerrar". La única salida real era clicar
  algo del sidebar principal (que sigue visible, `ManageLayout.razor` usa el `MainLayout`
  normal), pero sin ningún indicio ahí mismo de que esa era la forma de salir.

  Se agregó un botón "← Volver" en el `HeaderActions` de la tarjeta "Mi cuenta" (slot que
  `Card.razor` ya tenía pero ningún lugar de la app usaba todavía), al lado del título,
  apuntando a `/`. Queda visible en las 14 páginas de `Account/Manage/*` porque todas
  comparten el mismo `ManageNavMenu.razor`.

  Verificado con `dotnet build` (0 errores/advertencias) y smoke test no interactivo de
  `/Account/Manage` y `/Account/Manage/Email` (sin excepciones en el log). **No se
  verificó visualmente en el navegador** (ver memoria `feedback-no-browser-testing`).
  Archivos: `Components/Account/Shared/ManageNavMenu.razor`.

- [x] **25. No se podía editar el nombre completo desde "Mi cuenta"** — Hecho.

  `Account/Manage/Index.razor` (Perfil) mostraba "Usuario" (el username/email) como campo
  deshabilitado, y tenía Teléfono/Dirección editables — pero `NombreCompleto` (el nombre
  que se pide obligatorio al registrarse, y que queda como snapshot en
  `SolicitanteNombre`/`GestorNombre` de cada solicitud) no tenía ningún campo en esta
  pantalla, en ningún lado de la app.

  Se agregó un campo "Nombre completo" al formulario de Perfil, con la misma validación
  `[Required]` que ya usa `Register.razor` para el mismo campo (mismo mensaje de error,
  mismo `[Display(Name = "Nombre completo")]`). El guardado se unificó con el de
  Dirección (antes solo guardaba si cambiaba la dirección; ahora guarda si cambió
  cualquiera de los dos, en un solo `UpdateAsync`) — Teléfono sigue aparte porque usa
  `SetPhoneNumberAsync` (un setter propio de `UserManager`, no una propiedad plana como
  `NombreCompleto`/`DireccionPredeterminada`).

  Cambiar el nombre acá **no** altera retroactivamente `SolicitanteNombre`/`GestorNombre`
  de solicitudes ya creadas — son snapshots, mismo criterio ya documentado en la tarea 7
  de este plan (dato histórico, no se recalcula).

  Verificado con `dotnet build` (0 errores/advertencias) y smoke test no interactivo de
  `/Account/Manage` (sin excepciones en el log). **No se verificó visualmente en el
  navegador** (ver memoria `feedback-no-browser-testing`).
  Archivos: `Components/Account/Pages/Manage/Index.razor`.

- [x] **26. Las 6 sub-secciones del sidebar (tarea 23) ahora se pueden esconder** — Hecho.

  Pedido explícito del usuario, como continuación directa de la tarea 23: que cada una de
  las 6 sub-secciones nuevas del sidebar de Gestor se pueda ocultar/mostrar.

  `SidebarGroupLabel.razor` (un encabezado estático) se reemplazó por `SidebarGroup.razor`
  — ahora envuelve sus links (`ChildContent`) en vez de ser solo una etiqueta suelta antes
  de ellos, y el encabezado es un `<button>` con estado propio (`expandido`, default
  `true` — nada queda escondido de entrada, coherente con el comportamiento actual; el
  usuario decide qué colapsar) que alterna un ícono `chevron-down`/`chevron-right`. Con el
  sidebar entero colapsado (modo solo-íconos) el acordeón no aplica — ahí se sigue viendo
  como un separador simple, siempre expandido, porque no hay texto que esconder.

  El estado de cada grupo (abierto/cerrado) vive en el propio componente `SidebarGroup`,
  así que persiste mientras dure la sesión/circuito (navegar entre páginas no lo resetea,
  porque el sidebar es parte del layout persistente) pero no sobrevive un refresh completo
  del navegador — no se agregó persistencia en `localStorage`, se consideró fuera de
  alcance del pedido ("que se esconda" no pedía que se recuerde entre sesiones).

  Verificado con `dotnet build` (0 errores/advertencias), `dotnet test` (16/16 sin
  regresiones) y smoke test no interactivo (sin excepciones en el log). **No se verificó
  visualmente en el navegador** (ver memoria `feedback-no-browser-testing`).
  Archivos: `Components/Layout/SidebarGroup.razor` (nuevo, reemplaza a
  `SidebarGroupLabel.razor`, eliminado), `Components/Layout/Sidebar.razor`.
