# Paso 66 — Arquitectura técnica del jefe final (Capitán de las Fuerzas Expedicionarias)

Estado: **solo diseño**. No se ha programado ni configurado ningún dato del Capitán.
Todas las cifras, habilidades y fases son propuestas pendientes de aprobación.

## 1. Componentes existentes reutilizables (verificados en código)

| Necesidad | Componente actual | Notas |
|---|---|---|
| Unidad enemiga con stats | `UnitData` (+ `isBoss`) | `isBoss` ya da inmunidad a Stun (`StatusEffectController`). |
| Definición de enemigo | `EnemyArchetypeData` | `Zone.Jefe` ya existe; guarda UnitData, ataque básico, especiales, estilo y retardo. |
| Prefab de enemigo | `BattleUnit` + `EnemyAIController` + `AbilityCooldownController` + `StatusEffectController` | Igual que los 11 enemigos actuales. |
| Habilidades | `AbilityData` | Soporta penetración de defensa, estados, empuje/atracción, movimiento del usuario, cooldown, área (`AllEnemies`). |
| Ataque anunciado | `AbilityData.requiresPreparation` + `PreparedAbilityController` | Se anuncia, inicia cooldown y resuelve al inicio del siguiente turno propio; se cancela si la unidad cae o está aturdida. Hoy solo para `AllEnemies`. El HUD ya muestra "X prepara Y" y la etiqueta PRE. |
| Orden de turnos / victoria | `TurnManager` | `OnBattleWon` -> XP, recuperación, Coronas, `MarkEncounterCompleted`. |
| Carga del encuentro | `EncounterData` + `EncounterLoader` | `Validate` admite 1-4 enemigos; `Zone.Jefe` -> 100 Coronas por defecto. |
| IA táctica | `EnemyAIController.Tactical` (v0.2) | Puntúa (habilidad, objetivo) con herido/remate/amenaza/foco; perfil de Corrupción también cubre `Jefe`. |

## 2. Huecos reales (lo que NO existe hoy)

1. **IA enemiga solo usa ataques `Single`**: `CollectTargets` descarta área/grupales y `IsUsable` descarta `requiresPreparation`. El Capitán no podría usar ataques anunciados ni de área.
2. **Sin fases**: nada reacciona a umbrales de vida.
3. **Sin resistencias**: solo existe la inmunidad a Stun (`isBoss`). No hay inmunidades por estado ni reducción por tipo.
4. **Mapa**: `BOSS` no está en `MapProgression.Order`; `StateText` muestra "Pendiente de implementación" y el nodo "Trono Sellado" no es iniciable.
5. **Final de demo**: tras ganar solo existe el panel de victoria normal; no hay pantalla/flujo de conclusión.
6. **Prefab, `EncounterData` y sprites del jefe**: no existen (y no se deben reutilizar personajes existentes como sustituto permanente).

## 3. Arquitectura recomendada (extensión, sin duplicar lógica)

- **Datos**: un `EnemyArchetypeData` (zona `Jefe`) + un prefab `Capitan` con los mismos componentes que cualquier enemigo. Stats y habilidades como assets `UnitData` / `AbilityData` normales -> sin fórmulas nuevas de daño.
- **IA**: no tocar v0.1/v0.2. Añadir un componente opcional `BossTacticsController` (hermano de `EnemyAIController`) o una subclase que sobrescriba `ChooseOption`/`ChooseTarget` (ya `protected virtual`). Solo actúa en el Capitán, así los comunes no cambian.
  - Selección de objetivo prioritaria: reutiliza lecturas de `ExpectedDamage`, vida %, amenaza y estados (ya en la IA táctica).
  - Soporte de ataques de área y preparados: ampliación **aditiva** y solo habilitada para el jefe (flag), llamando a `AbilitySystem.ExecuteAreaAbility`, que ya implementa preparación y resolución.
