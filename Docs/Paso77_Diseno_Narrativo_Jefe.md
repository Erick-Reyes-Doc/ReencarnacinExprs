# Paso 77 — Preparación narrativa del jefe final (análisis y diseño)

Estado: **solo análisis y diseño**. No se programó nada, no se escribieron diálogos y no se creó ninguna pantalla.
Todo lo marcado como *propuesta* requiere aprobación antes de implementarse.

## 1. Sistemas existentes que se pueden reutilizar (verificados en código)

| Necesidad | Lo que ya existe | Cómo se reutiliza |
|---|---|---|
| Modal narrativo | Modal de "La máscara rota" en `WorldMapController` (`eventPanel`, `eventTitleText`, `eventBodyText`, botón confirmar y botón volver; construido en `BuildEventPanel`). El velo bloquea clics en el mapa. | Base del reproductor de escenas. Hoy muestra **una sola página** de texto, con título y texto editables desde el Inspector (`eventTitle`, `eventNarrative`). |
| Bloqueo de la UI mientras hay evento | `eventOpen` ya impide `Select`, `SelectLocation`, `PrepareStart`, tienda, inventario y menú. | Mismo patrón para las secuencias del jefe. |
| Activación única | `GameSession.completedEncounters` (HashSet de sesión, se vacía en `EndSession`); `TryLiberateNoelle` es el patrón de "marcar solo una vez y solo si se cumple la condición". | Un segundo conjunto de "secuencias vistas" con la misma vida útil. |
| Transición mapa → combate | `WorldMapController.StartSelected()` → `PrepareStart()` (valida `CanStart`, guarda el encuentro pendiente) → `SceneManager.LoadScene`. `EncounterLoader` vuelve a validar con `MapProgression.CanStart`. | El diálogo se inserta **entre** el clic y `PrepareStart`. |
| Victoria y recompensas | `CombatSessionBinder.HandleBattleWon` entrega Coronas solo si el encuentro no estaba completado y lo marca; XP la entrega `TurnManager`. Panel de victoria con botón "Continuar" → `CombatHudController.ContinueToMap()`. | No se toca: las recompensas ya se entregan **antes** de cualquier escena posterior, por lo que una escena final no puede duplicarlas. |
| Derrota | Panel DERROTA → `ReturnToMenu()` → `GameSession.EndSession()`. Nueva partida → `PrepareNewGame()` → `StartNewGame()`. | Sin cambios. Al terminar la sesión, las secuencias vistas se reinician solas. |
| Estado de final de demo | `MapProgression.BossUnlocked()` (C2) y `AllCompleted()` (BOSS), ya separados en el Paso 76. Indicador temporal "Jefe 1/1". | Condición para mostrar el final. |
| Texto en combate | `BossPhaseData.Phase.announcement` y `bannerText` (Fase 2), anuncios de habilidades del Capitán en el HUD, `Say()`. | Cualquier línea **dentro** del combate se puede hacer con datos que ya existen, sin código nuevo. |
| Datos por encuentro | `EncounterData` (ScriptableObject por encuentro, ya referenciado desde mapa y cargador). | Lugar natural para enlazar las secuencias de cada encuentro. |
| Arte provisional | `MapArtSet` (sprites sustituibles sin tocar código), medallón hexagonal y formas provisionales. | Retratos y marcos definitivos se cambian en datos. |

Observaciones:
- La escena `IntroCinematic` está en Build Settings pero **ningún código ni escena la referencia** (el menú va directo a `Map`). No es aprovechable para el jefe y no se debe tocar en este paso.
- El evento de Noelle es un nodo propio dentro de `MapProgression.Order` (`EVT_NOELLE`). **No** conviene repetir ese patrón para el Capitán: añadiría un nodo extra entre C2 y BOSS, y el flujo pedido es "seleccionar Trono Sellado → escena → combate".

## 2. Propuesta de flujo previo al combate

1. El jugador gana C2 → BOSS queda disponible (ya implementado).
2. Selecciona Trono Sellado y pulsa "Iniciar combate".
3. `StartSelected()` comprueba: BOSS disponible (`CanStart`), con secuencia de entrada definida y no vista en la sesión.
4. Si se cumple, abre el modal narrativo. **Todavía no** guarda el encuentro pendiente.
5. El jugador avanza página a página (Siguiente / Omitir si se aprueba permitirlo).
6. Al terminar: marca la secuencia como vista, revalida `CanStart("BOSS")`, ejecuta `PrepareStart()` y carga el combate.
7. Si cierra con "Volver" en medio, no pasa nada: no se marcó nada y el mapa queda como estaba.

