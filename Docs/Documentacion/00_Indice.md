# Documentación técnica — Reencarnación Express (Paso 79)

Documentación para que el equipo comprenda, mantenga y presente el proyecto. Está basada **únicamente** en lo que se pudo
leer en el repositorio (escenas, scripts, assets y documentos existentes en `Docs/`). No se modificó ninguna escena, prefab,
script del juego, estadística, habilidad ni balance para escribirla.

| Archivo | Contenido |
|---|---|
| `01_Guia_de_Escenas.md` | Las cuatro escenas, jerarquía de combate (`00_Cameras` … `90_EditorHelpers`), objetos de prueba vs. objetos usados, `IntroCinematic`. |
| `02_HUD_y_Configuracion_Visual.md` | `CombatHudController`, qué se guarda y qué se genera en ejecución, vista previa del editor, por qué Scene View y Game View difieren, Canvas y resolución 1920×1080, cómo abrir el proyecto en otro equipo. |
| `03_Guia_de_Demostracion.md` | Guion explicativo: flujo, progresión, turnos, movimiento, habilidades, consumibles, Noelle, Capitán y qué está (y no está) probado. |

Documentos previos relacionados: `Docs/Paso66_Arquitectura_Jefe_Final.md`, `Docs/Paso77_Diseno_Narrativo_Jefe.md`,
`Docs/Paso78_Organizacion_Escenas.md`, `Docs/Balance_Capitan_Paso72…75_*.txt`, capturas en `Docs/Capturas/`.

## Mapa rápido del código

| Carpeta | Qué hay |
|---|---|
| `Assets/Scripts/Core` | `GameSession` (estado de partida), `MapProgression` (orden y desbloqueos), `ConsumableService` (tienda y objetos). |
| `Assets/Scripts/UI` | Menú (`MainMenuController`), mapa (`WorldMapController` en 3 archivos parciales, `MapShopController`, `MapAmbientController`, `MapArtSet`). |
| `Assets/Scripts/Combat/Core` | `TurnManager`, `PositionManager`, `DamageCalculator`, `CombatSessionBinder`, `TargetSelector` y componentes de prueba. |
| `Assets/Scripts/Combat/Abilities` | `AbilityData`, `AbilitySystem`, `AbilityCooldownController`, `CharacterAbilityLoadout`, `PreparedAbilityController`. |
| `Assets/Scripts/Combat/Units` | `BattleUnit`, progresión de personajes, IA enemiga (`EnemyAIController` y sus parciales `.Tactical`/`.Boss`), fases del jefe. |
| `Assets/Scripts/Combat/Effects` | Estados alterados (`StatusEffectController`, `StatusEffectType`, regeneración). |
| `Assets/Scripts/Combat/Encounters` | `EncounterData`, `EncounterLoader`. |
| `Assets/Scripts/Combat/UI` | `CombatHudController`, `AbilityIconSet`. |
| `Assets/Editor` | Solo editor: `SceneTools/` (encuadre, guías, vista previa del HUD, resolución de Game View) y `BossBalanceHarness.cs` (balance del jefe). |
| `Assets/ScriptableObjects` | Datos: personajes, progresión, habilidades, enemigos, encuentros. |
| `Assets/Prefabs` | `Enemies/` (11 enemigos) y `Bosses/CapitanExpedicionario.prefab`. |

## Información que no se pudo verificar

1. **Otros equipos y sistemas operativos:** todo se ejecutó en un único equipo Windows; no hay registro de pruebas en otra computadora.
2. **Compilaciones (builds) del juego:** no hay registro de una build ejecutable; solo se trabajó en el editor.
3. **Pruebas con jugadores humanos:** las pruebas documentadas son automáticas (bot de balance y comandos de editor).
4. **Componentes de prueba heredados** (`BattleActionTest`, `MovementTest`, etc.): están habilitados en la escena de combate; su
   efecto durante una partida normal no se probó específicamente.
5. **Relaciones de aspecto distintas de 16:9** (p. ej. ultraancho): el código prevé el relleno del mapa (`MundoBordes`) pero
   solo se comprobó 1920×1080 y 1280×720.
6. **Equilibrio general más allá del jefe:** el balance de los nueve encuentros comunes proviene de pasos anteriores; en este paso no se reevaluó.
7. **Texto narrativo:** el del evento de Noelle y el de la fase 2 del Capitán es provisional; no hay diálogos oficiales del jefe.
