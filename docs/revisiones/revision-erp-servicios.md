## Resumen

Los servicios presentan una base sólida de autorización, consultas asíncronas, transacciones explícitas e idempotencia en operaciones críticas. Se encontraron oportunidades importantes de mantenibilidad y observabilidad, principalmente en manejo de excepciones, límites codificados sin nombre y composición de operaciones DTE.

No se identificaron hallazgos Críticos. Se aplicaron únicamente refactors seguros dentro de `Services/**`; no se modificaron Views, Helpers, seguridad, contratos públicos, esquema EF ni migraciones.

## Hallazgos

### Importante — Respuestas externas completas en logs

- Ubicación: `Ferreteria.PuntoVenta/Services/Dte/MhApiClient.cs:96` y `Ferreteria.PuntoVenta/Services/Dte/FirmadorHttpClient.cs:99`
- Problema: se registraba el cuerpo completo de respuestas HTTP del Ministerio de Hacienda y del Firmador. Esas respuestas pueden incluir datos del documento, mensajes internos o información sensible; además, el log no necesita el cuerpo para diagnosticar el estado HTTP.
- Propuesta: registrar únicamente el código de estado y conservar el mensaje controlado que se muestra al usuario. APLICADO.

### Importante — Capturas vacías durante la resolución de zona horaria

- Ubicación: `Ferreteria.PuntoVenta/Services/Time/BusinessTimeZone.cs:78-122`
- Problema: varios `catch` vacíos ocultaban que se estaba probando una representación alternativa de la zona horaria. Aunque el fallback era intencional, el flujo era difícil de entender y podía confundirse con un error tragado.
- Propuesta: encapsular la prueba en `TryFindSystemZone`, devolver `null` solo para las dos excepciones esperadas y explicar el motivo en comentarios. APLICADO; se conserva el mismo orden de fallback.

### Importante — Valores ESC/POS sin nombre

- Ubicación: `Ferreteria.PuntoVenta/Services/Printing/EscPosDocumentBuilder.cs:10-226` y `EscPosCommands.cs:8-118`
- Problema: los valores de columnas, tabla de caracteres, avance antes del corte, tamaño predeterminado del QR y límites del protocolo aparecían como literales (`16`, `80`, `255`, `1` y `16`). Un desarrollador junior podía modificarlos sin conocer su significado físico o de protocolo.
- Propuesta: extraer constantes con nombres descriptivos y usarlas en los mismos cálculos, sin cambiar bytes ni resultados. APLICADO.

### Importante — Parámetro privado sin uso y clase muerta

- Ubicación: parámetro `dbContext` de `OrderService.AddSaleLineAsync` y `Ferreteria.PuntoVenta/Services/ConfigService.cs`
- Problema: el parámetro no participaba en ninguna operación y la clase `ConfigService` era un marcador vacío sin referencias ni registro de DI. Ambos elementos agregaban superficie mental sin comportamiento.
- Propuesta: quitar el parámetro privado y eliminar la clase sin referencias. APLICADO.

### Importante — Auditoría que falla sin informar al llamador

- Ubicación: `Ferreteria.PuntoVenta/Services/AuditService.cs:68-80` y `92-119`
- Problema: `RecordChangeAsync` y los eventos de sesión capturan cualquier excepción, la registran y completan normalmente. Una operación de negocio puede parecer exitosa aunque su auditoría no se haya persistido.
- Propuesta: definir por operación si la auditoría es obligatoria y, cuando lo sea, propagar un error controlado o compartir la transacción de negocio. DIFERIDO: cambiar este comportamiento puede bloquear inicios de sesión, CRUD o impresiones y requiere acordar la política de consistencia y pruebas de recuperación.

### Importante — Fallos de conectividad sin telemetría

- Ubicación: `Ferreteria.PuntoVenta/Services/ConnectivityService.cs:25-33`
- Problema: la excepción se convierte correctamente en un estado de desconexión para la UI, pero se descarta sin logger. Los fallos repetidos de base de datos no dejan evidencia técnica.
- Propuesta: agregar logger estructurado y registrar solo el tipo de fallo y el contexto técnico, manteniendo el mensaje seguro para el usuario. DIFERIDO: requiere modificar la dependencia del constructor y cubrir el comportamiento de health check.

### Importante — Reproceso DTE que oculta fallos parciales

- Ubicación: `Ferreteria.PuntoVenta/Services/Dte/DteService.cs:368-398`
- Problema: el lote captura cualquier excepción por documento, registra el error y devuelve solo la cantidad procesada. El llamador no puede distinguir un lote completamente exitoso de uno con documentos fallidos.
- Propuesta: mantener el procesamiento del resto del lote, pero devolver un resultado con procesados/fallidos o lanzar un error agregado al terminar. DIFERIDO: cambiar el contrato de `Task<int>` requiere actualizar consumidores y pruebas de contingencia.

### Importante — Métodos DTE extensos con varias responsabilidades

