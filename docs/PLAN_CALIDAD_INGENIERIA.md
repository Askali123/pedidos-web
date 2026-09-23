# Plan de mejoras — calidad e ingeniería

Checklist de trabajo, de mayor a menor prioridad, salida de una auditoría completa hecha
el 2026-09-19 sobre dos frentes: aspectos transversales de ingeniería (testing, seguridad,
performance, manejo de errores, observabilidad, background jobs) y un bug de negocio
crítico encontrado en el camino. Se cruzó contra `docs/MEJORAS_PROPUESTAS.md` (análisis
previo ya existente en el repo) para no repetir lo ya señalado ahí y marcar qué de eso
sigue abierto. El plan hermano `docs/PLAN_FUNCIONALIDADES_NEGOCIO.md` cubre las
funcionalidades de negocio nuevas — se decidió resolver primero este (la base) antes de
seguir agregando funcionalidad sobre algo sin red de seguridad. Cada tarea se marca `[x]`
al completarse; no reordenar sin avisar — el orden es la prioridad acordada.

## Decisiones de diseño ya acordadas

- **Orden de los dos planes**: primero calidad e ingeniería (esto — incluye el bug de
  stock, tratado como P0 porque es lo más urgente de arreglar antes de construir más
  funcionalidad encima), después `docs/PLAN_FUNCIONALIDADES_NEGOCIO.md`.
- **Auditoría de acciones administrativas (tarea 4)**: se implementa como logging
  estructurado (`ILogger`) con actor + acción + destino, no como una entidad/tabla de
  auditoría nueva — alcance mínimo viable, coherente con que hoy no existe ningún log de
  aplicación más allá del que emite EF Core por cada comando SQL.

## P0 — Bug de negocio crítico

- [x] **1. Reponer stock automáticamente al confirmar la entrega** — Hecho.
  `SolicitudService` descuenta `Producto.Stock` al aprobar una solicitud, pero ningún
  código lo reponía cuando un proveedor confirmaba la entrega —
  `ConfirmacionEntregaService.ConfirmarAsync` no tocaba `Stock` en ningún punto. El stock
  solo bajaba, nunca subía solo. Esto explicaba directamente por qué ~41 de 236 productos
  del catálogo ya tenían stock negativo (hallazgo repetido durante la sesión de UI/UX).

  Se agregó `GrupoEntregaDto.Productos` (reutilizando el mismo `ProductoDelPedidoDto` que
  ya usaba `ProveedorDelPedidoDto` para el selector "Enviar a proveedor" — mismo DTO,
  ningún tipo nuevo) para que cada grupo de entrega exponga no solo el conteo de líneas
  sino los productos y cantidades reales que cubre. `ConfirmacionEntregaService` ahora
  inyecta `IProductoRepository` y, justo después de crear la `ConfirmacionEntrega`, llama
  a `ReponerStockAsync(grupo.Productos, ct)` — incrementa `Producto.Stock` en la
  `Cantidad` de cada producto del grupo confirmado. Mismo patrón no transaccional que
  `SolicitudService.DescontarStockAsync` (su contraparte simétrica): `ObtenerPorIdAsync` +
  mutar + `ActualizarAsync`, sin try/catch propio (si el producto ya no existe, se lo
  salta; cualquier otra falla real propaga, igual que el descuento).

  Verificado en el navegador logueado como Gestor, con dos pedidos reales que ya estaban
  aprobados y pendientes de confirmar (no datos de prueba — se dejaron confirmados, ya
  que es trabajo legítimo, no hubo que revertir nada): Pedido #2 ("Mouse inalámbrico",
  cantidad 1) — stock pasó de 29 a 30; Pedido #9 ("Agua CRISTAL sin GAS...", cantidad 3,
  partiendo de stock negativo) — stock pasó de -9 a -6, confirmando que suma la cantidad
  exacta (no solo +1) y que funciona igual de bien partiendo de un valor negativo. Sin
  errores nuevos en el log del servidor (solo el falso positivo ya conocido de
  `HttpsRedirectionMiddleware`).
  Archivo(s): `GrupoEntregaDto.cs`, `ConfirmacionEntregaService.cs`

## P1 — Red de seguridad antes de seguir construyendo

- [ ] **2. Agregar un proyecto de tests automatizados (xUnit)**
  Cero tests en toda la solución hoy (confirmado: ningún `*Tests.csproj`). Crear
  `CatalogoPedidos.Application.Tests` y empezar por la lógica no trivial agregada en
  sesiones recientes, sin red de seguridad: `ExcelProductoImportador` (duplicados,
  upsert), `ConfirmacionEntregaService`/`PedidoNotificacionProveedorService` (cálculo de
  "grupos de entrega", exclusión de líneas ya enviadas a otro proveedor — tarea 2 de
  `docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md`), `SolicitudService.ResolverAsync`
  (transición de estados, no permitir resolver dos veces).

- [ ] **3. Manejo de errores en la UI — evitar que una excepción tumbe todo el circuito**
  Sin ningún `<ErrorBoundary>` en la app — reproducido en vivo en esta misma sesión (tarea
  4 del plan de proveedores/entregas): una `InvalidOperationException` de validación del
  servicio (que este codebase lanza a propósito en decenas de lugares) tira TODO el
  circuito de Blazor Server, mostrando la barra roja genérica "Ha ocurrido un error
  inesperado. Recargar" y perdiendo el estado de la página. El manejo hoy es inconsistente
  — `Proveedores.razor`'s `Guardar` ya atrapa `InvalidOperationException` y la muestra en
  un `Alert`, pero `ConfirmarEntregaSubmit` (Bandeja/Administrar) no. Agregar un
  `<ErrorBoundary>` como red de seguridad general, y revisar los `OnClick`/submit
  handlers que llaman a servicios que pueden lanzar `InvalidOperationException` sin
  try/catch propio, empezando por `ConfirmarEntregaSubmit`.

