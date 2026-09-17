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

- [ ] **6. Revisar el texto de `RegisterConfirmation.razor` tras conectar el email real**
  Hoy le dice al usuario que revise su correo aunque nunca llegue nada (tarea 1 lo
  destraba); una vez el envío sea real, confirmar que el mensaje siga siendo preciso
  (qué esperar, cuánto puede tardar, qué hacer si no llega).

- [ ] **7. Selector de rol en login vs. passkeys**
  El chequeo de "elegiste Gestor pero tu cuenta es Solicitante" (`Login.razor`) busca
  al usuario por `Input.Email` después de un sign-in exitoso — pero en un login por
  passkey/credencial descubrible `Input.Email` puede venir vacío (es la naturaleza de
  passkeys), así que ese chequeo se salta silenciosamente. No es un bypass de
  autorización real (`[Authorize(Roles=...)]` en cada página sigue mandando), pero
  rompe la consistencia de la UX que el selector de rol prometía. Decidir: aceptar el
  gap documentado, o resolver el usuario por el id de la credencial en vez del email
  para ese caso.

- [ ] **8. Aislar en scope los usos "menores" de `UserManager` sin scope**
  `Administrar.razor`/`Bandeja.razor` (`DatosGestor`) y `Carrito.razor` (`Enviar()`)
  llaman a `UserManager.GetUserAsync` sin scope aislado, pero desde manejadores de
  evento (no `OnInitializedAsync`), así que la ventana de carrera con
  `UserMenu.razor` es mucho más chica que en la tarea 2. Aplicar el mismo patrón por
  consistencia y defensa en profundidad, no por urgencia.

- [ ] **9. Traducir y estilizar `AccessDenied.razor`**
  Hoy es texto plano en inglés ("Access denied. You do not have access to this
  resource.") con una clase Tailwind que no sigue la convención del proyecto
  (`text-danger` en vez de `text-danger-700`). Alinear con el resto de la app:
  español, componentes existentes (`EmptyState`/`Alert` si aplica).

## P3 — Documentar como riesgo aceptado / bajo impacto

- [ ] **10. Suavizar los mensajes de error de registro**
  `Register.razor` muestra la descripción cruda de `IdentityError` (ej. "Username
  already taken"), lo que permite enumerar cuentas existentes probando emails.
  Impacto bajo para este proyecto — decidir si vale la pena un mensaje genérico o
  simplemente dejarlo documentado como riesgo aceptado.

- [ ] **11. Documentar la política de expiración de cookies/sesión**
  No hay `ConfigureApplicationCookie` en ningún lado — la app corre con los valores
  por defecto de Identity (14 días de expiración deslizante si "Recordarme" está
  marcado). No es necesariamente incorrecto, pero conviene dejarlo escrito como
  decisión deliberada en vez de un default accidental. De paso, documentar que
  `IdentityRevalidatingAuthenticationStateProvider` ya revalida el security stamp
  cada 30 min en circuitos conectados — es un control real ya existente que mitiga
  sesiones viejas tras un cambio de contraseña o de rol, vale la pena que quede
  anotado para que no se reinvente.

- [ ] **12. Nota sobre las contraseñas de las cuentas semilla**
  `Gestor123!`/`Usuario123!` (`Seed.cs`) están bien para practicar, pero si este
  proyecto alguna vez apunta a un despliegue real hay que recordarlo explícitamente
  en el README o similar — no es una tarea de código, es una nota para no
  olvidarla.

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

---

*Se trabaja de arriba hacia abajo, una tarea a la vez. Al terminar una, se marca `[x]` y
se resume qué cambió y cómo se verificó en el navegador (mismo formato que
`docs/PLAN_FLUJO_PEDIDO_PROVEEDOR.md`) antes de pasar a la siguiente.*
