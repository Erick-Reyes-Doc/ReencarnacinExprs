# Mapa del mundo ilustrado — especificación de arte e integración

Estado: **propuesta pendiente de aprobación**. Nada de esto está implementado todavía;
el mapa funcional actual (Paso 61) no se toca hasta que se apruebe.
Este documento está pensado para entregárselo tal cual a quien ilustre el mundo.

Requisitos de dirección artística (resumen):
ilustración digital 2D de fantasía medieval estilizada, nivel de detalle intermedio,
iluminación pintada y siluetas reconocibles. No pixel art, no fotorrealismo, no 3D.
Arte **original** (no copiar ilustraciones de referencias externas).

---

## 1. Composición del mundo

Un único territorio en formato 16:9 (el encuadre jugable es 1920×1080; se entrega a 2×).
Los límites de región surgen de la geografía, no de franjas.

```
 norte  ┌──────────────────────────────────────────────┐
        │              ▲▲  ▲▲▲   ▲▲▲  ▲▲▲  (cordillera)  │  y 0.05–0.28
        │                                  ·Lugar bloq.│
        │   Ruinas·            ·Santuario     (cráter)  │
        │                         ·Sendero                │
        │ ·Campamento   ·Claro     sombrío  ·Tierras    │
        │   PRADERA        BOSQUE            marchitas  │
 sur    └──────────────────────────────────────────────┘
          x 0.00 ───────────── 0.45 ─────────── 1.00
```

- **Pradera (izquierda, x 0.00–0.40):** colinas onduladas, campos, árboles sueltos, senderos claros,
  pequeñas ruinas. Luz cálida y abierta. Sensación de inicio.
- **Bosque (centro, x 0.35–0.68):** arboleda densa que se cierra, caminos que se internan, sombra y
  niebla ligera. Cada vez más oscuro hacia la derecha. Contiene el lugar del evento de Noelle.
- **Corrupción (derecha, x 0.65–1.00):** vegetación muerta, terreno deformado y agrietado, rocas oscuras,
  resplandor corrupto morado. Hostil. Al fondo-arriba, un **destino misterioso bloqueado** (jefe) sin
  identidad definida (cráter, puerta o formación; decisión del ilustrador).
- **Norte:** cordillera continua que cierra el mapa y separa Bosque y Corrupción del borde superior.
- **Sendero principal:** un camino de tierra que atraviesa el mapa de izquierda a derecha,
  pasa por los lugares en orden y entra en cada región por un paso natural (valle, claro, desfiladero).
- **Bordes laterales:** deben continuar el paisaje (verde a la izquierda, morado oscuro a la derecha),
  porque en pantallas más anchas que 16:9 se prolonga con ese color.
- **Zona libre inferior:** el panel de lugar puede flotar abajo; evitar detalles críticos en el 12 % inferior.

### Lugares seleccionables (7) y combates que contienen

Posición: coordenadas normalizadas (0–1) con origen **arriba-izquierda**; son guía, el ilustrador
puede moverlas si lo pide la geografía (entonces entrega las coordenadas finales, ver §5).

| Id | Nombre provisional | x | y | Contiene | Elemento ambiental sugerido |
|---|---|---|---|---|---|
| `camp` | Campamento | 0.11 | 0.63 | P1, P2 | campamento junto al sendero |
| `ruins` | Ruinas | 0.23 | 0.45 | P3, P4 | ruinas de piedra cubiertas de musgo |
| `clearing` | Claro del bosque | 0.39 | 0.60 | B1 | claro con luz entre los árboles |
| `shrine` | Santuario | 0.48 | 0.43 | evento *La máscara rota* | altar de piedra con una máscara quebrada |
| `trail` | Sendero sombrío | 0.59 | 0.53 | B2, B3 | camino estrecho entre troncos y niebla |
| `wastes` | Tierras marchitas | 0.78 | 0.61 | C1, C2 | terreno agrietado con obelisco/rocas corruptas |
| `gate` | Lugar bloqueado | 0.91 | 0.33 | jefe (pendiente) | formación misteriosa, sin definir |

Los nombres son placeholders; no son nombres narrativos definitivos.

---

## 2. Lista exacta de imágenes

Formato: **PNG 8 bits RGBA**, sRGB. Carpeta destino: `Assets/Art/Map/` con las subcarpetas indicadas.
"Cuadro completo" = mismo encuadre 16:9 para que todas las capas encajen sin posicionarse.

### 2.1 Mundo (orden de capas: de atrás hacia delante)

