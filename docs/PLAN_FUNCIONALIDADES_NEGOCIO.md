# Plan de mejoras — funcionalidades de negocio

Checklist de trabajo, de mayor a menor prioridad, salida de la misma auditoría completa
del 2026-09-19 que dio origen a `docs/PLAN_CALIDAD_INGENIERIA.md`. Ese plan hermano cubre
la base (bug de stock, tests, manejo de errores, seguridad) — se decidió resolverlo
primero, y retomar este recién después. Los ítems de acá son funcionalidades nuevas o
huecos de negocio reales, fundamentados en datos y código que ya existen en el proyecto
(no ideas genéricas). Cada tarea se marca `[x]` al completarse; no reordenar sin avisar —
el orden es la prioridad acordada.

## Decisiones de diseño ya acordadas

- **Este plan arranca después de `docs/PLAN_CALIDAD_INGENIERIA.md`** — en particular
  después de su tarea 1 (reponer stock al confirmar entrega), porque varias tareas de acá
  (reportes de gasto, lead time por proveedor) asumen que los datos de stock/entrega ya
  son confiables.

## P1 — Mayor valor, ya hay datos para construirlo

- [ ] **1. Reporte de gasto por proveedor, categoría y período**
  `IPdfExportService`/`IExcelExportService` hoy solo generan listados (catálogo,
  solicitudes, pedido individual) — cero reportes agregados. Agregar una pantalla nueva
  de "Reportes" que agrupe `SolicitudProducto` (Aprobadas) por `Categoria` y por
  `Proveedor` en un rango de fechas (usando `Precio`/`PrecioProveedor` × `Cantidad`), con
  exportación a Excel — con datos que ya existen pero que hoy solo se ven fila por fila.

- [ ] **2. Tope de gasto mensual / segunda aprobación al superarse** (antes "Centro de
  costo / presupuesto por pedido")
  **Revisado el 2026-09-23** — la mitad de esta tarea (el campo de agrupación
  organizativa: `CentroCosto`/`Departamento`) quedó **superada por `Empresa`/`Sede`**
  (ver `docs/PLAN_EMPRESAS_FILIALES.md`): ya no hace falta agregar ese campo aparte —
  `Sede` cubre la misma necesidad de agrupar solicitudes por unidad organizativa, y además
  trae geolocalización propia y jerarquía real con el holding Auropaq. Ese plan ya
  implementó el modelo, la administración (crear/editar/desactivar) y un dashboard de
  consumo filtrable por Empresa/Sede/período.

  Lo que sigue **sin tocar** de esta tarea es la idea original del umbral: hoy no hay
  ninguna noción de presupuesto en el dominio — cualquier Gestor puede aprobar cualquier
  categoría y cualquier monto sin ningún tope. Evaluar un tope de gasto mensual
  configurable (por Empresa/Sede o global) que dispare una alerta o exija una segunda
  aprobación al superarse — eso queda pendiente, no forma parte del plan de Empresas.

- [ ] **3. Lead time real por proveedor**
  Ahora que `NotificacionProveedor.FechaEnvio` y `ConfirmacionEntrega.FechaEntrega`
  conviven (post `docs/PLAN_MEJORAS_PROVEEDORES_ENTREGAS.md`), hay datos crudos para
  calcular el tiempo de entrega real por proveedor — pero nada lo agrega ni lo muestra.
  Calcular y mostrar en la ficha de cada proveedor (`Proveedores.razor` o una página de
  detalle) el lead time promedio observado, por grupo de entrega confirmado — dato
  objetivo para decidir a cuál proveedor mandarle un pedido urgente, en vez de elegir a
  ciegas.

- [ ] **4. Normalizar `Categoria` a una entidad propia**
  `Producto.Categoria` es un `string` libre — `ProductoService.ObtenerCategoriasAsync`
  deriva valores distintos de ese texto, lo que permite duplicados por typo ("Aseo" vs
  "aseo"). Reemplazarlo por una entidad `Categoria` con gestión propia (crear/renombrar/
  fusionar) — además de limpiar el catálogo, es un prerrequisito para que el reporte de
  gasto por categoría (tarea 1) agregue sobre datos limpios.

## P2 — Huecos reales, menor urgencia

- [ ] **5. Historial de precios**
  No existe ninguna entidad que registre cambios de `Precio`/`PrecioProveedor` a lo largo
  del tiempo — cada edición pisa el valor anterior sin dejar rastro, así que no se puede
  ver tendencia ni comparar cuánto subió un insumo. Agregar una entidad de snapshot
  (`HistorialPrecio`) que registre el valor anterior cada vez que cambia.

- [ ] **6. Código/SKU interno propio del producto**
  El catálogo no tiene ningún identificador propio de la empresa — solo el `Producto.Id`
  autogenerado y el `CodigoProveedor` de cada asociación (que es del proveedor, no
  nuestro). Con cientos de productos, un SKU propio (opcional, editable) facilita
  operarlo a escala.

- [ ] **7. Múltiples contactos por proveedor**
  `Proveedor` tiene un solo `Contacto`/`Telefono`/`Email` como strings sueltos — sin
  soporte para distinguir ventas, despachos y facturación. Agregar una entidad
  `ContactoProveedor` (nombre, rol, teléfono, email) 1:muchos con `Proveedor`.

- [ ] **8. Preferencias de notificación por usuario**
  `ApplicationUser` no tiene ningún campo de preferencias — no se puede silenciar un tipo
  de aviso (ej. "no quiero recordatorios de stock bajo"). Agregar una pantalla simple de
  preferencias (qué `TipoNotificacion` recibir) en "Mi cuenta".

## P3 — Nice-to-have

- [ ] **9. Resumen de notificaciones por email (digest)**
  Hoy todas las notificaciones son 100% in-app/tiempo real — ningún resumen por correo.
  Evaluar un digest diario/semanal opcional para quien no revisa la app seguido.

- [ ] **10. Exportar catálogo completo y pedido individual a Excel**
  Asimetría hoy: PDF tiene 6 métodos de exportación (incluye catálogo completo y pedido
  individual), Excel solo 2 (solicitudes). Parejar las opciones.

- [ ] **11. Imagen de producto**
  El catálogo es 100% texto — sin foto por producto. Agregar un campo opcional de imagen
  (URL o upload) a `Producto`.
