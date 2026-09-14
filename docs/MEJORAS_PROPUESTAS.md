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
- **Recordatorio de solicitudes pendientes.** Hoy la Bandeja solo resalta visualmente las
  solicitudes con más de 48h sin resolver. Podría convertirse en una notificación activa
  (ej. un job periódico o un chequeo al cargar la Bandeja) en vez de depender de que el
  gestor entre a mirar.
- **Editar/desactivar proveedores.** Hoy `IProveedorService` solo permite crear y listar.
  Falta `ActualizarAsync`/`DesactivarAsync`, análogo a como `Producto.Activo` ya existe.

## 2. Importación desde Excel (`ExcelProductoImportador`)

- **Vista previa antes de confirmar.** Ahora mismo se sube el archivo y se importa de
  inmediato. Sería más seguro (dado que reimportar actualiza productos existentes) mostrar
  primero una tabla de "esto se va a crear / esto se va a actualizar" y pedir confirmación.
- **Detectar códigos duplicados dentro del mismo archivo.** Si el proveedor repite un
  `CODIGO` en dos filas del Excel, la segunda pisa silenciosamente lo que dejó la primera
  (mismo comportamiento de upsert que para reimportaciones legítimas). Convendría
  detectarlo y reportarlo como advertencia en `ImportarProductosResultado.Errores`.
- **Capturar precio del proveedor.** El modelo `ProductoProveedor.PrecioProveedor` ya
  existe pero el importador no lo llena. Se podría aceptar una columna `PRECIO` opcional
  en el Excel.
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

---

*Última actualización: reflejando el estado del proyecto tras agregar importación desde
Excel con código de proveedor, alta manual con proveedor+código, y el flujo de carrito
multi-producto para solicitudes.*