| Orden | Archivo | Tamaño | Alfa | Contenido |
|---|---|---|---|---|
| 0 | `World/W00_Base.png` | 3840×2160 | no | terreno completo: relieve, ríos, praderas, bosque, tierra corrupta, sendero ya pintado |
| 1 | `World/W10_Paths.png` | 3840×2160 | sí | senderos de tierra (opcional si ya están en la base; permite iluminar el recorrido) |
| 2 | `Locations/Loc_<id>_Landmark.png` ×7 | ver tabla | sí | elemento ambiental de cada lugar, recortado, sin fondo |
| 3 | `World/W30_CanopyFront.png` | 3840×2160 | sí | copas y troncos que pasan **por delante** de caminos y lugares (profundidad) |
| 4 | `World/W31_MountainsFront.png` | 3840×2160 | sí | rocas/crestas de primer plano en los bordes (opcional) |
| 5 | `Fog/F10_Lock_Forest.png` | 3840×2160 | sí | niebla densa que cubre el Bosque mientras esté bloqueado |
| 5 | `Fog/F11_Lock_Corruption.png` | 3840×2160 | sí | niebla/bruma que cubre la Corrupción mientras esté bloqueada |
| 6 | `Atmosphere/A10_FogLow.png` | 3840×2160 | sí | niebla baja ligera (Bosque) |
| 6 | `Atmosphere/A20_LightRays.png` | 3840×2160 | sí | luz cálida suave (Pradera) |
| 6 | `Atmosphere/A30_CorruptionGlow.png` | 3840×2160 | sí | resplandor morado (pensado para mezcla aditiva) |
| 7 | `Atmosphere/A90_Vignette.png` | 1920×1080 | sí | viñeta suave en bordes |

Tamaños de `Loc_<id>_Landmark.png` (alfa, recorte limpio, base apoyada en el suelo):
`camp` 512×384 · `ruins` 512×384 · `clearing` 384×384 · `shrine` 512×512 ·
`trail` 384×384 · `wastes` 512×512 · `gate` 640×640.

### 2.2 Marcadores (64–96 px visibles dentro de un lienzo de 128×128, con alfa)

| Archivo | Uso |
|---|---|
| `Markers/M_Available.png` | lugar con combate disponible |
| `Markers/M_Recommended.png` | siguiente recomendado (más llamativo) |
| `Markers/M_Completed.png` | todos los combates del lugar superados |
| `Markers/M_Locked.png` | bloqueado |
| `Markers/M_Event.png` | evento narrativo |
| `Markers/M_Boss.png` | destino del jefe (pendiente) |
| `Markers/M_Hover.png` | brillo al pasar el cursor (160×160) |

El estado no debe depender solo del color: cada marcador lleva una forma o icono distinto.

### 2.3 Interfaz

| Archivo | Tamaño | Notas |
|---|---|---|
| `UI/UI_Plate_9.png` | 128×128 | placa 9-slice (barra superior, etiquetas), borde 32 |
| `UI/UI_Panel_9.png` | 256×256 | panel de lugar y modales, borde 40 |
| `UI/UI_Chip_9.png` | 96×64 | chips de Coronas/Pociones/Antídotos, borde 24 |
| `UI/UI_Button_Normal_9.png`, `_Hover_9`, `_Pressed_9`, `_Disabled_9` | 128×64 | 4 estados, borde 24 |
| `UI/UI_Divider.png` | 512×16 | separador horizontal |
| `Icons/Ic_Coin`, `Ic_Potion`, `Ic_Antidote`, `Ic_Lock`, `Ic_Check`, `Ic_Star`, `Ic_Mask`, `Ic_Skull`, `Ic_Close` `.png` | 96×96 | iconos con alfa |

### 2.4 Entrega por fases (mínimo viable)

1. **Fase A:** `W00_Base` + los 7 marcadores + `UI_Panel_9` + `UI_Plate_9`. Con esto ya se ve el mapa nuevo.
2. **Fase B:** landmarks, `W30_CanopyFront`, `F10/F11`.
3. **Fase C:** atmósfera, `W10_Paths`, `W31`, botones, iconos.

### 2.5 Importación en Unity (se aplica por preset)

Sprite (2D and UI) · Sprite Mode Single · Mesh Type **Full Rect** · Pixels Per Unit 100 · sin mipmaps ·
Filter Bilinear · Wrap Clamp · Alpha Is Transparency activado · Max Size 4096 ·
compresión de alta calidad (BC7/ASTC). Los 9-slice llevan su borde (columna "borde" arriba).
Memoria: un 3840×2160 sin comprimir ocupa ~33 MB; comprimido ~8 MB. Se admiten como mucho
6 capas a cuadro completo.

---

## 3. Organización en Canvas y referencias de sprites

Jerarquía propuesta bajo `WorldMapCanvas` (Screen Space Overlay, Scale With Screen Size 1920×1080, Expand):

