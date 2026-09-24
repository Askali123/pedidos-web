# Plan de mejoras — autenticación y autorización

Checklist de trabajo, de mayor a menor prioridad, salida del análisis hecho el 2026-09-17
sobre el sistema de autenticación (ASP.NET Core Identity + cookies). El hallazgo más
grave: **la recuperación de contraseña está rota hoy para todo usuario auto-registrado**
(no llega a fallar por "no configuré SMTP" — ni siquiera genera el token, porque el email
de confirmación de Identity es un no-op fijo y "Olvidé mi contraseña" exige la cuenta
confirmada). Aparece junto con un bug sistémico de concurrencia de `DbContext` — ya
detectado y arreglado en 2 páginas durante el plan anterior — que sigue sin corregirse en
otras 13 páginas de "Administrar cuenta", con alta probabilidad de que hoy tiren excepción
al abrirse. Cada tarea se marca `[x]` al completarse; no reordenar sin avisar — el orden
es la prioridad acordada.

## Decisiones de diseño ya acordadas

- **Email de Identity**: en vez de dejar `IdentityNoOpEmailSender` (no-op fijo,
  `Program.cs`) o solo destrabar "Olvidé mi contraseña" quitando el requisito de cuenta
  confirmada, se conecta el sender de Identity a la infraestructura SMTP que ya existe
  (`CatalogoPedidos.Application.Notificaciones.IEmailSender`, la misma que usa el envío a
  proveedores — ya soporta el interruptor `Smtp:Host` configurado/no configurado). Con
  esto, confirmación de cuenta y recuperación de contraseña mandan correos reales cuando
  hay SMTP configurado, y se mantiene `RequireConfirmedAccount`/el gate de
  `ForgotPassword.razor` tal como están — es la infraestructura de correo la que estaba
  mal conectada, no la regla de negocio.
- **Gestión de roles**: pantalla nueva y acotada, visible solo para `Roles.Gestor`, que
  lista los usuarios registrados y permite asignar/quitar el rol Gestor con un toggle —
  no un panel de administración completo (no se tocan otros datos del usuario desde ahí).
  Hoy la única forma de crear un segundo gestor es editar la base directamente.

## P0 — Bugs rotos hoy

- [x] **1. Conectar el email de Identity a la infraestructura SMTP real** — **Hecho.**
  `Program.cs` registra `IEmailSender<ApplicationUser>` como
  `IdentityNoOpEmailSender` sin condición — nunca manda nada, sin importar si
  `Smtp:Host` está configurado. Como además `Register.razor` nunca marca
  `EmailConfirmed = true` (solo las 2 cuentas semilla de `Seed.cs` lo tienen), y
  `ForgotPassword.razor` exige `IsEmailConfirmedAsync(user)` antes de generar el token
  de recuperación, **todo usuario auto-registrado que olvida su contraseña queda sin
  ningún camino de recuperación** — el flujo no falla ruidosamente, simplemente no
  genera el token y redirige igual a la pantalla de confirmación.
  Se reemplazó `IdentityNoOpEmailSender` por `IdentityEmailSender`, que delega los 3
  métodos de la interfaz (`SendConfirmationLinkAsync`, `SendPasswordResetLinkAsync`,
  `SendPasswordResetCodeAsync`) en `CatalogoPedidos.Application.Notificaciones
  .IEmailSender` — mismo mecanismo que ya usa `PedidoNotificacionProveedorService` para
  el envío a proveedores. Registrado como `Scoped` (no `Singleton`, como estaba antes)
  porque el `IEmailSender` de Application también es `Scoped` — un `Singleton`
  reteniéndolo sería una captive dependency. A propósito **best-effort**: si el envío
  real falla (`SmtpEmailSender` propaga la excepción), se loguea y se traga en vez de
  dejarla subir — la acción principal (crear la cuenta, generar el token de reseteo) ya
  se completó antes de llamar al sender, y las pantallas de Identity siempre muestran el
  mismo mensaje genérico pase lo que pase con el correo (para no filtrar si una cuenta
  existe/está confirmada) — dejar que la excepción rompiera esa pantalla habría sido
  peor que perder el correo. De paso se limpió `RegisterConfirmation.razor`: tenía un
  atajo de desarrollo que mostraba el link de confirmación directo en la página cuando
  detectaba el sender no-op (ya inalcanzable en la práctica, porque
  `RequireConfirmedAccount = false` hace que `Register.razor` ni siquiera redirija a esa
  pantalla — el registro deja logueado directo).
  Verificado en el navegador contra el SMTP real ya configurado (`Smtp:Host` =
  `smtp.gmail.com`, credenciales existentes en `user-secrets`): (1) registré una cuenta
  nueva con un email de dominio ficticio y el log del servidor confirmó `Correo enviado
  a ... — Confirmá tu cuenta — CatalogoPedidos` (antes de este fix, ningún correo se
  mandaba nunca, ni siquiera se intentaba); (2) pedí "Olvidé mi contraseña" con la
  cuenta semilla `gestor@catalogo.local` (`EmailConfirmed = true`) y el log confirmó
  `Correo enviado a gestor@catalogo.local — Restablecer tu contraseña —
  CatalogoPedidos`; (3) repetí "Olvidé mi contraseña" con la cuenta recién registrada
  (todavía sin confirmar) y, aunque la pantalla mostró el mismo mensaje genérico "Si el
  email existe en el sistema..." que en el caso anterior (sin filtrar información), el
  conteo de correos enviados en el log siguió en 2 — confirmando que el gate de
  `IsEmailConfirmedAsync` sigue intacto y de verdad no genera token para cuentas sin
  confirmar.
  Archivo(s): `IdentityEmailSender.cs` (nuevo, reemplaza `IdentityNoOpEmailSender.cs`),
  `Program.cs`, `RegisterConfirmation.razor`