Garantías de legitimidad:
- La secuencia solo arranca desde `StartSelected()` de BOSS en estado Available. No existe otra puerta (el cargador de combate no la dispara; `DevEncounter` de editor la salta a propósito).
- Un BOSS completado no puede iniciarse, así que la entrada no puede repetirse.
- Con derrota la sesión termina; en una partida nueva se vuelve a ver, como debe ser.

## 3. Propuesta de flujo posterior a la victoria

Se distinguen cinco capas, de la más técnica a la más narrativa:

1. **Victoria jugable** (ya existe): banner VICTORIA y panel con "Continuar".
2. **Recompensas y progreso** (ya existe y ocurre primero): 200 XP, 100 Coronas, BOSS marcado, `AllCompleted()` verdadero, guardado del equipo. Nada de lo que sigue puede repetirlo.
3. **Posible escena narrativa** (propuesta): al pulsar "Continuar" se vuelve al mapa (comportamiento actual). Al entrar, el mapa detecta "BOSS completado y secuencia final no vista" y abre el mismo modal con la secuencia final.
4. **Mensaje de fin de demo** (propuesta): última página de esa secuencia, o una tarjeta neutra independiente, con texto breve y sustituible (ej. marcada `[PROVISIONAL]`).
5. **Navegación** (a decidir por el equipo). Opciones:
   - A (recomendada): tras la tarjeta de fin queda el mapa explorable (tienda e inventario siguen disponibles, nada repetible) y el botón "Volver al menú" existente, con su confirmación.
   - B: la tarjeta de fin ofrece dos botones: "Volver al mapa" y "Volver al menú".
   - C: volver al menú automáticamente. Es la menos recomendable: corta sin que el jugador vea el mapa completado.

Por qué la escena final va en el mapa y no en el combate: el combate no tiene modal narrativo (solo el panel de victoria de un botón). Construir otro en el HUD sería una segunda implementación; el mapa ya tiene el suyo.

Lo que **no** se decide aquí: destino del Capitán, nuevas revelaciones sobre el Rey Demonio o los protagonistas, ni la existencia de escena final. La secuencia final puede quedar vacía y el sistema debe seguir funcionando solo con el mensaje de fin de demo.

## 4. Componentes que harían falta (arquitectura mínima)

Todos son aditivos; los nueve encuentros previos y el Capitán no cambian.

1. `NarrativeSequenceData` (ScriptableObject): lista de páginas `{ speaker, texto, retrato opcional, lado }`, `omitible`, etiqueta del botón final. Los textos viven en el asset: se cambian sin tocar código.
2. Dos campos opcionales en `EncounterData`: `introSequence` y `outroSequence`. Si están vacíos, el encuentro se comporta exactamente como hoy.
3. `GameSession`: conjunto de secuencias vistas (`IsSequenceSeen` / `MarkSequenceSeen`), vaciado en `EndSession`.
4. Reproductor de secuencias en el mapa: generalizar el modal del evento (varias páginas, Siguiente/Omitir, callback al terminar). El evento de Noelle sigue funcionando con su propio flujo; migrarlo a una secuencia de una página es opcional y se hace aparte.
5. Dos puntos de enganche en `WorldMapController`: en `StartSelected` (entrada) y al iniciar la escena `Map` cuando BOSS está completado (salida y fin de demo).
6. Sprites: el retrato es un `Sprite` del asset (placeholder hoy). El Capitán de combate ya se cambia por el prefab; ninguno de los dos requiere código al llegar el arte definitivo.

Requisitos cubiertos:
- Activación única: conjunto de secuencias vistas + estado de BOSS.
- Continuación al combate: la secuencia termina llamando a `PrepareStart()`.
- Recompensas duplicadas: imposibles, porque se entregan en el combate, antes de la escena final, con la guarda existente.
- Derrota y nueva partida: `EndSession()` vacía todo.
- Textos sustituibles: viven en assets y en la secuencia.

