# Paso 78 — Organización de escenas en Unity

Estado inicial registrado en `Docs/Paso78_Estado_Inicial_Escenas.txt` y `Docs/Paso78_Jerarquias_Mapa_Combate.txt`
(antes de cualquier cambio). Respaldo de las escenas originales: copia local fuera del repositorio.

## Herramientas de editor (`Assets/Editor/SceneTools`, solo editor, sin ExecuteAlways)

| Menú | Qué hace |
|---|---|
| Reencarnación > Escenas > Encuadrar escena actual | Pone la Scene View en 2D sobre el área útil (Menú/Mapa: lienzo 1920×1080; Combate: vista de la cámara). |
| Reencarnación > Escenas > Encuadre automático al abrir | Lo anterior al abrir cada escena (activo por defecto, preferencia por usuario). |
| Reencarnación > Escenas > Ocultar rellenos de aspecto del mapa | Oculta en Scene View `MundoBordes` (no modifica la escena). |
| Reencarnación > Escenas > Guías de Scene View | Dibuja el marco 16:9 de referencia, huecos P1-P4/E1-E4 y paneles del HUD. No se guarda. |
| Reencarnación > Combate > Vista previa del HUD (solo editor) | Ventana que renderiza el HUD ya guardado a 1920×1080 con guías, fichas de unidad y textos de muestra. Botón "Exportar PNG". |
| Reencarnación > Game View > 1920×1080 / 1280×720 | Cambia la resolución de Game View (para comprobar escalado). |

La vista previa no crea sesión, no toca GameSession/estadísticas/inventario, no marca la escena como modificada y
no deja objetos: se limpia al cerrar la ventana. No funciona en Play Mode.

## Cambios en escenas

- `CombateIntegracion.unity`: raíz agrupada en `00_Cameras`, `10_BattleField` (BattlePositions, Party,
  Enemies_Placeholder, Enemies_Runtime), `20_UI`, `30_CombatSystems`, `90_EditorHelpers`. Todos los grupos están en el
  origen con transform identidad: posiciones y escalas no cambian. `EncounterLoader.spawnParent` apunta a
  `Enemies_Runtime` (los enemigos generados ya no ensucian la raíz). Cámara: fondo en color sólido.
- `Map.unity`: solo la cámara pasa de Skybox a color sólido (igual que Menú y Combate). Jerarquía intacta porque
  `MapAmbientController` localiza `WorldMapCanvas/MapArea` por ruta.
- `Menu.unity` e `IntroCinematic.unity`: sin cambios.