- Ubicación: `Ferreteria.PuntoVenta/Services/Dte/DteService.cs:100-200` y `203-339`
- Problema: `EmitForOrderAsync` y especialmente `EmitCreditNoteAsync` mezclan autorización, lecturas, construcción del documento, transacción, persistencia, restauración de inventario y transmisión. El flujo es difícil de recorrer y modificar con seguridad.
- Propuesta: extraer métodos privados descriptivos para cargar/validar datos, persistir el DTE y transmitirlo, conservando los límites transaccionales actuales. DIFERIDO: requiere pruebas de idempotencia, rollback, contingencia y nota de crédito antes de separar el flujo.

### Importante — Validación de identificadores incompleta al actualizar clientes

- Ubicación: `Ferreteria.PuntoVenta/Services/CustomerService.cs:118-147`
- Problema: al actualizar se comprueba la duplicidad del NIT, pero no se realizan las mismas comprobaciones para DUI y NRC que sí se normalizan y persisten. El resultado depende de restricciones externas o puede permitir datos duplicados si no existe una restricción equivalente.
- Propuesta: validar DUI y NRC excluyendo el registro actual y agregar pruebas de actualización duplicada. DIFERIDO: es una corrección funcional, no un refactor puramente seguro, y debe verificarse contra las restricciones existentes sin modificar el esquema.

### Importante — Reloj del sistema usado junto con relojes inyectados

- Ubicación: `CustomerService.cs`, `SupplierService.cs`, `InventoryService.cs`, `DteService.cs`, `PrinterConfigService.cs` y `AuditService.cs`
- Problema: varios servicios usan `DateTime.UtcNow` directamente mientras otros reciben `TimeProvider` o `BusinessCalendar`. Esto hace que pruebas y operaciones que dependen de límites temporales sean menos deterministas.
- Propuesta: unificar gradualmente el origen del tiempo por servicio y cubrirlo con relojes controlados. DIFERIDO: requiere cambiar constructores/registro de dependencias y revisar timestamps usados por auditoría, DTE e inventario.

### Crítico — No identificado

- No se observó una condición dentro del alcance que justificara clasificarla como Crítica.

## Lo que está bien

- Las operaciones de ventas, órdenes, caja y devoluciones usan transacciones explícitas y validan estados antes de persistir.
- Hay autorización por permiso y revalidación contra la base de datos en los flujos sensibles.
- Las ventas y devoluciones tienen controles de idempotencia y manejo explícito de conflictos transitorios.
- Las consultas de lectura usan `AsNoTracking`, cancelación y límites de resultados en los puntos principales.
- El código usa logger estructurado y no se encontraron `Console`, `Debug`, `Trace` ni impresiones de depuración en los servicios revisados.
- Las reglas DTE, inventario, caja, devoluciones y filtros están separadas en tipos auxiliares con nombres relativamente claros.
- Los cambios aplicados mantienen las firmas públicas, el esquema EF, las migraciones y los contratos de respuesta.

## Siguientes pasos

- Acordar la política de consistencia de auditoría: auditoría obligatoria con rollback o telemetría best-effort explícita.
- Definir el contrato del reproceso DTE para reportar fallos parciales sin detener los demás documentos.
- Extraer el flujo DTE después de añadir pruebas de idempotencia, rollback y contingencia.
- Completar las validaciones de DUI/NRC de clientes con pruebas y sin modificar el esquema.
- Planificar la unificación de relojes inyectados en una iteración separada.
- Ejecutar restauración de paquetes, compilación Release y pruebas no integradas en un entorno con los artefactos de NuGet disponibles.

## Estado de aplicación

| Hallazgo | Estado | Motivo |
|---|---|---|
| Respuestas externas completas en logs | APLICADO | Se registran solo códigos de estado; se evita exponer cuerpos de respuesta. |
| Capturas vacías de zona horaria | APLICADO | Se extrajo `TryFindSystemZone` con excepciones esperadas y comentarios de fallback. |
| Valores ESC/POS sin nombre | APLICADO | Se agregaron constantes privadas sin alterar los valores emitidos. |
| Parámetro privado sin uso y clase muerta | APLICADO | Se quitó el parámetro y se eliminó `ConfigService.cs`, que no tenía referencias. |
| Auditoría que falla sin informar | DIFERIDO | Requiere decidir la política de consistencia y puede cambiar el resultado de operaciones existentes. |
| Fallos de conectividad sin telemetría | DIFERIDO | Requiere una nueva dependencia de logger y pruebas del contrato del servicio. |
| Reproceso DTE con fallos parciales | DIFERIDO | Requiere cambiar el contrato de resultado o la política de propagación. |
| Métodos DTE extensos | DIFERIDO | La extracción toca límites transaccionales y necesita pruebas de recuperación/idempotencia. |
| Validación incompleta de DUI/NRC | DIFERIDO | Es un cambio funcional que debe verificarse contra las restricciones actuales. |
| Relojes del sistema mezclados | DIFERIDO | Requiere una modificación transversal de dependencias y registro de servicios. |