## 5. Riesgos

Técnicos:
- Estados de bloqueo: el modal debe sumarse a las comprobaciones `eventOpen`/`loading` para evitar doble clic, cambio de selección o salida al menú a mitad de secuencia.
- Orden de validación: `CanStart` debe revisarse al terminar la secuencia, no solo al empezarla.
- Texto largo: el modal actual es de 920×520 px con fuente 26; hay que limitar la longitud por página y revisar el desbordamiento.
- `DevEncounter` y los comandos de prueba se saltan el mapa: las pruebas automáticas de combate no ven la secuencia; hay que probarla desde el mapa.
- No hay transición visual entre mapa y combate (carga inmediata): un corte directo tras el diálogo puede sentirse brusco. Un fundido es opcional.
- Tamaño del retrato del Capitán: ajustar escala cuando haya sprite definitivo (el HUD ya tiene este riesgo anotado desde el Paso 66).

Narrativos:
- Cualquier línea del Capitán debe respetar su identidad (comandante de las Fuerzas Expedicionarias del Rey Demonio; frío, calculador, disciplinado, leal; investiga a los héroes reencarnados y debilita asentamientos humanos; sin misericordia).
- Evitar fijar el destino del Capitán: ganar el combate no implica necesariamente su muerte.
- Evitar voces nuevas de los protagonistas o revelaciones sobre el Rey Demonio sin aprobación.
- Mantener coherencia con lo ya mostrado en combate: anuncios de habilidades del Capitán y mensaje de Fase 2 (ya aprobados por el equipo).
- Los textos provisionales deben llevar la marca `[TEXTO PROVISIONAL]`, igual que el evento de Noelle, para que no se confundan con la historia oficial.

## 6. Revisión de Coronas (Paso 76): 390 vs 450

**Resultado: no es un error de progresión. Es un artefacto del estado de prueba.**

Evidencia:
- Economía de la campaña: 50 iniciales + P1–P4 (4×25 = 100) + B1–B3 (3×40 = 120) + C1–C2 (2×60 = 120) = **390 tras C2**. Coincide con el log real de la campaña (C2: 330 → 390) y con `coins: 390` en el estado de llegada guardado.
- Ese estado de llegada **ya incluye** la recompensa de C2 y el nivel/XP posteriores.
- En la prueba del Paso 76 cargué ese estado y marqué como completados solo los encuentros hasta C1 (no C2). Después jugué C2 de verdad: la recompensa se entregó **otra vez** (390 + 60 = 450, y XP 100 + 150). El combate del jefe sumó +100 una sola vez (450 → 550).
- Un recorrido consistente da 390 al llegar al jefe y 490 tras vencerlo (sin compras). En la prueba de derrota, partiendo del estado correcto (C2 marcado), las Coronas quedaron en 390 y no se dio nada.
- El código es correcto: `HandleBattleWon` entrega Coronas solo si el encuentro no estaba completado. Repetir BOSS ya está bloqueado (Coronas 550 → 550 en el intento de repetirlo).

No se cambia la economía ni el código. Recomendación: para pruebas, cargar el estado de llegada **y** marcar también C2 (como hace la herramienta de balance), o jugar la campaña completa.

## 7. Orden recomendado de implementación (tras aprobación)

1. Aprobar con el equipo: texto del fin de demo, si hay escena de entrada, escena final, navegación (A/B/C) y si se permite "Omitir".
2. Datos sin comportamiento: `NarrativeSequenceData` y los dos campos en `EncounterData` (vacíos). Verificar que nada cambia.
3. `GameSession`: secuencias vistas + prueba de reinicio con nueva partida y derrota.
4. Reproductor generalizado en el mapa, con el evento de Noelle intacto (regresión de "La máscara rota").
5. Enganche de entrada en BOSS, con **textos placeholder marcados**; prueba de flujo mapa → diálogo → combate, cierre a medias, doble clic, repetición imposible.
6. Enganche de salida y tarjeta de fin de demo; prueba de recompensas únicas, derrota, nueva partida.
7. Regresión completa: C2, los nueve encuentros, Noelle, inventario/Coronas/niveles, habilidades del Capitán.
8. Cuando haya texto aprobado y sprites definitivos, sustituir solo los assets.
