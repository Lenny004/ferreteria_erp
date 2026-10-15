# Revisión junior de vistas principales de Caja

## Resumen

Las vistas principales mantienen un flujo comprensible y respetan los atajos y confirmaciones del POS. El riesgo principal estaba en errores de búsqueda/selección que se ocultaban sin telemetría y en límites de entrada duplicados en Facturación; se aplicaron refactors locales y seguros para corregirlo.

No se encontraron hallazgos críticos. El cierre de caja conserva el contrato solicitado: F9 es el único atajo que ejecuta el cierre y Escape no cierra el turno.

## Hallazgos

### Importante — Dependencias de Facturación sin validación fail-fast

- Ubicación: `Ferreteria.PuntoVenta/Views/Caja/FacturacionView.xaml.cs:59-78`, constructor de `FacturacionView`.
- Problema: la mayoría de los servicios se asignaban sin validar `null`; un registro incompleto del contenedor podía producir un `NullReferenceException` lejos del origen.
- Propuesta: validar cada dependencia con `ArgumentNullException` al inicio del constructor y validar también `IOptions<PrintingOptions>`.
- Estado: aplicado. Es un cambio local de contrato defensivo y no altera el flujo normal.

### Importante — Cancelación asíncrona tratada como excepción silenciosa

- Ubicación: `Ferreteria.PuntoVenta/Views/Caja/FacturacionView.xaml.cs:120-122` y `174-176`, `SearchProductsAsync` y `SearchCustomersAsync`.
- Problema: los `catch (OperationCanceledException)` aceptaban cualquier cancelación y tenían cuerpo vacío. Una cancelación no asociada al coordinador podía ocultar un fallo real.
- Propuesta: filtrar el `catch` con `cancellationToken.IsCancellationRequested` y salir explícitamente; las demás excepciones continúan hacia el logger y el mensaje operativo.
- Estado: aplicado. La cancelación normal sigue sin mostrar un error al cajero.

### Importante — Límites y precisión duplicados como valores mágicos

- Ubicación: `Ferreteria.PuntoVenta/Views/Caja/FacturacionView.xaml.cs:24`, `116`, `170` y `290-298`; `Ferreteria.PuntoVenta/Views/Caja/DevolucionesView.xaml.cs:293-297`.
- Problema: el límite de resultados `25`, la precisión `12`, la escala `3` y el redondeo de cantidades estaban escritos en el code-behind, mientras que parte de la UI ya usa `FieldConstraints`. Esto permite que XAML, código y reglas compartidas diverjan.
- Propuesta: nombrar el límite de resultados y reutilizar `FieldConstraints.OrderDetailQuantityPrecision` y `OrderDetailQuantityScale` tanto al parsear como al redondear cantidades.
- Estado: aplicado. No cambia los valores efectivos actuales; elimina duplicación y hace explícita su intención.

### Importante — Excepciones de Devoluciones sin registro técnico

- Ubicación: `Ferreteria.PuntoVenta/Views/Caja/DevolucionesView.xaml.cs:95-99` y `127-132`, `OnSearchClick` y `OnSaleSelected`.
- Problema: ambos bloques capturaban `Exception` sin conservar la excepción. El usuario recibía un mensaje, pero soporte no podía correlacionar el fallo ni conocer el pedido afectado.
- Propuesta: capturar la excepción en una variable y registrarla con el logger oficial, incluyendo el `OrderId` al cargar líneas, sin exponer detalles técnicos en la UI.
- Estado: aplicado. Se conserva el mensaje seguro para el cajero y se agrega telemetría estructurada.

### Importante — Alta de cliente traga errores inesperados

- Ubicación: `Ferreteria.PuntoVenta/Views/Caja/CustomerCreateDialog.xaml.cs:89-96`, `OnConfirmClick`.
- Problema: el `catch (Exception)` convierte cualquier fallo técnico en un mensaje genérico sin logger. La operación puede fallar sin evidencia suficiente para diagnosticarla.
- Propuesta: inyectar un `ILogger<CustomerCreateDialog>` y registrar la excepción, manteniendo el mensaje genérico; idealmente cubrir también el comportamiento con una prueba del diálogo o del adaptador que lo invoque.
- Estado: diferido. Aplicarlo de forma segura requiere cambiar el constructor que Facturación instancia manualmente y coordinar su resolución; re-lanzar la excepción desde un `async void` podría cerrar inesperadamente la aplicación. No se introdujo una abstracción ni un cambio de contrato sin pruebas de UI.

