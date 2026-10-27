# 02 — HUD de combate, configuración visual y herramientas de editor

## 1. `CombatHudController` (`Assets/Scripts/Combat/UI/CombatHudController.cs`)

Interfaz de combate hecha con uGUI. **No contiene reglas de combate**: ejecuta las acciones con `AbilitySystem` y
`ConsumableService`, y los turnos avanzan con `TurnManager`. Responsabilidades:

- Mostrar región/encuentro, ronda, turno activo, cola de turnos (`ELA`, `EVA`, `VAL`, `NOE`, enemigos…) y Coronas.
- Mostrar una **ficha por unidad** (nombre, posición P/E, vida, barra de vida y etiquetas de estado; para el jefe, la etiqueta `FASE2` y la marca `FUEGO` de amenaza).
- Mostrar las 4 habilidades del personaje activo, el panel de información, los consumibles y los botones *Mover* y *Terminar turno*.
- Permitir elegir habilidad → objetivo con el ratón, con una segunda pulsación de confirmación en las acciones que la requieren (`pendingConfirm`); `Esc` cancela cualquier selección pendiente.
- Modo movimiento (Avanzar / Retroceder) con confirmación.
- Avisos: ataques preparados, anuncios de habilidades de enemigos, rótulo de fase del jefe, marcas de amenaza (`FUEGO`).
- Paneles de **Victoria** (botón *Continuar* → `ContinueToMap()`) y **Derrota** (*Volver al menú* → `ReturnToMenu()`).

### Qué está guardado en la escena (se ve fuera de Play)
`BuildUI()` genera la jerarquía y se ejecutó en el editor para **guardarla** en `CombateIntegracion.unity`
(`CombatHudController.Start` solo la reconstruye si `canvas == null`). Bajo `20_UI/CombatHUD/Canvas/Marco` están guardados:

- `BarraSuperior` (`Region`, `Ronda`, `TurnoActivo`, `Turno_0…7`, `Recursos`).
- `Unidades` con `Casilla_0…7` (fichas), `Marcadores` (8 triángulos).
- `Mensajes`, `Registro` (desactivados), `Preparacion`, `Banner`.
- `PanelConsumibles` (`Pocion`, `Antidoto`), `PanelPersonaje` (`Info`, `Habilidad_0…3`, `OpcionesMovimiento`),
  `PanelInformacion` (`TituloHabilidad`, `IconoInfo`, `Detalle`, `Ayuda`, `Mover`, `Volver`, `TerminarTurno`).
- `PanelDerrota` y `PanelVictoria` (desactivados).

### Qué cambia en ejecución
- **Todos los textos** (nombres, vida, rondas, habilidades, ayudas) están vacíos en el editor y se rellenan en `Refresh()`
  (se ejecuta en cada `Update`).
- **Posición de las fichas:** la posición guardada es provisional. `PlaceUnitCard` coloca cada ficha bajo los pies de su unidad
  y nunca por debajo de los paneles inferiores; las fichas de posiciones vacías se ocultan.
- Colores/tintes de ficha (activa, objetivo, amenazada), marcadores sobre la cabeza, botones habilitados/bloqueados,
  visibilidad del botón *Volver*, *OpcionesMovimiento* y paneles de victoria/derrota.
- Se **crea una vez** el rótulo `AvisoFase` (cambio de fase del jefe); no se guarda en la escena.
- Los enemigos del encuentro se instancian bajo `10_BattleField/Enemies_Runtime`; el HUD vuelve a leer las unidades (`RefreshUnits`).

## 2. Vista previa del HUD en el editor (solo editor)

Código en `Assets/Editor/SceneTools/` (no entra en la compilación del juego; no usa `ExecuteAlways`).

| Menú de Unity | Archivo | Función |
|---|---|---|
| Reencarnación › Combate › Vista previa del HUD (solo editor) | `CombatHudPreviewWindow.cs` | Ventana que **renderiza la cámara principal** de la escena abierta a 1920×1080 en un `RenderTexture` temporal y superpone guías: huecos P1–P4/E1–E4, paneles, fichas de unidad con la misma fórmula de `PlaceUnitCard`, y textos de **muestra** (marcados `[muestra]`). Botón *Exportar PNG* → `Docs/Capturas/Paso78/HUD_vista_previa.png`. |
| Reencarnación › Escenas › Guías de Scene View (…) | `SceneGuides.cs` | Dibuja en Scene View el marco 16:9, huecos de combate y paneles del HUD. No se guarda en la escena. |
| Reencarnación › Escenas › Encuadrar escena actual / Encuadre automático al abrir | `SceneViewFraming.cs` | Coloca la Scene View en 2D sobre el área útil de cada escena al abrirla (preferencia por usuario). |
| Reencarnación › Escenas › Ocultar rellenos de aspecto del mapa | `SceneViewFraming.cs` | Oculta `MundoBordes` solo en Scene View (visibilidad del editor; no modifica la escena). |
| Reencarnación › Game View › 1920×1080 / 1280×720 | `GameViewResolution.cs` | Cambia la resolución de Game View (usa la API interna del editor; si falla devuelve un mensaje). |

