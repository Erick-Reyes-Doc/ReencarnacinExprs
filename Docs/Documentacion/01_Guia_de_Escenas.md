# 01 — Guía de escenas y jerarquía de combate

Proyecto: **Reencarnación Express** — Unity 6 (6000.6.4f1), render 2D con URP 17.6, Input System 1.20, uGUI 2.6.
Todo lo descrito aquí se verificó leyendo las escenas, los scripts y los assets del repositorio. Lo que no se pudo
verificar está marcado como **[No verificado]**.

## 0. Escenas del proyecto y orden en Build Settings

| Índice | Archivo | ¿Participa en el flujo? |
|---|---|---|
| 0 | `Assets/Scenes/Menu.unity` | Sí (escena inicial) |
| 1 | `Assets/Scenes/IntroCinematic.unity` | **No** (ver §4) |
| 2 | `Assets/Scenes/Map.unity` | Sí |
| 3 | `Assets/Scenes/CombateIntegracion.unity` | Sí |

Flujo real: `Menu` → (Nueva partida) → `Map` → (Iniciar combate) → `CombateIntegracion` → (Continuar) → `Map`.
Derrota o "Volver al menú" → `Menu` (la sesión se descarta).

Estado del juego: no vive en escenas sino en la clase estática `GameSession` (`Assets/Scripts/Core/GameSession.cs`),
que guarda en memoria Coronas, consumibles, encuentros completados, la "foto" del equipo (nivel, XP, vida, Noelle liberada)
y el encuentro pendiente. No usa PlayerPrefs ni ScriptableObjects. `GameSession.EndSession()` lo borra todo.

---

## 1. `Menu.unity` — menú principal

**Propósito:** pantalla de título con tres botones (Nueva partida, Opciones, Salir). Al llegar al menú por cualquier vía,
`MainMenuController.Start()` llama a `GameSession.EndSession()`: no puede quedar una partida anterior viva.

| Elemento | Detalle verificado |
|---|---|
| Cámara | `Main Camera`, ortográfica (tamaño 5), fondo de color sólido, con `AudioListener` y `UniversalAdditionalCameraData`. |
| Luz | `Global Light 2D` (URP 2D). |
| Canvas | `Canvas`: Screen Space – Overlay, orden 0, `CanvasScaler` en *Scale With Screen Size*, referencia **1920×1080**, *Match Width Or Height* = **0.5**. |
| Hijos del Canvas | `MenuBackground` (fondo a pantalla completa), `GameLogo`, `ButtonsContainer` (3 botones con `VerticalLayoutGroup`), `OptionsPanel` (desactivado por defecto; `CanvasGroup` con animación y sliders de volumen general y de música). |
| Controlador | Objeto `MainMenuManager` con `MainMenuController` + `AudioSource` (música del menú). |
| Otros | `EventSystem` con `InputSystemUIInputModule`. |

**Nueva partida:** `MainMenuController.NewGame()` → `PrepareNewGame()` comprueba que la escena `Map` esté en Build Settings,
llama a `GameSession.StartNewGame()` (50 Coronas, 1 poción, 0 antídotos) y carga `Map`.

---

## 2. `Map.unity` — mapa del mundo

**Propósito:** elegir el siguiente encuentro, abrir el evento narrativo, comprar/usar consumibles y volver al menú.
El mapa solo **presenta y navega**; las reglas viven en `MapProgression`, `GameSession`, `ConsumableService` y `EncounterData`.

### Jerarquía (raíces)
- `Main Camera` (color sólido; el Canvas overlay la cubre por completo).
- `MapShop` → `MapShopController` + hijos `Canvas` (overlay, orden 100) y `EventSystem`.
- `WorldMap` → `WorldMapController` (+ `.Locations.cs` y `.Visuals.cs`, clases parciales) y `MapAmbientController`.
  - `WorldMapCanvas` (overlay, orden 0):
    - `FondoGeneral` (color de relleno).
    - `MapArea` — lienzo fijo de 1920×1080 con las capas: `FondoBase` (ilustración + `MundoBordes`), `NubesAnimadas`,
      `NieblaAmbiental`, `EfectosRegionales`, `NieblaBloqueo`, `MarcadoresInteractivos`, `PanelesUI`.
    - `BarraSuperior` (Coronas, pociones, antídotos, progreso por zona, botones Tienda / Inventario / Volver al menú).
    - `EventoVelo` (modal del evento, desactivado hasta abrirlo).

> Importante para mantenimiento: `MapAmbientController` localiza `WorldMapCanvas/MapArea` **por ruta** (`transform.Find`).
> No se debe renombrar ni reubicar esa jerarquía.

### Ubicaciones (7) y encuentros
Definidas en `WorldMapController.Locations.cs` (`DefaultLocations`), editables en el Inspector.

| Lugar (id) | Región | Contenido |
|---|---|---|
| Campamento del Alba (`camp`) | Pradera | P1, P2 |
| Ruinas de Liria (`ruins`) | Pradera | P3, P4 |
| Claro del Susurro (`clearing`) | Bosque | B1 |
| Santuario Velado (`shrine`) | Bosque | Evento *La máscara rota* (`EVT_NOELLE`) |
| Sendero Sombrío (`trail`) | Bosque | B2, B3 |
| Tierras Marchitas (`wastes`) | Corrupción | C1, C2 |
| Trono Sellado (`gate`) | Corrupción | BOSS (Capitán) |

