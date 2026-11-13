# 03 — Guía de demostración del proyecto

Texto pensado para una exposición. Todo lo que se afirma sale del código y de los assets del repositorio; las pruebas
mencionadas son únicamente las que tienen registro en `Docs/` (ver §9).

## 1. Qué es
**Reencarnación Express**: JRPG táctico por turnos en 2D (Unity 6, URP 2D). Cuatro protagonistas reencarnados avanzan por
tres regiones (Pradera, Bosque, Corrupción) y se enfrentan a un jefe final, el **Capitán de las Fuerzas Expedicionarias**.

## 2. Flujo Menú → Mapa → Combate
1. **Menú** (`Menu.unity`): *Nueva partida* inicia una sesión limpia (50 Coronas, 1 poción, 0 antídotos).
2. **Mapa** (`Map.unity`): se elige un lugar, se ve su estado y se pulsa *Iniciar combate*. El mapa guarda el encuentro pendiente
   y carga la escena de combate.
3. **Combate** (`CombateIntegracion.unity`): `EncounterLoader` crea los enemigos del encuentro y comienza la batalla.
4. **Victoria:** panel con *Continuar* → vuelve al mapa conservando la sesión.
   **Derrota total:** panel con *Volver al menú* → la sesión se descarta (no hay reintento ni puntos de control).

## 3. Progresión de encuentros
Orden fijo: `P1 → P2 → P3 → P4 → B1 → Evento → B2 → B3 → C1 → C2 → Jefe`. Cada nodo se desbloquea al completar el anterior;
un encuentro completado no se puede repetir.

| Encuentro | Nombre | Enemigos | XP | Coronas |
|---|---|---|---|---|
| P1 | Pradera 1: Dos goblins | 2 | 50 | 25 |
| P2 | Pradera 2: Goblin y arquero | 2 | 50 | 25 |
| P3 | Pradera 3: Goblins y arquero | 3 | 50 | 25 |
| P4 | Pradera 4: Bruto, goblin y arquero | 3 | 50 | 25 |
| B1 | Bosque 1: Dos lobos | 2 | 100 | 40 |
| Evento | *La máscara rota* (Santuario Velado) | — | — | — |
| B2 | Bosque 2: Bandido, lobo y chamán | 3 | 100 | 40 |
| B3 | Bosque 3: Guardián, bandido y chamán | 3 | 100 | 40 |
| C1 | Corrupción 1: Errante, acechador y hechicero | 3 | 150 | 60 |
| C2 | Corrupción 2: Guardián, errante y hechicero | 3 | 150 | 60 |
| BOSS | Capitán de las Fuerzas Expedicionarias | 1 | 200 | 100 |

Recompensas: la XP se entrega completa a **cada** protagonista (aunque haya caído) al ganar; las Coronas solo la primera vez
que se completa un encuentro. Tras ganar, los protagonistas vivos recuperan el **40 %** de su vida máxima y los caídos reviven con ese 40 %
(`TurnManager.postBattleRecoveryPercent`). Una campaña completa suma 50 + 100 + 120 + 120 = 390 Coronas hasta C2, y 100 más con el jefe.

## 4. Sistema de turnos (`TurnManager`)
- Cada **ronda** se ordena a las unidades vivas por **Velocidad** de mayor a menor; cada unidad actúa una vez por ronda.
- Al empezar el turno propio: avanzan los *cooldowns*, se aplica el daño periódico (sangrado, veneno, quemadura), la regeneración,
  se resuelve una habilidad preparada pendiente, y una unidad aturdida pierde su acción.
- Los enemigos deciden con `EnemyAIController` (IA táctica v0.2 seleccionada en la escena mediante `EnemyAISettings`;
  existe también la v0.1). Una acción por turno enemigo.
- Fin del combate: cuando un equipo no tiene unidades vivas.

**Daño** (`DamageCalculator`): `daño = máx(1, ATQ + poder de la habilidad − DEF efectiva)`; luego puede ser crítico (según la probabilidad de la unidad).
El *poder* puede ser negativo. Las marcas, reducciones y guardias se aplican al recibir el daño.

## 5. Movimiento táctico (`PositionManager`)
- Cada equipo tiene 4 posiciones (P1 al frente… P4 al fondo; E1–E4 para enemigos). Los huecos se compactan hacia el frente cuando alguien cae.
- *Mover* → *Avanzar* o *Retroceder* una posición (se confirma con una segunda pulsación). Si otro aliado ocupa el destino, **intercambian** lugar.
- En la prueba hecha desde el HUD, mover consumió el turno de la unidad.
- Algunas habilidades también mueven (p. ej. *Tiro Evasivo* de Elaina: dispara y retrocede una posición).
- Un aliado revivido recupera su lugar en la formación si sigue libre.

## 6. Habilidades y consumibles
**Habilidades** (`AbilityData`, `AbilitySystem`): cada personaje tiene hasta 4. Tipos: *Ataque*, *Curación*, *Apoyo*; objetivos:
*individual*, *todos los aliados*, *todos los enemigos* o *uno mismo*. Cada una tiene *cooldown* (en turnos propios) y puede aplicar estados:
sangrado, marca, regeneración, aturdimiento, quemadura, debilitamiento, vulnerabilidad, veneno, mejora de defensa, guardia y, solo para el Capitán, objetivo prioritario.
Algunas son **preparadas**: se anuncian y se resuelven al comienzo del siguiente turno propio (se cancelan si la unidad cae o queda aturdida).

