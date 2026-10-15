# Revisión de mantenibilidad junior: UI compartida

## Resumen

El alcance revisado tiene una base clara: las reglas numéricas, permisos y navegación están separadas en funciones puras o servicios pequeños, y la autenticación evita registrar el PIN. Se encontraron riesgos importantes en el manejo de errores del flujo PIN, en permisos expuestos como colecciones mutables y en código muerto; los refactors seguros fueron aplicados sin cambiar contratos de negocio.

No se identificaron hallazgos Críticos.

## Hallazgos

### Importante — Excepciones del flujo PIN sin tratamiento seguro

- Ubicación: `Ferreteria.PuntoVenta/Views/PIN/PinWindow.xaml.cs:205-253`, `ValidarPinAsync`.
- Problema: el método se dispara con `_ = ValidarPinAsync()`. La consulta de bloqueo ocurría antes del `try`, por lo que una falla del servicio de intentos podía quedar en una tarea no observada. Además, el `catch` mostraba `ex.Message`, lo que podía exponer detalles técnicos en la UI.
- Propuesta: abarcar toda la operación con `try/catch/finally`, registrar la excepción con `ILogger<PinWindow>` sin incluir el PIN y mostrar un mensaje genérico. También validar que el dígito recibido sea exactamente un carácter ASCII entre `0` y `9`.
- Estado: aplicado.

### Importante — Colecciones de permisos declaradas como lectura podían ser mutables

- Ubicación: `Ferreteria.PuntoVenta/Services/Security/PosAuthorizationPolicy.cs:53-61`, `AuthorizationGuard.GetGrantedPermissionsAsync` y `Views/Shell/MainShellWindow.xaml.cs:35`.
- Problema: `IReadOnlySet<T>` expresa una vista de lectura, pero el objeto devuelto era un `HashSet<T>` mutable. Un consumidor que recuperara la implementación concreta podía modificar el conjunto usado para decidir qué secciones mostrar.
- Propuesta: devolver `FrozenSet<PosPermission>` tanto para conjuntos calculados como para conjuntos vacíos, conservando el tipo público `IReadOnlySet<PosPermission>`.
- Estado: aplicado.

### Importante — Método de rechazo con contrato de retorno engañoso

- Ubicación: `Ferreteria.PuntoVenta/Services/Security/AuthorizationGuard.cs:42-63, 98-108`.
- Problema: `Reject` declaraba que devolvía `AuthorizedEmployee`, aunque siempre lanzaba una excepción. El patrón `return Reject(...)` ocultaba el flujo real y dificultaba que un desarrollador junior distinguiera una salida normal de una denegación.
- Propuesta: cambiarlo a `void`, marcarlo como método que no retorna y llamarlo como una salida explícita de rechazo.
- Estado: aplicado.

### Importante — Código muerto en shell y pantalla PIN

- Ubicación: `Ferreteria.PuntoVenta/Views/Shell/MainShellWindow.xaml.cs` y `Ferreteria.PuntoVenta/Views/PIN/PinWindow.xaml`.
- Problema: `GetVisibleSections` no tenía llamadas y los estilos locales `LegacyPinKeyStyle`, `LegacyPinDeleteStyle` y `LegacyPinConfirmStyle` no eran utilizados; los botones usan estilos globales `AppPin*`. Mantenerlos aumenta el área que un cambio puede afectar y confunde sobre cuál es el estilo vigente.
- Propuesta: eliminar los miembros y recursos sin referencias.
- Estado: aplicado.

### Importante — Permisos de navegación cacheados durante toda la vida del shell

- Ubicación: `Ferreteria.PuntoVenta/Views/Shell/MainShellWindow.xaml.cs:159-174, 287-323`.
- Problema: el shell recarga permisos en `OnLoaded`, pero después `ShowSection` decide con `_grantedPermissions` sin volver a consultar el guard. Si un permiso se revoca mientras la ventana sigue abierta, la UI puede conservar botones y rutas visibles hasta una nueva carga.
- Propuesta: definir una política de refresco al cambiar de sección o invalidar la navegación cuando cambie el empleado/estado de sesión. La solución requiere decidir el comportamiento de UX y el costo de consultas, por lo que no se aplicó como refactor mecánico.
- Estado: diferido por riesgo de cambiar el flujo de navegación.

### Importante — Eventos asíncronos del shell sin recuperación uniforme