- [ ] **4. Auditoría de acciones administrativas**
  `GestionRoles.razor` (cambio de rol de un usuario) y `ProductoService`/`ProveedorService`
  (alta/edición/desactivación) no generan ningún registro de "quién hizo qué" — ni un
  `LogInformation`. Contrasta con Solicitudes/Pedidos, que sí dejan `GestorId`/
  `GestorNombre` snapshot en cada entidad resuelta. Agregar logging estructurado en esos
  puntos (ver decisión de diseño arriba).

- [ ] **5. Habilitar el bloqueo por intentos fallidos en el login**
  `Login.razor` llama a `SignInManager.PasswordSignInAsync(..., lockoutOnFailure: false)`,
  desactivando el bloqueo por fuerza bruta que Identity trae por defecto (5 intentos).
  Cambiar a `true` — ya señalado en `docs/MEJORAS_PROPUESTAS.md` #11, sigue sin resolver.

## P2 — Deuda técnica que ya empezó a notarse

- [ ] **6. Paginar en SQL en vez de en memoria**
  No es solo Bandeja/Administrar: `ProductoRepository.ObtenerCatalogoAsync` (sin
  `Skip`/`Take` en la query) alimenta `Catalogo.razor`, que pagina sobre la lista completa
  ya traída a memoria; mismo patrón en `SolicitudRepository.BuscarAsync`/
  `BuscarResueltasAsync` para Administrar/históricos. No duele con el volumen actual
  (~230 productos, cientos de solicitudes), pero es el mismo problema de fondo repetido en
  3-4 pantallas. Mover la paginación a la consulta SQL (recibir página/tamaño, devolver
  también el total).

- [ ] **7. Reducir las consultas N+1 en las pantallas de pedidos**
  Ya existían antes, pero las tareas 4/5/7 de `docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md`
  sumaron `ObtenerGruposDeEntregaAsync` (que internamente vuelve a llamar
  `ObtenerPedidoAsync` + `ObtenerPorProductosAsync` + `ObtenerPorPedidoAsync` de envíos y
  de confirmaciones) al mismo loop por pedido en Bandeja/Administrar/MisSolicitudes — cada
  pedido en pantalla dispara ahora ~6-8 round-trips a la base en vez de ~4. Acotado por
  paginación (10-20 pedidos/página) así que no es urgente, pero es deuda que se acaba de
  agrandar. Evaluar batchear estas consultas por página en vez de por pedido.

- [ ] **8. Rate limiting HTTP y reintentos ante fallos transitorios de SQL**
  Sin `AddRateLimiter` en todo el proyecto — el único límite de tasa hoy es el cooldown de
  5 min para reenvío a proveedor. Endpoints como `/Account/ForgotPassword` (dispara un
  correo real) o los de exportación PDF/Excel no tienen ningún límite. Agregar una
  política de rate limiting básica para esos endpoints. De paso, `UseSqlServer(...)` no
  tiene `EnableRetryOnFailure` — agregarlo (una línea, ya señalado en
  `docs/MEJORAS_PROPUESTAS.md` #7).

- [ ] **9. Health check y logging estructurado mínimo**
  Sin `/health` que verifique la conexión a SQL Server, y sin logging de eventos de
  negocio ("solicitud creada", "importación completada con N errores") más allá de lo que
  emite EF Core por cada comando SQL. Agregar `AddHealthChecks`/`MapHealthChecks` con un
  chequeo de DB, y loggear los eventos de negocio más relevantes con `ILogger` (no hace
  falta una librería nueva como Serilog para esto, es proporcional usar lo que ya trae
  ASP.NET Core).

- [ ] **10. Traducir y restilizar `Error.razor`**
  Es el scaffolding original sin tocar — en inglés, sin estilo, con un texto interno
  ("The Development environment shouldn't be enabled...") visible al usuario. Es la
  página que se muestra en Producción para cualquier excepción no manejada a nivel HTTP
  (`app.UseExceptionHandler("/Error")`). Quedó fuera del plan de UI/UX ya cerrado —
  aplicar el mismo Design System (Card/Alert) y traducir.

## P3 — Nice-to-have

- [ ] **11. Agregar security headers básicos**
  Sin CSP, `X-Content-Type-Options` ni `Referrer-Policy` configurados en `Program.cs`.

- [ ] **12. Agregar índices explícitos en columnas de filtro frecuente**
  `AppDbContext` solo define 4 índices explícitos hoy. `Solicitudes.Estado`,
  `Solicitudes.SolicitanteId` y `Productos.Categoria`/`Activo` se filtran constantemente
  sin índice — no es un problema activo con el volumen actual, pero es barato agregarlo.

- [ ] **13. Definir cómo se asigna el rol "Gestor"**
  Hoy no hay ningún mecanismo para que alguien se registre como Gestor, ni para que un
  Gestor promueva a otro usuario después de que ya existe (fuera de editar la base a
  mano). Ya señalado en `docs/MEJORAS_PROPUESTAS.md` #11 — definir la regla antes de que
  haga falta en la práctica.

- [ ] **14. Aclarar que la confirmación de email no envía nada real**
  `IdentityNoOpEmailSender` es un no-op. Como `RequireConfirmedAccount = false` esto no
  bloquea el login, pero el mensaje que ve el usuario tras registrarse ("revisa tu
  correo") es engañoso — aclarar explícitamente que está desactivado en este entorno de
  práctica. Ya señalado en `docs/MEJORAS_PROPUESTAS.md` #11.