- [x] **2. Arreglar la concurrencia de `DbContext` en el resto de "Administrar cuenta"** — **Hecho.**
  El plan anterior encontró y arregló (`Manage/Index.razor`, `Carrito.razor`) que
  cualquier componente cuyo propio `OnInitializedAsync` llame a
  `UserManager.GetUserAsync`/`SignInManager` sobre la instancia inyectada
  directamente choca con la llamada idéntica de `Layout/UserMenu.razor` sobre el
  mismo `AppDbContext` Scoped compartido — `InvalidOperationException: A second
  operation was started on this context instance...`, reproducible al 100%. El
  arreglo (inyectar `IServiceScopeFactory` y resolver `UserManager`/`SignInManager`
  desde un `CreateAsyncScope()` aislado) nunca se había aplicado al resto de páginas de
  Identity, que usaban exactamente el mismo patrón sin scope: `ChangePassword.razor`,
  `DeletePersonalData.razor`, `Disable2fa.razor`, `Email.razor`,
  `EnableAuthenticator.razor`, `ExternalLogins.razor`, `GenerateRecoveryCodes.razor`,
  `Passkeys.razor`, `PersonalData.razor`, `RenamePasskey.razor`,
  `ResetAuthenticator.razor`, `SetPassword.razor`, `TwoFactorAuthentication.razor` — se
  aplicó el mismo patrón a las 13, incluyendo el detalle no obvio de NO reusar en un
  handler de evento (`OnValidSubmitAsync`, `OnSubmitAsync`, etc.) el `ApplicationUser`
  cargado en `OnInitializedAsync` (quedó rastreado por el `DbContext` de un scope ya
  cerrado) — cada handler vuelve a pedirlo fresco con el `UserManager` de SU PROPIO
  scope. Casos particulares: `ExternalLogins.razor` también resuelve `IUserStore`
  desde el scope aislado (usa el mismo `AppDbContext` por dentro) y pasa el
  `UserManager`/`SignInManager` ya abiertos a su callback interno
  `OnGetLinkLoginCallbackAsync` (se invoca sincrónicamente dentro del mismo
  `OnInitializedAsync`, no necesita scope propio); `Passkeys.razor` (`UpdatePasskey` →
  `DeletePasskey`) sigue el mismo criterio.
  Verificado en el navegador con dos niveles de profundidad: (1) carga simple — las 13
  páginas abren sin la excepción de concurrencia (antes del fix, el mismo patrón
  reproducía el error al 100% en `Index.razor`/`Carrito.razor`); (2) flujo completo —
  cambié la contraseña de una cuenta de prueba de punta a punta en `ChangePassword.razor`
  (el caso más sensible al gotcha de reusar el `user` entre scopes) y terminó con "Your
  password has been changed", sin error de tracking.
  **Hallazgo aparte, no relacionado con este fix**: al probar `Passkeys.razor` encontré
  un bug real y distinto — tira `InvalidOperationException: This operation is not
  permitted because the underlying 'DbContext' does not include 'IdentityUserPasskey'1'
  in its model... 'IdentityOptions.Stores.SchemaVersion' is set to
  'IdentitySchemaVersions.Version3' or higher.` Es decir: los passkeys nunca funcionaron
  en este proyecto (contradice lo que había asumido el análisis inicial de que "ya
  estaban funcionando, solo sin probar") — falta configurar `SchemaVersion` Y, más de
  fondo, `AppDbContext` no tiene el constructor `(options, IOptions<IdentityOptions>)`
  que EF necesita para que el modelo de Identity sepa qué versión de esquema usar al
  construirse. Arreglarlo de verdad implica una migración nueva (la tabla de passkeys no
  existe en la base). Lo dejo afuera de esta tarea (alcance distinto, cambio de otro
  tamaño) — a definir si se suma como tarea nueva al plan.
  Archivo(s): 13 páginas en `Components/Account/Pages/Manage/`

## P1 — Controles de seguridad ausentes

- [x] **3. Activar el bloqueo por fuerza bruta en el login** — **Hecho.**
  `Login.razor` llamaba a `PasswordSignInAsync(..., lockoutOnFailure: false)` — el
  propio comentario en el código decía "To enable password failures to trigger
  account lockout, set lockoutOnFailure: true". Se cambió a `true` y se configuró
  explícitamente `options.Lockout.AllowedForNewUsers`, `MaxFailedAccessAttempts` (5) y
  `DefaultLockoutTimeSpan` (5 min) en `DependencyInjection.cs` — mismos valores que ya
  traía Identity por defecto, pero ahora documentados como decisión deliberada en vez de
  un default implícito. El manejo de `result.IsLockedOut` en `Login.razor` (redirige a
  `Account/Lockout`) ya estaba armado desde antes — nunca se disparaba porque
  `lockoutOnFailure` estaba en `false`.
  Verificado en el navegador con la cuenta de prueba de la tarea 1: 5 intentos con
  contraseña incorrecta (uno de los clics no llegó a registrarse — confirmado por SQL
  que `AccessFailedCount` iba en 4 antes del 5.º clic real) terminaron redirigiendo a
  "Cuenta bloqueada"; `LockoutEnd` en la base quedó ~5 minutos por delante de la hora
  actual, tal como se configuró. Con la cuenta ya bloqueada, probé de nuevo con la
  contraseña **correcta** y igual redirigió a "Cuenta bloqueada" (el chequeo de lockout
  corre antes de validar la contraseña). Confirmé que el bloqueo es por cuenta, no
  global: `gestor@catalogo.local` inició sesión sin problema mientras la otra cuenta
  seguía bloqueada.
  Archivo(s): `Login.razor`, `DependencyInjection.cs`

- [x] **4. Pantalla de gestión de roles (asignar/quitar Gestor)** — **Hecho.**
  Nueva página `/usuarios/roles` (solo Gestor, link en el sidebar): lista todos los
  usuarios registrados con su rol actual (badge) y un botón "Hacer gestor"/"Quitar
  gestor" por fila, vía el nuevo `IUsuarioRolService` (`Application/Usuarios`,
  implementado en `Infrastructure/Identity` sobre `UserManager.AddToRoleAsync`/
  `RemoveFromRoleAsync`/`IsInRoleAsync`). El botón "Quitar gestor" se deshabilita en la
  UI cuando quedaría 0 gestores, y el servicio tira `InvalidOperationException` con el
  mismo motivo si de todas formas se llega a invocar (defensa en profundidad, probado
  aparte de la UI). La página sigue el mismo patrón de scope aislado que el resto de
  "Administrar cuenta" (`IServiceScopeFactory` + resolver `IUsuarioRolService` — que por
  dentro usa `UserManager` — desde un scope propio en cada operación), porque comparte
  layout con `UserMenu.razor`.
  Decisión de alcance: al quitarse el rol a uno mismo, NO se intenta refrescar el cookie
  de sesión al toque — esta página usa clicks interactivos normales (como
  `Administrar.razor`/`Bandeja.razor`), no un form POST como `ChangePassword.razor`, así
  que la respuesta HTTP ya se mandó y el cookie no se puede reescribir desde ahí. En vez
  de eso, el mensaje avisa que hay que cerrar sesión y volver a entrar para que se
  refleje.
  Verificado en el navegador como `gestor@catalogo.local` (único gestor al empezar):
  "Quitar gestor" aparecía deshabilitado en su propia fila ("(vos)"), con "1 gestor(es)"
  en el resumen; le asigné el rol a `usuario@catalogo.local` ("Usuario de Prueba ahora es
  Gestor.", confirmado por SQL en `AspNetUserRoles`), lo que habilitó "Quitar gestor" en
  ambas filas; se lo quité de nuevo ("Se le quitó el rol Gestor a Usuario de Prueba.") y
  volvió a quedar deshabilitado en la fila del único gestor restante. Aparte, con un
  script descartable llamé a `QuitarGestorAsync` directo sobre el servicio (sin pasar
  por el botón deshabilitado) contra `gestor@catalogo.local` siendo el único gestor, y
  tiró la excepción esperada — confirmando el guard también a nivel de servicio, no solo
  en la UI. Se confirmó por SQL que la base quedó exactamente como al empezar (un solo
  gestor).
  Archivo(s): `UsuarioRolDto.cs`, `IUsuarioRolService.cs`, `UsuarioRolService.cs`,
  `DependencyInjection.cs`, `GestionRoles.razor`, `Sidebar.razor`

- [x] **5. Política de autorización "autenticado por defecto"** — Hecho.
  `Program.cs` registraba `AddAuthorization()` sin fallback policy: cada página o
  endpoint nuevo debía acordarse de poner `[Authorize]` explícitamente, o quedaba
  público sin que nadie lo notara. De hecho, así estaban hoy `/catalogo`,
  `/catalogo/nuevo`, `/proveedores`, `/solicitudes/*` — protegidas solo porque el
  link en `Sidebar.razor` estaba dentro de un `AuthorizeView`, no porque la página
  en sí exigiera sesión: cualquiera que conociera la URL entraba igual sin loguearse.

  Se invirtió el default: `Program.cs` ahora registra
  `AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())`,
  así que cualquier página o endpoint sin `[Authorize]`/`[AllowAnonymous]` explícito
  exige sesión iniciada por defecto. Antes de aplicarlo hubo que mapear con
  cuidado dos mecanismos separados que ambos quedan alcanzados por esta política:

  - **Páginas Razor** (`@page`): se relevaron todas las rutas públicas reales —
    contando que `Manage/_Imports.razor` ya le pone `[Authorize]` a las 13+
    páginas de esa carpeta por atributo de carpeta (no se ve con grep por
    archivo) — y se les agregó `@attribute [AllowAnonymous]` explícito a las 20
    que deben seguir siendo públicas: `Pages/Home.razor`, `Pages/Error.razor`,
    `Pages/NotFound.razor`, y 17 páginas de `Account/Pages/*.razor` (todo el
    flujo previo al login: Login, Register, RegisterConfirmation,
    ForgotPassword y su confirmación, ResetPassword y su confirmación,
    ResendEmailConfirmation, ConfirmEmail, ConfirmEmailChange, ExternalLogin,
    LoginWith2fa, LoginWithRecoveryCode, Lockout, AccessDenied, InvalidUser,
    InvalidPasswordReset).
  - **Endpoints minimal-API**: los 6 `/api/*` ya tenían `.RequireAuthorization()`
    explícito, sin cambios. En `IdentityComponentsEndpointRouteBuilderExtensions.cs`,
    de los 4 endpoints sin protección de `/Account/*` (fuera del grupo
    `/Manage`, que ya exige auth), se marcaron `.AllowAnonymous()` los tres que
    se llaman sin sesión: `POST /PerformExternalLogin` (login externo, invocado
    desde `Login.razor`), `POST /Logout` (`SignInManager.SignOutAsync()` es
    idempotente, no debía empezar a exigir sesión previa) y
    `POST /PasskeyRequestOptions` (login con passkey, también desde
    `Login.razor`, soporta `username: null` para credenciales discoverable).
    `POST /PasskeyCreationOptions` se dejó SIN `.AllowAnonymous()` a propósito:
    solo lo usa `Manage/Passkeys.razor` ("Agregar una passkey nueva"), que ya
    está detrás del `[Authorize]` de carpeta de `Manage/_Imports.razor` — con la
    fallback policy ahora exige sesión también a nivel de endpoint, lo cual es
    más correcto que antes (antes, un POST anónimo directo devolvía 404 "Unable
    to load user" en vez de redirigir a login).
  - **Assets estáticos**: `app.MapStaticAssets()` no tiene metadata de
    autorización propia, así que sin exención quedaba alcanzado por la fallback
    policy también — hubiera roto el CSS/JS de la propia página de Login para
    usuarios sin sesión. Se le agregó `.AllowAnonymous()` explícito
    (`app.MapStaticAssets().AllowAnonymous()`), tal como documenta el patrón
    oficial de Blazor Web App + Identity para este mismo escenario.

  Verificado en el navegador con el servidor corriendo: cerrada la sesión desde
  el menú de usuario (ejercitando el nuevo `/Account/Logout` anónimo), la Home
  pública cargó bien estilada (confirma que `MapStaticAssets().AllowAnonymous()`
  no rompió el CSS). Desde ahí, `/Account/Login` cargó estilada e interactiva
  (el botón "Gestor"/"Solicitante" cambió de estado al hacer clic, confirmando
  que el circuito de Blazor Server se establece igual para usuarios anónimos).
  Con `fetch()` desde la consola: `POST /Account/PasskeyRequestOptions` devolvió
  `200` estando anónimo (login por passkey sigue funcionando), y
  `POST /Account/PasskeyCreationOptions` devolvió una redirección
  (`opaqueredirect`, equivalente a 302 a Login) estando anónimo — confirma que
  ahora sí exige sesión. Navegar directo a `/catalogo` sin sesión redirigió
  correctamente a `/Account/Login?ReturnUrl=%2Fcatalogo` (el gap real que
  motivó esta tarea, cerrado). Iniciando sesión como `gestor@catalogo.local`
  volvió automáticamente a `/catalogo` (el `ReturnUrl` funcionó) con el catálogo
  cargando normalmente; `/usuarios/roles` (tarea 4) y `/Account/Manage` (tarea 2)
  se probaron después de logueado y siguen funcionando sin cambios. No se vio
  ningún error nuevo en el log del servidor durante toda la prueba.
  Archivo(s): `Program.cs`, `IdentityComponentsEndpointRouteBuilderExtensions.cs`,
  `Pages/Home.razor`, `Pages/Error.razor`, `Pages/NotFound.razor`, y 17 páginas
  bajo `Account/Pages/*.razor` (no incluye `Account/Pages/Manage/*`, que ya
  estaba protegido por atributo de carpeta).

## P2 — UX y consistencia

- [x] **6. Revisar el texto de `RegisterConfirmation.razor` tras conectar el email real** — Hecho.
  Al revisarlo apareció un hallazgo previo a cualquier tema de texto: con
  `RequireConfirmedAccount = false` (decisión ya acordada, ver tarea 1), tanto
  `Register.razor` como `ExternalLogin.razor` solo redirigen a esta pantalla dentro de
  un `if (UserManager.Options.SignIn.RequireConfirmedAccount)` — condición que hoy
  nunca se cumple. En la práctica **esta pantalla es inalcanzable por el flujo normal
  de la app**: quien se registra queda logueado directo, sin pasar por acá. Sigue
  siendo accesible solo si alguien escribe la URL a mano
  (`/Account/RegisterConfirmation?email=...`), algo que hoy no tiene ningún link real
  en la app.

  Aun así se corrigió el texto, por dos razones: (1) sigue siendo una ruta pública
  real y con URL adivinable, y (2) si en algún momento se activa
  `RequireConfirmedAccount` (revirtiendo la decisión de la tarea 1), esta pantalla
  vuelve a ser parte del flujo real de registro. Estaba en inglés, sin traducir, y con
  markup plano (`<h1>`/`<p>`) en vez de los componentes `Card`/`Alert` que ya usan las
  pantallas hermanas (`ForgotPasswordConfirmation.razor`). Se tradujo y restyleó para
  quedar consistente, con un mensaje que es preciso sin importar el valor de
  `RequireConfirmedAccount` (se evitó afirmar "no hace falta confirmar para entrar",
  que sería falso justo en el único caso en que esta pantalla se muestra por el flujo
  normal): ahora dice qué se envió, cuánto puede tardar, que revise spam, y agrega un
  link para reenviarlo (`Account/ResendEmailConfirmation`) y otro para volver al
  login. El caso de error (email sin cuenta asociada) se tradujo también y se
  reescribió usando `Alert Variant="AlertVariant.Danger"` en vez del `StatusMessage`
  con clases `alert-danger` del scaffolding original, sin tocar la lógica (sigue
  devolviendo 404).

  Verificado en el navegador navegando directo a la URL con query string (ya que no
  hay ningún link real que lleve ahí): con un email existente (`gestor@catalogo.local`)
  se ve la tarjeta "Revisa tu correo" traducida y estilada correctamente; con un email
  sin cuenta (`noexiste@catalogo.local`) se ve la página "Not Found" de la app (404
  correcto, mismo comportamiento que antes de este cambio, no es una regresión).
  Archivo(s): `RegisterConfirmation.razor`

- [x] **7. Selector de rol en login vs. passkeys** — Hecho.
  El chequeo de "elegiste Gestor pero tu cuenta es Solicitante" (`Login.razor`) buscaba
  al usuario por `Input.Email` después de un sign-in exitoso — pero en un login por
  passkey con credencial descubrible (autofill, sin escribir nada en el campo Email;
  confirmado leyendo `PasskeySubmit.razor.js`: `tryAutofillPasskey()` dispara el login
  apenas el navegador soporta *conditional mediation*, antes de que el usuario toque
  el campo Email, y manda `username=""` a `/Account/PasskeyRequestOptions`) ese
  `Input.Email` llega vacío, `FindByEmailAsync("")` no encuentra a nadie, y el chequeo
  se saltaba en silencio. No era un bypass de autorización real (`[Authorize(Roles=...)]`
  en cada página seguía mandando), pero rompía la consistencia de la UX que el
  selector de rol prometía.

  Se resolvió (no se dejó como gap documentado): en vez de re-buscar al usuario por
  `Input.Email`, ahora se resuelve con `UserManager.GetUserAsync(HttpContext.User)`.
  Investigado por reflexión contra el ensamblado instalado
  (`Microsoft.AspNetCore.Identity.dll` 10.0.12, mismo método que se usó para la tarea
  13 — no hay decompilador disponible) que `SignInManager<TUser>` no expone un
  `PasskeySignInResult` con el usuario ya resuelto (`PasskeySignInAsync` devuelve un
  `SignInResult` plano, igual que `PasswordSignInAsync`); ambos métodos, sea cual sea
  el mecanismo de autenticación, terminan actualizando `HttpContext.User` como parte
  del sign-in — así que `HttpContext.User` refleja al usuario recién logueado sin
  depender de qué campo del formulario haya llegado lleno o vacío. Con este cambio,
  el mismo chequeo sirve para contraseña y para passkey por igual, sin ramas
  especiales por método de login.

  Verificado en el navegador (solo se pudo probar el camino de contraseña: una
  ceremonia real de passkey requiere un autenticador WebAuthn nativo que las
  herramientas de automatización no pueden simular sin abrir un diálogo del sistema
  operativo, y hoy no hay ninguna passkey registrada en la base de datos de
  desarrollo — confirmado con `SELECT COUNT(*) FROM AspNetUserPasskeys` = 0): logueé
  `gestor@catalogo.local` (cuenta Gestor) con "Solicitante" seleccionado y la pantalla
  mostró "Esta cuenta no tiene permisos de Solicitante." — igual que antes del
  cambio, confirmando que `HttpContext.User` ya refleja el sign-in dentro del mismo
  request (si no lo reflejara, `GetUserAsync` habría devuelto `null` y el chequeo se
  habría saltado también para contraseña, revelando una regresión). Repetí el login
  con "Gestor" seleccionado (coincide con el rol real) y entró normalmente, con el
  menú de Gestor visible. El camino de passkey no se pudo ejercitar de punta a punta,
  pero la corrección ya no depende de ningún dato específico del método de login —
  usa la misma fuente (`HttpContext.User`) que Identity ya actualiza para cualquier
  sign-in exitoso.
  Archivo(s): `Login.razor`

- [x] **8. Aislar en scope los usos "menores" de `UserManager` sin scope** — Hecho.
  `Administrar.razor`/`Bandeja.razor` (`DatosGestor`) y `Carrito.razor` (`Enviar()`)
  llamaban a `UserManager.GetUserAsync` con el `UserManager` inyectado directo (sin
  scope aislado), desde manejadores de evento (no `OnInitializedAsync`), así que la
  ventana de carrera con `UserMenu.razor` era mucho más chica que en la tarea 2. Se
  aplicó el mismo patrón por consistencia y defensa en profundidad, no por urgencia —
  ninguno de los tres había mostrado la excepción de concurrencia en la práctica.

  `Administrar.razor` y `Bandeja.razor` comparten el mismo helper `DatosGestor`, así
  que se le agregó `@inject IServiceScopeFactory ScopeFactory` a ambos archivos y el
  scope se abre DENTRO del helper (`await using var scope = ScopeFactory
  .CreateAsyncScope();`), no en cada uno de sus 3 call sites — como el `UserManager`
  inyectado directo quedó sin ningún otro uso en ninguno de los dos archivos, se le
  sacó el `@inject` a ambos. En `Carrito.razor`, `Enviar()` ya tenía `ScopeFactory`
  inyectado (lo usa `OnInitializedAsync` desde la tarea 2 anterior a este plan), así
  que solo hubo que envolver la única línea suelta (`UserManager.GetUserAsync(user)`)
  en su propio `await using (var scope = ScopeFactory.CreateAsyncScope())`, y sacar
  el `@inject UserManager<ApplicationUser> UserManager` que quedó sin uso.

  Verificado en el navegador de punta a punta, ejercitando los tres call sites reales
  (no solo compilación): logueado como `usuario@catalogo.local`, agregué un producto
  al catálogo y lo envié desde el carrito (`Carrito.razor.Enviar()`) — se creó el
  Pedido #63 en estado Pendiente. Logueado como `gestor@catalogo.local`, lo aprobé
  desde la Bandeja (`Bandeja.razor.DatosGestor` vía el botón Aprobar) — "Solicitud
  #130 aprobada." Después lo envié a "Distribuciones Andinas" desde Administrar
  pedidos (`Administrar.razor.DatosGestor` vía `ConfirmarEnvio`, con el correo real
  ya conectado desde la tarea 1) — "Pedido #63 enviado a Distribuciones Andinas (PDF
  adjunto, 1 producto(s))", con "Gestor de Catálogo" mostrado correctamente como
  quien lo envió (confirma que `DatosGestor` resolvió bien al usuario a través del
  scope aislado). Sin errores nuevos en el log del servidor durante toda la prueba.
  Archivo(s): `Administrar.razor`, `Bandeja.razor`, `Carrito.razor`

- [x] **9. Traducir y estilizar `AccessDenied.razor`** — Hecho.
  Era texto plano en inglés ("Access denied. You do not have access to this
  resource.") con una clase Tailwind que no sigue la convención del proyecto
  (`text-danger` en vez de `text-danger-700`, y sin ninguno de los componentes que
  usa el resto de la app). Se reescribió con `Card`/`Alert` (mismo patrón que
  `Lockout.razor`), en español: "Acceso denegado" / "No tenés permiso para acceder a
  este recurso."

  A diferencia de `Lockout.razor`/`InvalidUser.razor` (que ofrecen "Volver a iniciar
  sesión"), acá el link es "Volver al inicio" (`/`): esta página es el
  `AccessDeniedPath` por defecto de Identity, al que llega un usuario que YA tiene
  sesión iniciada pero le falta el rol que exige la página (`[Authorize(Roles=...)]`)
  — ofrecerle "iniciar sesión" no tendría sentido. Tampoco se le agregó
  `@layout AuthLayout` (a diferencia de esas dos páginas, que sí lo tienen): como el
  visitante está autenticado, conviene que mantenga el layout normal de la app
  (sidebar y header) en vez del layout centrado de pre-login — es el mismo criterio
  que ya usan por default el resto de páginas bajo `Components/Pages/*` (ninguna,
  salvo `NotFound.razor`, fija un `@layout` explícito).

  Verificado en el navegador: logueado como `usuario@catalogo.local` (rol
  Solicitante), navegué directo a `/solicitudes/bandeja` (`[Authorize(Roles =
  Roles.Gestor)]`) y la redirección automática de Identity llevó a
  `/Account/AccessDenied?ReturnUrl=%2Fsolicitudes%2Fbandeja` — se vio la tarjeta
  "Acceso denegado" traducida y estilada, con el sidebar/header de "Usuario de
  Prueba" todavía visible (confirma que se mantuvo el layout normal). El link
  "Volver al inicio" navegó correctamente a `/`. Sin errores nuevos en el log del
  servidor.
  Archivo(s): `AccessDenied.razor`

## P3 — Documentar como riesgo aceptado / bajo impacto

- [x] **10. Suavizar los mensajes de error de registro** — Hecho.
  `Register.razor` mostraba la descripción cruda de `IdentityError` (ej. "Username
  'gestor@catalogo.local' is already taken"), lo que permitía enumerar cuentas
  existentes probando emails en el formulario de registro. Impacto bajo para este
  proyecto (app interna), pero se decidió mitigarlo en vez de solo documentarlo.

  Se eligió el mensaje genérico acotado (no uno genérico para cualquier error de
  registro): si el `IdentityError.Code` es `"DuplicateUserName"` o `"DuplicateEmail"`
  (el email en esta app es también el username, así que un registro duplicado casi
  siempre dispara ambos códigos a la vez — de ahí el `.Distinct()`, para no repetir
  el mismo mensaje dos veces), se reemplaza por un mensaje genérico que no confirma
  la duplicación: "No pudimos completar el registro con estos datos. Si ya tenés una
  cuenta, iniciá sesión o recuperá tu contraseña." El resto de los `IdentityError`
  (contraseña débil, etc.) se siguen mostrando tal cual — no filtran nada sobre
  cuentas existentes, y ocultarlos sería peor UX (el usuario no sabría qué corregir).

  Verificado en el navegador: intenté registrar una cuenta nueva con el email de la
  cuenta semilla `gestor@catalogo.local` (ya existente) y la pantalla mostró
  únicamente el mensaje genérico — no reveló que ese email ya tiene cuenta más allá
  de la ambigüedad que el propio mensaje reconoce a propósito. No se pudo forzar en
  vivo un `IdentityError` distinto al de duplicado (la validación de contraseña
  mínima ya la bloquea el `DataAnnotationsValidator` del lado cliente antes de
  llegar al servidor, y no hay otras reglas de contraseña configuradas —
  `RequireNonAlphanumeric = false`), pero el cambio deja esa rama intacta (mismo
  `error.Description` que antes), así que no hay riesgo de regresión ahí. Sin errores
  nuevos en el log del servidor.
  Archivo(s): `Register.razor`

- [x] **11. Documentar la política de expiración de cookies/sesión** — Hecho.
  Tarea de documentación, sin cambios de código. Confirmado que no hay
  `ConfigureApplicationCookie` en ningún lado del proyecto (`grep` sin resultados) —
  la cookie de `IdentityConstants.ApplicationScheme` corre con los valores por
  defecto de `CookieAuthenticationOptions`, verificados por código (instanciando la
  clase directamente, ya que `AddIdentityCookies()` no los pisa): `ExpireTimeSpan` =
  14 días, `SlidingExpiration = true`, `Cookie.HttpOnly = true`,
  `Cookie.SecurePolicy = SameAsRequest`, `Cookie.SameSite = Lax`. Se deja anotado
  como decisión deliberada (quedan como están) en vez de default accidental: son
  razonables para esta app y no hay ningún requisito que pida algo distinto.

  **Cómo interactúa esto con "Recordarme" en `Login.razor`**: el checkbox controla
  `isPersistent` en `SignInManager.PasswordSignInAsync(...)`. Marcado, la cookie sale
  con expiración persistente (los 14 días deslizantes de arriba). Sin marcar, la
  cookie sale sin fecha de expiración explícita — es una cookie de sesión de
  navegador, se borra al cerrar el navegador entero (no la pestaña) —
  independientemente del `ExpireTimeSpan` configurado, que solo aplica a cookies
  persistentes. Es el comportamiento estándar de cookies de ASP.NET Core, no algo
  específico de este proyecto.

  **Control ya existente, para que no se reinvente**:
  `IdentityRevalidatingAuthenticationStateProvider` (`RevalidationInterval` =
  `TimeSpan.FromMinutes(30)`) revalida cada 30 min, en cualquier circuito de Blazor
  Server conectado, que el `SecurityStamp` del claim coincida con el de la base —
  si no coincide, el circuito pierde la sesión sin esperar a que la cookie expire.
  Mitiga sesiones activas después de un cambio de contraseña (`ChangePasswordAsync`/
  `ResetPasswordAsync` actualizan el `SecurityStamp` como parte de su
  implementación estándar en Identity).

  **Corrección al hallazgo inicial de este plan**: el análisis original (más arriba
  en este mismo punto, antes de esta verificación) asumía que este mecanismo también
  cubría un cambio de rol — no es así. Verificado directo contra la base de datos de
  desarrollo con un script descartable (mismo patrón que las tareas 4 y 13): tomé el
  `SecurityStamp` de `usuario@catalogo.local`, le agregué el rol Gestor con
  `UserManager.AddToRoleAsync` y volví a leerlo — sin cambios
  (`JLWVZ5MSJJISIIZJKDZWE6XWF5Y2KDH2` antes y después); se lo quité con
  `RemoveFromRoleAsync` y tampoco cambió. `AddToRoleAsync`/`RemoveFromRoleAsync` no
  tocan el `SecurityStamp` en Identity. Esto quiere decir que la revalidación de 30
  min **no** detecta un cambio de rol — el mensaje de "cerrá sesión y volvé a entrar"
  que ya muestra `GestionRoles.razor` tras un auto-cambio de rol (tarea 4) no es solo
  por la limitación de reescribir la cookie desde un click handler interactivo: aunque
  esa limitación no existiera, la sesión activa igual quedaría con el rol viejo
  indefinidamente (no en 30 min) hasta un logout/login real. Vale la pena tenerlo
  anotado acá para no asumir a futuro que el control de 30 min ya cubre esto.
  Archivo(s): ninguno (solo este documento) — referencia:
  `IdentityRevalidatingAuthenticationStateProvider.cs`, `Login.razor`

- [x] **12. Nota sobre las contraseñas de las cuentas semilla** — Hecho.
  La tarea original solo pedía una nota en el README, pero al revisar `Program.cs`
  apareció algo más serio que una nota: `Seed.EjecutarAsync(scope.ServiceProvider)`
  se llamaba sin ningún `if (app.Environment.IsDevelopment())` — corría en
  cualquier ambiente. Si esta app se desplegara alguna vez tal cual, además de
  aplicar las migraciones pendientes, habría creado automáticamente
  `gestor@catalogo.local` / `Gestor123!` (rol Gestor) y `usuario@catalogo.local` /
  `Usuario123!`, y sembrado 5 productos ficticios en el catálogo real — no un
  descuido de "contraseña débil para recordar cambiar", sino cuentas reales con
  credenciales públicas (están en este mismo repo) creándose solas. Se lo planteé al
  usuario en vez de decidir por mi cuenta si ampliaba el alcance de una tarea
  marcada "no es tarea de código", y confirmó agregar el gate ahora.

  `Seed.EjecutarAsync` ahora recibe un `bool esDesarrollo` — la creación de roles
  (`Roles.Todos`) se sigue ejecutando siempre (es infraestructura que la
  autorización necesita en cualquier ambiente, no datos de prueba), pero con
  `if (!esDesarrollo) return;` justo después, antes de tocar las cuentas semilla o
  los productos de ejemplo. `Program.cs` ahora llama
  `Seed.EjecutarAsync(scope.ServiceProvider, app.Environment.IsDevelopment())`.
  Además se agregó la nota original en el README, junto a la tabla de "Cuentas de
  prueba", explicando que ahora dependen de `IsDevelopment()` y que las contraseñas
  no deben reutilizarse en un ambiente real de todos modos.

  Verificado corriendo la app en ambos ambientes: en `Development` (el default de
  `dotnet run`) arrancó igual que siempre. Forzando `ASPNETCORE_ENVIRONMENT=Production`
  (con `--no-launch-profile` para que no lo pisara `launchSettings.json`, y la
  cadena de conexión pasada por variable de entorno ya que los `user-secrets` solo
  se cargan en `Development`) arrancó limpio contra la misma base de datos de
  desarrollo, sin ningún log de "sembrado"/"de ejemplo creado" (los roles ya
  existían de antes, así que tampoco loguearon nada — comportamiento esperado, es
  idempotente) y sin excepciones; Home y Login respondieron 200 normalmente,
  confirmando que el gate no rompe el arranque en ese ambiente.
  Archivo(s): `Seed.cs`, `Program.cs`, `README.md`

## Agregada después — severidad real P0

- [x] **13. Arreglar los passkeys — nunca funcionaron en este proyecto** — **Hecho.**
  Descubierto al verificar la tarea 2 (no tenía que ver con la concurrencia de
  `DbContext`, era un bug distinto): `Manage/Passkeys.razor` tiraba
  `InvalidOperationException: This operation is not permitted because the underlying
  'DbContext' does not include 'IdentityUserPasskey`1' in its model... 'IdentityOptions
  .Stores.SchemaVersion' is set to 'IdentitySchemaVersions.Version3' or higher.` en
  cualquier operación.
  La causa real resultó más simple de lo que el análisis inicial suponía: alcanzó con
  agregar `options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;` dentro de
  `AddIdentityCore` (`DependencyInjection.cs`) — no hizo falta tocar el constructor de
  `AppDbContext` como se había planteado (esa hipótesis, basada en versiones viejas de
  Identity, no aplicaba a la 10.0.12 instalada acá: confirmé contra la documentación XML
  del paquete que `IdentityDbContext<TUser>` no tiene ni necesita un overload con
  `IOptions<IdentityOptions>`; el `SchemaVersion` configurado en DI ya alcanza para que
  el modelo de EF incluya `IdentityUserPasskey`).
  Al generar la migración (`AgregarSoportePasskeys`) salió una advertencia real de
  posible pérdida de datos — el nuevo esquema Version3 acorta varias columnas
  (`AspNetUserTokens.Name`/`LoginProvider`, `AspNetUserLogins.ProviderKey`/
  `LoginProvider` de nvarchar(450) a nvarchar(128), y `AspNetUsers.PhoneNumber` de
  nvarchar(max) a nvarchar(256)). Antes de aplicarla verifiqué por SQL que las tablas
  afectadas no tenían datos reales en riesgo (`AspNetUserTokens`/`AspNetUserLogins`
  vacías, los 7 `PhoneNumber` existentes todos `NULL`) — sin eso no la habría aplicado
  sin más. Al aplicar la migración tal como la generó EF, SQL Server la rechazó
  (`ALTER TABLE ALTER COLUMN Name failed because one or more objects access this
  column.`) porque `Name`/`LoginProvider` son parte de la clave primaria compuesta de
  `AspNetUserTokens` (y lo mismo con `AspNetUserLogins`) — EF no genera automáticamente
  el `DROP`/`ADD` de esas constraints alrededor del `ALTER COLUMN`. La migración falló
  transaccionalmente sin dejar estado a medias (verificado: ni la migración quedó
  registrada, ni la tabla de passkeys se creó, ni las columnas cambiaron). Edité la
  migración a mano agregando `DropPrimaryKey`/`AddPrimaryKey` alrededor de los
  `AlterColumn` (mismas columnas y mismo orden que el modelo real, confirmados por SQL:
  `AspNetUserTokens` = `UserId, LoginProvider, Name`; `AspNetUserLogins` =
  `LoginProvider, ProviderKey`), tanto en `Up()` como en `Down()`.
  Verificado en el navegador: `Passkeys.razor` cargó "No passkeys are registered."
  (antes tiraba la excepción); `ExternalLogins.razor` (otra pantalla que toca
  `AspNetUserLogins`) siguió cargando bien; `EnableAuthenticator.razor` generó y
  **escribió de verdad** una clave de authenticator nueva en `AspNetUserTokens` (mostró
  el QR/clave sin error), confirmando que el acortamiento de columnas + la
  reconstrucción de las PK no rompió ni la lectura ni la escritura en ninguna de las dos
  tablas tocadas.
  Archivo(s): `DependencyInjection.cs`, migración
  `20260917135106_AgregarSoportePasskeys` (editada a mano)

- [x] **14. `AntiforgeryValidationException` sin manejar en los endpoints de passkey** —
  Hecho (2026-09-24).

  El usuario reportó un 500 sin manejar con el mensaje "The provided antiforgery token
  was meant for a different claims-based user than the current user", con el stack trace
  apuntando a `IdentityComponentsEndpointRouteBuilderExtensions.cs:85`.

  **Diagnóstico**: esa línea es `await antiforgery.ValidateRequestAsync(context)` dentro
  de `/Account/PasskeyRequestOptions` (`[AllowAnonymous]`, se llama desde `Login.razor`
  antes de tener sesión). El token de antiforgery de ASP.NET Core queda atado a la
  identidad (claims) del usuario que lo generó — si el navegador conserva una cookie de
  antiforgery de OTRA sesión/cuenta (muy probable en este proyecto, con tantas pruebas
  alternando `gestor@catalogo.local`/`usuario@catalogo.local` en el mismo navegador a lo
  largo de esta sesión), la validación choca aunque la petición sea legítima — no es un
  bug de lógica de negocio, es un token vencido/desalineado, y antes eso tiraba un 500 en
  vez de manejarse como tal. `/Account/PasskeyCreationOptions` (el mismo patrón, pero
  autenticado, usado al registrar un passkey nuevo desde `Passkeys.razor`) tenía el mismo
  problema potencial.

  **Arreglo**: se envolvió `antiforgery.ValidateRequestAsync` en ambos endpoints con un
  helper nuevo `TokenAntiforgeryValidoAsync` que captura
  `AntiforgeryValidationException` específicamente y responde `400 BadRequest` con un
  mensaje claro ("El formulario quedó desactualizado. Recargá la página e intentá de
  nuevo.") en vez de dejar que la excepción suba sin manejar. No se tocó la validación en
  sí (sigue exigiendo un token válido, no se bypasea el chequeo de seguridad) — solo se
  maneja su fallo legítimo de forma controlada.

  Verificado con `dotnet build` (0 errores/advertencias) y smoke test no interactivo de
  `/Account/Login` (sin excepciones en el log). **No se pudo reproducir el escenario
  exacto del bug en un smoke test** (depende de tener una cookie de antiforgery
  desalineada en el navegador real, algo que no se puede forzar por HTTP directo) — la
  corrección está verificada por lectura de código (mismo patrón try/catch ya usado en
  otros lugares de la app para convertir una excepción de terceros en una respuesta
  controlada) y queda pendiente de que el usuario confirme en su navegador que ya no
  vuelve a ver el 500. Ver memoria `feedback-no-browser-testing`.
  Archivo(s): `Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs`

- [x] **15. "Olvidé mi contraseña" no mandaba nada si el email no estaba confirmado** —
  Hecho (2026-09-24).

  El usuario reportó que el correo de recuperación de contraseña no le estaba llegando a
  un usuario.

  **Diagnóstico**: `ForgotPassword.razor` (scaffolding original de Identity, sin tocar
  hasta ahora) tenía `if (user is null || !(await UserManager.IsEmailConfirmedAsync(user)))`
  — si la cuenta existe pero su email nunca se confirmó, el sistema **no manda nada** y
  redirige igual a la pantalla "revisá tu correo" (a propósito, para no revelar si una
  cuenta existe — pero de paso oculta también que no se confirmó, sin ningún aviso).
  Confirmado contra la base real: `vennov34@gmail.com` (la cuenta que el usuario acababa
  de desbloquear en la tarea anterior de esta conversación) tiene `EmailConfirmed = 0`,
  igual que `puentesbenjamin17@gmail.com` — solo las cuentas semilla
  (`gestor@catalogo.local`/`usuario@catalogo.local`) vienen confirmadas de fábrica
  (`Seed.cs` las crea así a propósito). Cualquier cuenta registrada normalmente por un
  usuario real queda con `EmailConfirmed = 0` hasta que confirme por su cuenta — y como
  esta app tiene `RequireConfirmedAccount = false` (`DependencyInjection.cs`), nada del
  resto de la app obliga a hacerlo, así que en la práctica la mayoría de las cuentas
  reales quedan en ese estado indefinidamente sin que nadie note el problema hasta que
  intentan recuperar la contraseña.

  **Arreglo**: se sacó la condición `IsEmailConfirmedAsync` — solo queda el chequeo de
  que la cuenta exista (`user is null`), para no revelar existencia mediante la misma
  redirección genérica de siempre. Consistente con la política ya decidida
  (`RequireConfirmedAccount = false`): si confirmar el email no es obligatorio para usar
  el resto de la app, tampoco debería serlo para recuperar la contraseña.

  Verificado con `dotnet build` (0 errores/advertencias), `dotnet test` (17/17 sin
  regresiones) y smoke test no interactivo de `/Account/ForgotPassword` (sin excepciones
  en el log). **No se probó clic-por-clic en el navegador** (ver memoria
  `feedback-no-browser-testing`) — la infraestructura de envío (`IdentityEmailSender` →
  `SmtpEmailSender`) ya estaba verificada como funcional en
  `docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md`, tarea 9, así que el gap era puramente esta
  condición, no el envío en sí.
  Archivo(s): `Components/Account/Pages/ForgotPassword.razor`.

---

*Se trabaja de arriba hacia abajo, una tarea a la vez. Al terminar una, se marca `[x]` y
se resume qué cambió y cómo se verificó en el navegador (mismo formato que
`docs/PLAN_FLUJO_PEDIDO_PROVEEDOR.md`) antes de pasar a la siguiente.*