- Ubicación: `Ferreteria.PuntoVenta/Views/Shell/MainShellWindow.xaml.cs:157-178, 198-205, 267-273`.
- Problema: `OnLoaded`, el timer y el comando F5 son `async void`; errores de autorización, conectividad o apertura de caja pueden terminar en el dispatcher de WPF sin un mensaje de recuperación ni un registro local específico.
- Propuesta: centralizar el manejo de excepciones de eventos de UI, registrar con el logger oficial y dejar la pantalla en un estado seguro (por ejemplo, navegación sin permisos y estado de conectividad desconocido). Requiere definir mensajes y degradación esperada, por lo que se difirió.
- Estado: diferido por riesgo de modificar recuperación y UX sin una decisión funcional.

### Menor — La propiedad `FieldHelper.IsRequired` no revierte el estado accesible

- Ubicación: `Ferreteria.PuntoVenta/Helpers/FieldHelper.cs:34-54`.
- Problema: cuando el valor cambia de `true` a `false`, el callback retorna sin quitar `IsRequiredForForm` ni el sufijo `(obligatorio)`. El uso actual parece activarlo de forma estática, pero el comportamiento sería sorprendente si se enlaza a un valor dinámico.
- Propuesta: guardar el nombre accesible original y restaurarlo al desactivar la propiedad, además de limpiar `IsRequiredForForm`. No se aplicó porque requiere estado adicional para no borrar texto configurado por la vista.

### Menor — `NavSections` expone arrays internos como `IReadOnlyList`

- Ubicación: `Ferreteria.PuntoVenta/Services/NavSections.cs:47-73`.
- Problema: `CajaSections` e `InventarioSections` son arrays y se devuelven directamente. Aunque el tipo declarado sea de solo lectura, un consumidor podría intentar recuperar la implementación concreta y modificar el orden global.
- Propuesta: encapsularlos con `Array.AsReadOnly` o una colección inmutable, manteniendo el orden y la firma pública.

## Lo que está bien

- `DecimalInputParser` y `NumericInputTextRules` separan el análisis puro de los eventos WPF y devuelven resultados explícitos con mensajes en español.
- `FieldConstraints` centraliza longitudes, precisión y escala, y las pruebas verifican deriva contra el esquema y metadatos EF.
- `AuthorizationGuard` recarga el empleado desde la base de datos con `AsNoTracking`, valida la sesión y registra las denegaciones sin incluir secretos.
- `NavigationShortcutMap`, `PendingSaleNavigationGuard` y `NavSections` mantienen decisiones de navegación pequeñas, deterministas y cubiertas por pruebas.
- La pantalla PIN muestra puntos, limpia el valor después de intentos fallidos y no registra el PIN.
- Los miembros públicos del alcance tienen documentación XML en español y los cambios agregados conservaron ese estándar.

## Siguientes pasos

- Prioridad 1: acordar la estrategia de refresco de permisos del shell y aplicarla también a la habilitación de botones.
- Prioridad 2: definir el estado visual seguro para fallos de carga inicial, timer de conectividad y apertura de caja; después centralizar el manejo de errores de eventos WPF.
- Prioridad 3: corregir la reversión de `FieldHelper.IsRequired` y encapsular las listas estáticas de `NavSections`.
- Mantener la ejecución de pruebas unitarias y UI de los proyectos individuales mientras el `.slnx` no produzca correctamente sus artefactos de tests con `--no-build`.

## Estado de aplicación

- Crítico: **APLICÓ** — no se identificaron hallazgos Críticos que requirieran cambios.
- Importante — excepciones y mensajes técnicos del PIN: **APLICÓ**; todo el flujo está dentro del manejo de errores, se usa el logger oficial y la UI muestra un mensaje genérico.
- Importante — colecciones de permisos mutables: **APLICÓ**; se usan `FrozenSet` sin cambiar el contrato `IReadOnlySet`.
- Importante — retorno engañoso de `Reject`: **APLICÓ**; ahora el método es `void` y está marcado como no retornante.
- Importante — código muerto: **APLICÓ**; se eliminaron el método privado sin llamadas y los tres estilos PIN sin referencias.
- Importante — permisos cacheados del shell: **DIFIRIÓ**; la corrección exige una decisión de refresco y puede cambiar el comportamiento de navegación.
- Importante — eventos asíncronos del shell: **DIFIRIÓ**; falta acordar la recuperación visible y el estado seguro esperado para cada dependencia.