Garantías de diseño: la ventana no crea sesión, no toca `GameSession`, estadísticas ni inventario, no marca la escena como
modificada, no deja objetos en la escena y libera el `RenderTexture` al cerrarse. Solo funciona fuera de Play Mode y con la
escena `CombateIntegracion` abierta.

## 3. Por qué Scene View y Game View se ven distintos

1. **Canvas de combate en `Screen Space – Camera`:** Unity recalcula su escala según la cámara solo al renderizar en Game View.
   La escala guardada es 0,01 (canvas de 19,2 × 10,8 unidades), pero la efectiva es ≈0,00926 (10 unidades de alto = 2 × tamaño ortográfico).
   Scene View muestra el valor guardado, por eso el HUD parece ~8 % mayor respecto a los personajes. No es un error y la escala
   no puede fijarse en la escena porque Unity la reescribe.
2. **Canvas Overlay (Menú y Mapa):** en Scene View se dibuja como un rectángulo de 1920×1080 *unidades* con origen en (0,0), enorme
   frente al mundo; en Game View ocupa la pantalla.
3. **Contenido que existe solo en ejecución:** textos, posición de fichas, enemigos del encuentro, rótulo de fase.
4. **Marcadores de posición:** los sprites de la escena (`Party`, `Enemies_Placeholder`) son provisionales hasta que el encuentro carga.
5. **`MundoBordes` del mapa:** cuatro `RawImage` fuera del lienzo 1920×1080 que prolongan el borde de la ilustración para pantallas que
   no son 16:9. En Game View a 16:9 quedan fuera de pantalla; en Scene View parecen "bandas verticales". Es intencional.
6. **Cámara de Scene View ≠ cámara de juego:** la primera no sigue a la segunda.

## 4. Configuración visual

**Resolución de referencia: 1920×1080 (16:9)** en todos los `CanvasScaler`.

| Canvas | Modo | Orden | Escalado | Por qué |
|---|---|---|---|---|
| `Menu/Canvas` | Overlay | 0 | Scale With Screen Size, 1920×1080, Match **0,5** | Configuración estándar: equilibra ancho y alto. |
| `Map/MapShop/Canvas` (tienda) | Overlay | 100 | Igual, Match **0,5** (`MapShopController.cs`) | Panel emergente sobre el mapa. |
| `Map/WorldMap/WorldMapCanvas` | Overlay | 0 | 1920×1080, modo **Expand** | El comentario del código lo explica: *"el lienzo 1920×1080 siempre cabe entero en pantalla"*. La ilustración es 16:9; en otras proporciones `MundoBordes` rellena el sobrante sin franjas vacías. |
| `CombateIntegracion/20_UI/…/Canvas` | **Screen Space – Camera** (cámara principal, distancia 5) | 100 | 1920×1080, Match **1,0** (altura) | `PlaceUnitCard` asume que, con Match = altura, la altura del lienzo equivale a 2 × tamaño ortográfico de la cámara; así la conversión mundo → HUD no depende de la resolución. |

Cámaras: las tres escenas usan cámara ortográfica de tamaño 5 y fondo de **color sólido** (unificado en el Paso 78).
Los sprites de personajes usan Sorting Layer *Default*, orden 1 (ninguna capa de fondo propia en combate: el fondo es el color de la cámara).

### Abrir y encuadrar las escenas en otra computadora
1. Instalar **Unity 6000.6.4f1** (versión en `ProjectSettings/ProjectVersion.txt`) con Unity Hub y abrir la carpeta del proyecto.
   `Library/` no se versiona: la primera apertura reimporta los assets y puede tardar.
2. Paquetes necesarios (`Packages/manifest.json`): Universal RP 17.6.0, Input System 1.20.0, uGUI 2.6.0 y los paquetes 2D.
3. Abrir `Assets/Scenes/Menu.unity` (o la escena deseada). Con el *encuadre automático* activo (por defecto), la Scene View
   se coloca en 2D sobre el área útil. Si no, usar *Reencarnación › Escenas › Encuadrar escena actual*.
4. Fijar Game View con *Reencarnación › Game View › 1920×1080* (o crear manualmente una resolución fija de 1920×1080).
5. Para el combate: abrir `CombateIntegracion.unity` y *Reencarnación › Combate › Vista previa del HUD*.
6. Para jugar desde el inicio: abrir `Menu.unity` y pulsar Play. Abrir directamente `Map` o `CombateIntegracion` crea una
   sesión de desarrollo (`GameSession.EnsureSession`).

**[No verificado]** el comportamiento del proyecto en otra computadora o sistema operativo: todo se probó en un único equipo (Windows).