### Menor — Búsqueda de devoluciones sin límite visual explícito

- Ubicación: `Ferreteria.PuntoVenta/Views/Caja/DevolucionesView.xaml:41`, `SearchTextBox`.
- Problema: el cuadro no define `MaxLength`, a diferencia de otros filtros. La validación termina dependiendo del servicio y se permite una entrada más grande de la necesaria.
- Propuesta: definir una restricción compartida específica para el criterio de devoluciones y enlazarla desde XAML, evitando reutilizar una constante de otra pantalla por conveniencia.

### Menor — Longitud del PIN repetida en diálogo y código

- Ubicación: `Ferreteria.PuntoVenta/Views/Caja/ReturnAuthorizationDialog.xaml:12` y `.xaml.cs:21-24`.
- Problema: el valor `4` aparece en `MaxLength`, en la validación y en el mensaje. Un cambio futuro podría actualizar solo una de las tres ubicaciones.
- Propuesta: centralizar la longitud en una restricción común usada por la captura y la validación. La implementación debe coordinarse con la pantalla PIN, que está fuera del alcance de esta revisión.

### Menor — Dimensiones de filas construidas con literales

- Ubicación: `Ferreteria.PuntoVenta/Views/Caja/DevolucionesView.xaml.cs:218-221`, `RenderLines`.
- Problema: los anchos `150`, `90`, `90`, `90` expresan decisiones de presentación sin nombres, lo que dificulta ajustar el diseño.
- Propuesta: extraer constantes privadas con nombres de intención o mover el layout a un recurso XAML cuando se haga una iteración visual. No se aplicó porque no afecta la lógica y requiere validar tamaños en pantalla.

## Lo que está bien

- `CorteCajaView.xaml` enlaza el cierre únicamente a F9 y no contiene un enlace Escape; existe una prueba específica que protege este comportamiento.
- Facturación y Devoluciones validan precondiciones antes de invocar operaciones críticas y mantienen confirmaciones visibles para cancelar ventas, quitar líneas, autorizar devoluciones y cerrar turnos.
- El cierre de caja registra errores técnicos mediante el logger oficial y mantiene la impresión como operación posterior al cierre persistido.
- Las ventas y devoluciones usan identificadores de intento para evitar duplicaciones y presentan mensajes operativos sin exponer detalles técnicos.
- Los controles XAML principales incluyen nombres de automatización, ayudas de atajos y límites de longitud en varios campos sensibles.

## Siguientes pasos

1. Resolver de forma coordinada el logging de `CustomerCreateDialog`, preferiblemente junto con una prueba de UI o una prueba del flujo que lo crea.
2. Definir restricciones compartidas para la búsqueda de devoluciones y para la longitud del PIN, incluyendo la pantalla PIN fuera de este alcance.
3. Mantener la prueba de atajos de Corte de Caja al modificar sus `CommandBinding` o `KeyBinding`.
4. Reducir las advertencias CS1591 pendientes fuera de este alcance; la compilación del proyecto WPF sigue reportándolas en modelos y servicios no revisados.

## Estado de aplicación

| Hallazgo | Estado | Motivo |
|---|---|---|
| Dependencias de Facturación sin validación fail-fast | APLICÓ | Refactor defensivo local, sin cambio del flujo normal. |
| Cancelación asíncrona tratada como excepción silenciosa | APLICÓ | Se distingue la cancelación esperada de errores no asociados al token. |
| Límites y precisión duplicados como valores mágicos | APLICÓ | Se reutilizan restricciones existentes y se conserva el mismo valor efectivo. |
| Excepciones de Devoluciones sin registro técnico | APLICÓ | Se usa el logger ya inyectado y se conserva el mensaje seguro. |
| Alta de cliente traga errores inesperados | DIFIRIÓ | Añadir logger requiere cambiar el constructor y su creación manual; hacerlo sin pruebas de UI sería riesgoso. |