| Personaje | Habilidades (assets en `ScriptableObjects/Abilities/Characters`) |
|---|---|
| Valerian | Corte Preciso · Golpe Bajo (debilita ATQ) · Guardia de Trinchera (reduce daño y protege) · Herida Profunda (sangrado) |
| Elaina | Disparo Certero · Marca del Cazador · Tiro Evasivo (retrocede) · Flecha Perforante (ignora defensa) |
| Evanora | Orbe Arcano · Llamarada (quemadura) · Sello de Ruptura (reduce defensa) · Golpe de Meteorito (área, preparado) |
| Noelle enmascarada | Golpe de Lucero · Impacto Quebrantador (puede aturdir) · Resistencia Obstinada · Auxilio Improvisado (cura 12) |
| Noelle liberada | Impacto Consagrado · Plegaria Focalizada (cura 30 %) · Amparo Divino (defensa a todos) · Bendición Continua (regenera 4/turno a todos) |

*(La descripción del asset de Golpe de Meteorito aún dice "PENDIENTE", pero el asset ya tiene `requiresPreparation` activado: es texto desactualizado.)*

**Consumibles** (`ConsumableService`): *Poción* (35 Coronas, cura 30 % de la vida máxima de un aliado vivo, no revive) y *Antídoto*
(20 Coronas, elimina sangrado, veneno y quemadura). Máximo 5 de cada uno. Se compran en la **Tienda** del mapa; se usan en combate
(gastan el turno) o desde el **Inventario** del mapa. No se pueden comprar durante el combate.

## 7. Transformación de Noelle
- Noelle comienza **enmascarada** con su primer conjunto de habilidades.
- Tras completar B1 aparece el evento *La máscara rota* en el Santuario Velado. Al confirmarlo (`GameSession.TryLiberateNoelle`) Noelle queda
  **liberada** para el resto de la partida y se desbloquea B2. La etapa se aplica al cargar el siguiente combate
  (`CombatSessionBinder` → `CharacterAbilityLoadout.SetTransformed`).
- Su conjunto de habilidades cambia por completo. Regla de *cooldowns*: cada habilidad nueva hereda el *cooldown* pendiente de la que ocupaba su ranura,
  para que transformarse no permita reutilizar habilidades de inmediato.
- Solo ocurre una vez por partida. El texto del evento es **provisional** (editable en el Inspector de `WorldMapController`).

## 8. Encuentro contra el Capitán
Se desbloquea al completar C2 y se inicia desde **Trono Sellado**. Sin escena narrativa propia por ahora.

| Dato | Valor (assets en `ScriptableObjects/Enemies/Jefe`) |
|---|---|
| Vida / ATQ / DEF / VEL / crítico | 450 / 28 / 10 / 13 / 8 % |
| Recompensa | 200 XP · 100 Coronas |
| Fases | **Comandante calculador** (100 %) y **Última resistencia** (a partir del 50 % de vida) |
| Habilidades | Sablazo de Campaña · Interrogatorio Implacable (debilita ATQ) · Disciplina de Hierro (+DEF propia) · Objetivo Prioritario (marca: +20 % de daño del Capitán al objetivo) · Fuego de Supresión (área por posiciones, **preparado**: se anuncia y golpea en su siguiente turno) · Última Orden (área, solo fase 2, debilita) |

Notas: el Capitán es inmune a aturdimiento (`isBoss`). Su IA usa un perfil propio (`EnemyAIController.Boss.cs`) y cambia de comportamiento
en la fase 2. El HUD muestra la etiqueta `FASE2`, un rótulo temporal y anuncios de sus habilidades. Su sprite es un marcador de posición.

## 9. Qué está documentado como probado (y qué no)
**Evidencia en el repositorio:**
- Balance del jefe con un *bot* de jugador automático (perfiles competente / promedio / aprendiendo) sobre la herramienta de editor
  `Assets/Editor/BossBalanceHarness.cs`: `Docs/Balance_Capitan_Paso72_Resultados.txt`, `…Paso73…`, `…Paso74_ANTES/DESPUES.txt`, `…Paso75…`.
- Diseño del jefe: `Docs/Paso66_Arquitectura_Jefe_Final.md`. Diseño narrativo propuesto (no implementado): `Docs/Paso77_Diseno_Narrativo_Jefe.md`.
- Organización de escenas y herramientas: `Docs/Paso78_Organizacion_Escenas.md` y capturas en `Docs/Capturas/`.

**Aclaraciones obligatorias para la exposición:**
- Las pruebas de flujo (menú → mapa → combate → jefe, evento de Noelle, derrota, nueva partida) se ejecutaron con **comandos de editor**
  (llamadas a los métodos que invocan los botones), no con una persona jugando; en varias se marcaron encuentros como completados por código
  para llegar al evento o al jefe.
- **No hay registro** en `Docs/` de pruebas con jugadores humanos, de rendimiento, de compilaciones (builds) ni de otros equipos.
- Escena narrativa del jefe, final de la demo y arte definitivo del Capitán: **no existen todavía**.