- **Ataques anunciados**: reutilizar `PreparedAbilityController` tal cual. El HUD ya los muestra. Único ajuste pendiente: permitir que el jugador cancele/mitigue (aturdir no funciona en jefe por `isBoss`; alternativas a decidir: empujar/Marca/matar objetivo, o definir contrajuego).
- **Fases (si se aprueban)**: `BossPhaseController` que escucha el cambio de vida de `BattleUnit` y, al cruzar un umbral, cambia el conjunto de habilidades / perfil de IA (data-driven: lista `{umbral %, habilidades, estilo}`), con aviso en el HUD (`Say`). Sin ser necesario cambiar `TurnManager`.
- **Inmunidades/resistencias**: lista serializable `immuneStatuses` en el controlador del jefe consultada desde `StatusEffectController.TryApply` (cambio de 3 líneas, mismo patrón que `isBoss`). No se añaden fórmulas de daño.
- **Victoria y final de demo**: `EncounterData` del jefe con `zone = Jefe`; `CombatSessionBinder` ya da XP/Coronas y marca completado. Añadir `"BOSS"` al final de `MapProgression.Order` (tras C2) y un flag `EncounterData.endsDemo` que cambie el panel de victoria por una pantalla de conclusión. Esto último se difiere hasta definir el texto/flujo.

## 4. Implementado en este paso

Nada en el código ni en datos. Solo este documento. No se crearon assets, no se habilitó el nodo en el mapa, no se tocó balance, IA v0.2, habilidades de protagonistas, transformación de Noelle, recompensas ni sprites.

## 5. Componentes nuevos que realmente se necesitan (todos pendientes)

1. Assets: `UnitData`, `EnemyArchetypeData`, prefab y `EncounterData` del Capitán + `AbilityData` de sus habilidades (requiere aprobación de stats).
2. Extensión de IA para jefe (área + preparadas + prioridad de objetivo), aislada del flujo v0.1/v0.2.
3. `BossPhaseController` (solo si se aprueban dos fases).
4. Lista de inmunidades/resistencias (cambio mínimo en `StatusEffectController`).
5. Habilitar `BOSS` en `MapProgression`/`WorldMapController` y flujo de conclusión de la demo.

## 6. Riesgos

- **IA**: ampliar `CollectTargets`/`IsUsable` sin flag afectaría a enemigos comunes -> debe quedar restringido al jefe y cubierto por la regresión de los 9 encuentros.
- **Preparados**: `PreparedAbilityController` solo se resuelve en el turno del propio usuario; si el Capitán muere antes se cancela (correcto), pero el aviso debe ser claro y dar tiempo de reacción (no hay forma de evitar el daño salvo Guard/evasión).
- **Slots**: el encuentro permite 1-4 enemigos; escoltas cambiarían compactación de posiciones (`ReorganizeTeam`) y el balance de focos de la IA.
- **Balance de Economía v0.1**: Jefe = 100 Coronas; XP aún sin definir (el valor por defecto es 50).
- **Mapa**: `MapProgression.Order` define desbloqueo secuencial; añadir `BOSS` cambia `AllCompleted()` (hoy = C2) usado por `StateText`.
- **HUD**: tarjetas/capturas diseñadas para sprites actuales; un sprite grande del Capitán exigirá revisar escala y colocación.
- **Sin sprites definitivos** del Capitán.

## 7. Orden recomendado de implementación (tras aprobación)

1. Aprobar stats, habilidades y si hay 2 fases (Diseño).
2. Assets del Capitán con sprite provisional neutro y encuentro **deshabilitado** en el mapa.
3. Ataque básico + habilidades `Single` (sin cambios de código; probar con IA v0.2).
4. Extensión de IA para jefe: área y ataques anunciados.
5. Resistencias/inmunidades.
6. Fase 2 (`BossPhaseController`).
7. Habilitar `BOSS` en el mapa y pantalla de conclusión de la demo.
8. Regresión: 9 encuentros, Noelle, recompensas; simulación de balance del jefe.