### Progresión (`Assets/Scripts/Core/MapProgression.cs`)
Orden secuencial: `P1 → P2 → P3 → P4 → B1 → EVT_NOELLE → B2 → B3 → C1 → C2 → BOSS`.
Un nodo está *Disponible* solo si el anterior está completado; el estado se **deduce** de `GameSession` (no hay un segundo registro).
Regiones: Bosque se desbloquea al completar P4; Corrupción al completar B3.
`BossUnlocked()` = C2 completado; `AllCompleted()` = BOSS completado (fin de los encuentros de la demo).

### Tienda e inventario
`MapShopController` solo presenta; las reglas están en `ConsumableService`: poción 35 Coronas (cura 30 % de la vida máxima),
antídoto 20 Coronas (quita sangrado, veneno y quemadura), máximo 5 de cada uno. No se puede abrir durante un combate.

### Evento *La máscara rota*
Disponible tras completar B1. El modal (`EventoVelo`) tiene texto **provisional** editable en el Inspector
(`eventTitle`, `eventNarrative`). Confirmarlo llama a `GameSession.TryLiberateNoelle()` (una sola vez; solo si B1 está completado)
y desbloquea B2. No da XP ni Coronas.

### Scripts de apoyo visual
`MapArtSet` (sprites sustituibles), `MapHoverTarget` (aviso de cursor), `MapAmbientController` (nubes/niebla animadas, sin raycast).

---

## 3. `CombateIntegracion.unity` — combate

**Propósito:** escena única de combate. Los encuentros no son escenas: `EncounterLoader` instancia los enemigos del
`EncounterData` elegido en el mapa dentro de esta misma escena.

### Jerarquía de combate (agrupada en el Paso 78)

| Grupo | Para qué sirve | Contenido |
|---|---|---|
| `00_Cameras` | Cámara del combate. | `Main Camera` ortográfica de tamaño 5 (ve 17,78 × 10 unidades), color sólido. |
| `10_BattleField` | Todo lo que ocupa el campo de batalla. | `BattlePositions` (`PlayerPositions` P1–P4, `EnemyPositions` E1–E4); `Party` (los 4 protagonistas); `Enemies_Placeholder` (4 enemigos de prueba); `Enemies_Runtime` (vacío; padre de los enemigos **generados en ejecución**). |
| `20_UI` | Interfaz. | `CombatHUD` (`CombatHudController`) con su `Canvas` y `EventSystem`. |
| `30_CombatSystems` | Lógica de combate, sin representación visual. | `TurnManager`, `PositionManager`, `AbilitySystem`, `EncounterLoader`, `CombatSession` (`CombatSessionBinder`), `EnemyAISettings`, `CombatMetrics` (`CombatMetricsCollector`). |
| `90_EditorHelpers` | Objetos de prueba y apoyo del editor. | `BattleTestManager` (**desactivado**), `ExperienceTest`. |

Todos los grupos están en el origen con transform identidad: reagrupar no cambió ninguna posición ni escala.

### Qué se usa realmente en la campaña
- **Usados:** los 4 protagonistas (`Party`), `BattlePositions`, `CombatHUD`, `TurnManager`, `PositionManager`, `AbilitySystem`,
  `EncounterLoader`, `CombatSession`, `EnemyAISettings` (versión de IA: la escena tiene seleccionada la **v0.2**).
- **Se generan en Play:** los enemigos del encuentro (hijos de `Enemies_Runtime`), y el rótulo `AvisoFase` del HUD (ver el documento 02).
  Los "enemigos de prueba" de `Enemies_Placeholder` se desactivan al cargar un encuentro (`EncounterLoader.ClearEnemies`).
- **Diagnóstico (solo lectura):** `CombatMetrics`. Sus datos viven en memoria y no se guardan ni se envían.
- **Objetos de prueba heredados:** los componentes `BattleActionTest`, `StatusEffectTest`, `TargetSelector` (en `TurnManager`),
  `MovementTest` (en `PositionManager`), `AbilityTest` (en `AbilitySystem`), `ExperienceTest` y `BattleTest` (desactivado).
  Están presentes y habilitados en la escena. Por el código, `BattleActionTest` responde a la tecla A y `MovementTest` a Q/E.
  **[No verificado]** qué efecto tienen exactamente durante una partida normal: no se ejecutaron pruebas específicas
  de estas teclas. No se eliminaron porque otras herramientas del proyecto aún pueden depender de ellos.

### Protagonistas (datos de `Assets/ScriptableObjects/Players`, nivel 1)

| Nombre en el juego | `unitName` en datos | Vida | ATQ | DEF | VEL | Crítico |
|---|---|---|---|---|---|---|
| Valerian | Caballero | 110 | 14 | 7 | 9 | 10 % |
| Elaina | Arquera | 75 | 13 | 3 | 13 | 2 % |
| Noelle | Noelle | 90 | 10 | 6 | 8 | 5 % |
| Evanora | Evanora | 55 | 19 | 2 | 11 | 12 % |

(El HUD traduce "Caballero" → Valerian y "Arquera" → Elaina; ver `CombatHudController.DisplayName`.)
Crecimiento por nivel en `*Progression.asset` (nivel máximo 10 según la escena). XP necesaria: 100 + 50 × (nivel − 1).

### Componentes que se agregan en ejecución
`PreparedAbilityController` se añade a una unidad la primera vez que prepara una habilidad diferida
(p. ej. Golpe de Meteorito de Evanora o Fuego de Supresión del Capitán).

---

## 4. `IntroCinematic.unity` — estado actual

- Contiene **solo** una `Main Camera` con `AudioListener`: sin Canvas, sin `EventSystem` y sin scripts propios.
- Está marcada en Build Settings (índice 1), pero **ningún script ni escena la referencia**: el menú carga `Map` directamente
  (`MainMenuController.newGameScene = "Map"`).
- Conclusión: es un **marcador de posición** para una futura introducción; hoy no participa en el flujo del juego.