```
WorldMapCanvas
├─ FondoGeneral                    color plano (relleno de bordes en pantallas anchas)
├─ MapArea (1920×1080, centrado)
│  ├─ Mundo/MundoBase              W00_Base            (estirado a MapArea)
│  ├─ MundoCapas/Camino            W10_Paths
│  ├─ Lugares/                     NUEVO
│  │   ├─ Lugar_camp   ├ Landmark  ├ Marcador (botón)  ├ Hover  ├ Etiqueta discreta
│  │   ├─ Lugar_ruins  ... (7 lugares)
│  ├─ MundoDelante/Copas           W30_CanopyFront
│  ├─ MundoDelante/MontanasFrente  W31_MountainsFront
│  ├─ NieblaBloqueo/Bosque         F10  (alfa según zona desbloqueada)
│  ├─ NieblaBloqueo/Corrupcion     F11
│  └─ MundoAtmosfera/…             A10, A20, A30, A90
├─ BarraSuperior (fina, ~56 px)    Coronas · Pociones · Antídotos · progreso · Tienda · Inventario · Menú
├─ PanelLugar                      NUEVO: panel flotante junto al marcador seleccionado
│     título del lugar · filas de combates (estado, XP/Coronas, enemigos) · [Iniciar] por fila
├─ EtiquetaHover                   nombre del lugar al pasar el cursor
└─ EventoVelo / PanelEvento        modal del evento (sin cambios de lógica)
```

Referencias de sprites (todas por `MapArtSet`, ya existente):

| Campo | Archivo |
|---|---|
| `worldBase` (ya existe) | `W00_Base` |
| `worldLayers[]` (ya existe) | `W10_Paths`, `W30_CanopyFront`, `W31_MountainsFront` (se añadirá posición "antes/después de lugares") |
| `atmosphereLayers[]` (ya existe) | `A10`, `A20`, `A30`, `A90` |
| *nuevos* `fogLockForest`, `fogLockCorruption` | `F10`, `F11` |
| *nuevos* `locations[]` | por lugar: id, `landmark`, posición normalizada, tamaño |
| *nuevos* `marker*` (7) | `M_*` |
| *nuevos* `plate`, `panel`, `chip`, `button*` | `UI_*` |

Datos de los lugares: una lista editable en el Inspector (`id`, nombre, posición 0–1, tamaño del
landmark, ids de encuentros que contiene). Mover un lugar = cambiar su posición, sin tocar código.

### Cómo se selecciona (lógica reutilizada, no reescrita)

1. El jugador pulsa un marcador → se abre `PanelLugar` con los combates de ese lugar.
2. Cada fila usa `WorldMapController.Select(encounterId)` y `StartSelected()` ya existentes.
3. Disponible / bloqueado / completado / recomendado salen de `MapProgression` (sin cambios).
4. El evento *La máscara rota* sigue usando `OpenEvent`/`ConfirmEvent` desde su lugar `shrine`.
5. Las regiones bloqueadas se ocultan con la niebla (alfa según `MapProgression.IsZoneUnlocked`).
6. F9, tienda, inventario, retorno desde combate y confirmación de salida: sin cambios.

---

## 4. Cómo sustituir recursos provisionales sin tocar la lógica

- Todo el arte pasa por `Assets/Art/Map/…` y se referencia desde `MapArtSet` (un solo asset).
- **Mismo nombre y mismo `.meta`:** sobrescribir un PNG conserva la referencia; no hay que reasignar nada.
- Con otra resolución pero el mismo encuadre 16:9, las capas de cuadro completo siguen encajando
  (se estiran a `MapArea`).
- Tras aprobar el plan se añadirá una herramienta de editor **"Map > Importar arte"** que:
  escanea `Assets/Art/Map/`, aplica los ajustes de importación, asigna cada sprite a su campo por
  convención de nombres y reconstruye la interfaz del mapa.
- Si falta un archivo, ese elemento usa el recurso provisional actual (el mapa nunca queda roto).
- El terreno geométrico actual solo se usa mientras `worldBase` esté vacío.

---

## 5. Archivos que necesito que me entregues

1. Los PNG de la lista (empezar por la **Fase A**), con los nombres exactos y en sus carpetas.
2. **Coordenadas finales de los 7 lugares** si cambian respecto a §1: una imagen guía con marcadores
   numerados o un `locations.json` (`id`, x, y en 0–1, origen arriba-izquierda).
3. Paleta de colores (hex) por región y de la interfaz.
4. Confirmación de que el arte es **original** o su licencia, y créditos si los hay.
5. (Opcional) PSD/Krita por capas para posteriores ajustes.
6. (Opcional) tipografía si no se usará la actual.
7. Aprobación de los nombres provisionales de los lugares o los nombres definitivos.

---

## 6. Qué haré tras la aprobación (y no antes)

1. Lista de lugares + marcadores pequeños + etiqueta hover.
2. `PanelLugar` contextual en lugar del panel fijo inferior.
3. Niebla de bloqueo por zona y capas "antes/después" de los lugares.
4. Herramienta de importación por nombres.
5. Pruebas: selección, desbloqueos, tienda/inventario, evento, F9, retorno desde combate y capturas
   en 1920×1080, 1280×720, 1920×1200 y 2560×1080.
