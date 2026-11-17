using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PositionManager : MonoBehaviour
{
    [Header("Equipo del jugador")]
    [SerializeField]
    private List<BattleUnit> playerUnits =
        new List<BattleUnit>();

    [Header("Equipo enemigo")]
    [SerializeField]
    private List<BattleUnit> enemyUnits =
        new List<BattleUnit>();

    [Header("Puntos visuales del jugador")]
    [SerializeField]
    private Transform[] playerPositions =
        new Transform[4];

    [Header("Puntos visuales del enemigo")]
    [SerializeField]
    private Transform[] enemyPositions =
        new Transform[4];

    [Header("Etiquetas de casilla (P1-P4)")]
    [SerializeField] private bool showSlotLabels = true;
    [SerializeField] private TMP_FontAsset slotLabelFont;
    [SerializeField] private float slotLabelFontSize = 2.8f;   // campo renombrado (antes slotLabelSize = 4) para que el nuevo tamano se aplique solo
    // true = las etiquetas van ENCIMA de los personajes; false = debajo de las barras de vida (comportamiento anterior).
    [SerializeField] private bool slotLabelsOnTop = true;
    // Arriba: separacion sobre la cabeza mas alta de todas las unidades (sombreros incluidos).
    [SerializeField] private float slotLabelTopMargin = 0.25f;
    // Arriba: limite superior en pantalla (0 = borde inferior, 1 = borde superior). El borde de arriba de la etiqueta nunca
    // pasa de aqui, para no quedar tapada por el HUD superior. Bajalo si aun se mete bajo la barra; 1 = sin limite.
    [SerializeField, Range(0f, 1f)] private float slotLabelScreenTop = 0.835f;   // renombrado (antes slotLabelTopLimit = 0.86) para que el nuevo valor se aplique solo
    // Arriba: altura sobre la casilla si aun no hay sprites que medir (p. ej. el encuentro todavia no cargo las unidades).
    [SerializeField] private float slotLabelTopFallback = 5.5f;
    // Solo modo abajo: Y mas negativa = mas abajo. La X se usa en ambos modos.
    [SerializeField] private Vector3 slotLabelOffset = new Vector3(0f, -2.0f, 0f);
    // Campo renombrado (antes slotLabelColor) para que el nuevo color claro se aplique sin tocar el Inspector.
    [SerializeField] private Color slotLabelTextColor = new Color(1f, 0.95f, 0.80f, 1f);
    // true = todas las etiquetas (heroes y enemigos) van en la misma fila.
    // Arriba: la fila de la cabeza mas alta. Abajo: la del punto mas bajo de ambos bandos.
    [SerializeField] private bool slotLabelsOneRow = true;

    [Header("Fondo de las etiquetas")]
    [SerializeField] private bool slotLabelBackground = true;
    [SerializeField] private Color slotLabelBackgroundColor = new Color(0f, 0f, 0f, 0.55f);
    // Margen interior (unidades de mundo) entre el texto y el borde del fondo.
    [SerializeField] private Vector2 slotLabelBgPadding = new Vector2(0.22f, 0.05f);
    // Radio de las esquinas redondeadas (unidades de mundo). 0 = esquinas rectas.
    [SerializeField] private float slotLabelCornerRadius = 0.14f;

    private static Texture2D labelBgTexture;
    private static Sprite labelBgSprite;
    private static float labelBgSpriteRadius = -1f;
    private bool slotLabelsBuilt;

    [Header("Orden de dibujo de las etiquetas")]
    // true = copia la Sorting Layer de las unidades y se dibuja siempre DETRAS de ellas (recomendado).
    [SerializeField] private bool slotLabelsBehindUnits = true;
    // true = con las etiquetas arriba, se dibujan DELANTE de los personajes (si no, las cabezas altas las tapan).
    [SerializeField] private bool slotLabelsInFrontWhenOnTop = true;
    // Cuantos niveles por debajo del Order in Layer de las unidades (solo si slotLabelsBehindUnits).
    [SerializeField] private int slotLabelOrderBelowUnits = 1;
    // Valores manuales (solo si slotLabelsBehindUnits esta desactivado, o si aun no hay unidades que copiar).
    [SerializeField] private string slotLabelSortingLayer = "Default";
    [SerializeField] private int slotLabelSorting = -1;

    // Etiquetas P1-P4 del bando enemigo: solo se muestran las casillas ocupadas (ver RefreshEnemySlotLabels).
    [SerializeField] private bool enemyLabelsOnlyOccupied = true;
    private readonly TextMeshPro[] enemyLabels = new TextMeshPro[4];

    private void OnDestroy()
    {
        SubscribeToTeam(playerUnits, false);
        SubscribeToTeam(enemyUnits, false);
    }

    // Suscribe (o cancela) la gestion de derrotas.
    // Quitar antes de agregar evita suscripciones duplicadas.
    private void SubscribeToTeam(
        List<BattleUnit> team,
        bool subscribe
    )
    {
        foreach (BattleUnit unit in team)
        {
            if (unit == null)
                continue;

            unit.OnFled -= HandleFledUnit;
            unit.OnDefeated -= HandleDefeatedUnit;
            unit.OnRevived -= HandleRevivedUnit;

            if (subscribe)
            {
                unit.OnDefeated += HandleDefeatedUnit;
                unit.OnRevived += HandleRevivedUnit;
                unit.OnFled += HandleFledUnit;
            }
        }
    }

    // Reincorpora a una unidad revivida a su formacion: vuelve a
    // mostrarse, recupera su posicion si sigue libre (o la primera
    // libre) y el equipo se reorganiza.
    private void HandleRevivedUnit(BattleUnit unit)
    {
        if (unit == null || !unit.IsAlive)
            return;

        UnitDeathEffect.Cancel(unit);   // si aun se estaba oscureciendo, detiene el efecto y restaura el color
        bool wasDowned = downedHeroes.Remove(unit);
        UnitDownedVisual.Stop(unit);     // heroe caido que se levanta: sale del suelo

        if (wasDowned)
            CombatFeedback.Show(unit, "¡De pie!", FeedbackKind.Buff);

        SpriteRenderer sprite = unit.GetComponent<SpriteRenderer>();

        if (sprite != null)
            sprite.enabled = true;

        List<BattleUnit> team =
            unit.Side == TeamSide.Player
            ? playerUnits
            : enemyUnits;

        if (!team.Contains(unit))
            team.Add(unit);

        // Recupera su lugar original en la formacion (la posicion que
        // tenia al caer); los aliados vivos a partir de ahi retroceden una
        // posicion. Si el hueco no existe, se coloca tras el ultimo vivo.
        int aliveOthers = 0;

        foreach (BattleUnit other in team)
        {
            if (other != null && other != unit && other.IsAlive)
                aliveOthers++;
        }

        int desired = unit.Position;

        if (defeatedAt.TryGetValue(unit, out int slot))
        {
            defeatedAt.Remove(unit);

            // Posicion compactada = lugar original menos los huecos
            // anteriores que siguen ocupados por caidos.
            desired = slot - DeadSlotsBefore(unit.Side, slot);
        }

        desired = Mathf.Clamp(desired, 1, Mathf.Min(4, aliveOthers + 1));

        foreach (BattleUnit other in team)
        {
            if (other != null && other != unit && other.IsAlive &&
                other.Position >= desired)
                other.SetPosition(Mathf.Min(4, other.Position + 1));
        }

        unit.SetPosition(desired);
        ReorganizeTeam(unit.Side);
    }

    // Casilla de la formacion completa (1-4, contando a los caidos) que
    // ocupaba cada unidad derrotada.
    private readonly Dictionary<BattleUnit, int> defeatedAt =
        new Dictionary<BattleUnit, int>();

    // Heroes caidos (vida 0) en orden de caida. No estan en la formacion activa (no tienen turno ni son objetivo), pero siguen
    // en el campo tumbados y pueden levantarse con una curacion. Se muestran en las casillas libres tras los heroes en pie.
    private readonly List<BattleUnit> downedHeroes = new List<BattleUnit>();

    public bool IsDowned(BattleUnit unit)
    {
        return unit != null && downedHeroes.Contains(unit);
    }

    public List<BattleUnit> GetDownedHeroes()
    {
        downedHeroes.RemoveAll(u => u == null || u.IsAlive);
        return new List<BattleUnit>(downedHeroes);
    }

    // Heroe caido que ocupa la casilla visual (1-4): el que cayo en ella (no se mueve de su lugar).
    public BattleUnit GetDownedAtPosition(TeamSide side, int position)
    {
        if (side != TeamSide.Player || downedHeroes.Count == 0)
            return null;

        foreach (BattleUnit unit in downedHeroes)
        {
            if (unit != null && !unit.IsAlive && defeatedAt.TryGetValue(unit, out int slot) && slot == position)
                return unit;
        }

        return null;
    }

    // Unidad (viva o heroe caido) de la casilla VISUAL 1-4 de un bando. Los heroes saltan las casillas de sus caidos, asi que la
    // posicion logica de un vivo es su casilla menos los caidos anteriores. Los enemigos se compactan como siempre.
    public BattleUnit GetUnitAtVisualSlot(TeamSide side, int slot)
    {
        if (side != TeamSide.Player)
            return GetUnitAtPosition(side, slot);

        BattleUnit downed = GetDownedAtPosition(side, slot);

        if (downed != null)
            return downed;

        if (IsDeadSlot(side, slot))
            return null;

        BattleUnit alive = GetUnitAtPosition(side, slot - DeadSlotsBefore(side, slot));
        return alive != null && alive.IsAlive ? alive : null;
    }

    private static int AliveCount(List<BattleUnit> team)
    {
        int count = 0;

        foreach (BattleUnit unit in team)
            if (unit != null && unit.IsAlive)
                count++;

        return count;
    }

    // Punto de mundo donde "vive" cada unidad (su casilla). Las barras de vida y otros indicadores se anclan aqui y no a donde este
    // el sprite en cada instante (embestidas, retrocesos, animaciones con otro ancho o una pose de caida).
    private readonly Dictionary<BattleUnit, Vector3> homePos = new Dictionary<BattleUnit, Vector3>();

    public bool TryGetHome(BattleUnit unit, out Vector3 home)
    {
        home = default;
        return unit != null && homePos.TryGetValue(unit, out home);
    }

    // Regla: un heroe caido NO se mueve de su lugar. Se queda en la casilla de la formacion completa donde cayo (la guardada en
    // defeatedAt), con su pose de derrota, y los demas heroes no ocupan esa casilla (ver UpdateTeamVisualPositions).
    private void PlaceDownedUnits()
    {
        int alive = AliveCount(playerUnits);

        for (int i = 0; i < downedHeroes.Count; i++)
        {
            BattleUnit unit = downedHeroes[i];

            if (unit == null)
                continue;

            // Casilla (1-4) donde cayo; si faltara el dato, la primera libre de la retaguardia como antes.
            int slot = defeatedAt.TryGetValue(unit, out int fallen) ? fallen : alive + i + 1;

            if (slot < 1 || slot > 4 || slot > playerPositions.Length || playerPositions[slot - 1] == null)
                continue;

            unit.SetPosition(slot);
            UnitDownedVisual.PlaceAt(unit, playerPositions[slot - 1].position);
            homePos[unit] = playerPositions[slot - 1].position;
        }
    }

    // Casilla visual (1-4) de una unidad viva con posicion logica 'position': la n-esima casilla que no ocupa un caido de su
    // bando. Asi las reglas siguen usando posiciones compactadas (P1-P4) pero nadie se coloca sobre un caido ni se desplaza.
    // Heroes vivos alcanzables por posiciones permitidas (habilidad o area enemiga). Regla: primero la CASILLA REAL (los caidos no
    // se mueven, asi que la retaguardia sigue protegida); si ningun heroe vivo queda en esas casillas se usa la posicion
    // compactada como respaldo, para que el combate nunca se quede sin objetivos validos.
    public List<BattleUnit> GetPlayerUnitsAtPositions(System.Func<int, bool> allows)
    {
        List<BattleUnit> result = new List<BattleUnit>();

        for (int slot = 1; slot <= 4; slot++)
        {
            if (!allows(slot))
                continue;

            BattleUnit unit = GetUnitAtVisualSlot(TeamSide.Player, slot);

            if (unit != null && unit.IsAlive && !result.Contains(unit))
                result.Add(unit);
        }

        if (result.Count > 0)
            return result;

        for (int position = 1; position <= 4; position++)
        {
            if (!allows(position))
                continue;

            BattleUnit unit = GetUnitAtPosition(TeamSide.Player, position);

            if (unit != null && unit.IsAlive && !result.Contains(unit))
                result.Add(unit);
        }

        return result;
    }

    // true si 'unit' (heroe vivo) esta dentro de las posiciones permitidas segun la regla anterior.
    public bool IsPlayerUnitInPositions(BattleUnit unit, System.Func<int, bool> allows)
    {
        return unit != null && unit.IsAlive && GetPlayerUnitsAtPositions(allows).Contains(unit);
    }

    // Casilla de formacion que cuenta para usar habilidades: los heroes conservan su lugar aunque otros caigan (los caidos no
    // se mueven), asi que un heroe de retaguardia sigue pudiendo usar lo que usa desde ahi. Enemigos: su posicion de siempre.
    public int AbilitySlotOf(BattleUnit unit)
    {
        if (unit == null)
            return 1;

        return unit.Side == TeamSide.Player && unit.IsAlive ? VisualSlotOf(unit.Side, unit.Position) : unit.Position;
    }

    private int VisualSlotOf(TeamSide side, int position)
    {
        int free = 0;

        for (int slot = 1; slot <= 4; slot++)
        {
            if (IsDeadSlot(side, slot))
                continue;

            free++;

            if (free == position)
                return slot;
        }

        return position;
    }

    private bool IsDeadSlot(TeamSide side, int slot)
    {
        foreach (KeyValuePair<BattleUnit, int> entry in defeatedAt)
        {
            if (entry.Key != null && entry.Key.Side == side &&
                entry.Value == slot)
                return true;
        }

        return false;
    }

    private int DeadSlotsBefore(TeamSide side, int slot)
    {
        int count = 0;

        foreach (KeyValuePair<BattleUnit, int> entry in defeatedAt)
        {
            if (entry.Key != null && entry.Key.Side == side &&
                entry.Value < slot)
                count++;
        }

        return count;
    }

    // Casilla en la formacion completa de una unidad que acaba de caer:
    // la n-esima casilla no ocupada por otros caidos, siendo n su
    // posicion compactada actual.
    private int FullFormationSlot(BattleUnit unit)
    {
        int slot = 0;
        int free = 0;

        while (free < unit.Position && slot < 4)
        {
            slot++;

            if (!IsDeadSlot(unit.Side, slot))
                free++;
        }

        return Mathf.Max(1, slot);
    }

    private void Start()
    {
        SubscribeToTeam(playerUnits, true);
        SubscribeToTeam(enemyUnits, true);

        // Heroes que empiezan el combate caidos (vida 0 de un combate anterior): tumbados y fuera de la formacion activa.
        foreach (BattleUnit unit in new List<BattleUnit>(playerUnits))
            if (unit != null && unit.IsDowned)
                HandleHeroDowned(unit, true);

        // Validar las posiciones iniciales.
        ValidateTeam(playerUnits, TeamSide.Player);
        ValidateTeam(enemyUnits, TeamSide.Enemy);

        // Mostrar las formaciones en consola.
        ShowTeamPositions(playerUnits, "JUGADORES");
        ShowTeamPositions(enemyUnits, "ENEMIGOS");

        // Colocar los sprites en sus posiciones.
        UpdateVisualPositions();

        // Etiquetas P1-P4 sobre las casillas fijas.
        if (showSlotLabels)
            RebuildSlotLabels();
    }

    // Reconstruye todas las etiquetas (util tras cambiar valores en el Inspector con el juego en marcha).
    [ContextMenu("Reconstruir etiquetas")]
    public void RebuildSlotLabels()
    {
        float rowY;

        if (slotLabelsOnTop)
        {
            // Fila comun: justo encima de la cabeza mas alta (si no hay sprites, a una altura fija sobre las casillas).
            rowY = HighestUnitTop();

            if (rowY == float.MinValue)
            {
                rowY = ExtremeSlotY(true);

                if (rowY == float.MinValue)
                    return;

                rowY += slotLabelTopFallback;
            }
            else
            {
                rowY += slotLabelTopMargin;
            }
        }
        else
        {
            // Fila comun: la Y mas baja entre todas las casillas.
            rowY = ExtremeSlotY(false);

            if (rowY == float.MaxValue)
                return;

            rowY += slotLabelOffset.y;
        }

        BuildSlotLabels(playerPositions, rowY, null);
        BuildSlotLabels(enemyPositions, rowY, enemyLabels);
        RefreshEnemySlotLabels();
        slotLabelsBuilt = true;
    }

    // Y de mundo que corresponde a slotLabelScreenTop en la camara principal (sin camara o con 1 = sin limite).
    private float ViewportLimitWorldY(float worldZ)
    {
        Camera cam = Camera.main;

        if (cam == null || slotLabelScreenTop >= 1f)
            return float.MaxValue;

        float distance = Mathf.Abs(worldZ - cam.transform.position.z);
        return cam.ViewportToWorldPoint(new Vector3(0.5f, slotLabelScreenTop, distance)).y;
    }

    // highest = true: Y mas alta de las casillas (float.MinValue si no hay); false: la mas baja (float.MaxValue si no hay).
    private float ExtremeSlotY(bool highest)
    {
        float y = highest ? float.MinValue : float.MaxValue;

        foreach (Transform[] slots in new[] { playerPositions, enemyPositions })
        {
            foreach (Transform t in slots)
            {
                if (t == null)
                    continue;

                y = highest ? Mathf.Max(y, t.position.y) : Mathf.Min(y, t.position.y);
            }
        }

        return y;
    }

    // Borde superior del sprite visible mas alto de ambos bandos (float.MinValue si no hay ninguno).
    private float HighestUnitTop()
    {
        float top = float.MinValue;

        foreach (List<BattleUnit> team in new[] { playerUnits, enemyUnits })
        {
            foreach (BattleUnit unit in team)
            {
                if (unit == null)
                    continue;

                SpriteRenderer sprite = unit.GetComponent<SpriteRenderer>();

                if (sprite != null && sprite.enabled && sprite.sprite != null)
                    top = Mathf.Max(top, sprite.bounds.max.y);
            }
        }

        return top;
    }

    // Sprite blanco de esquinas redondeadas (9-slice) que se tine con slotLabelBackgroundColor.
    private Sprite GetLabelBackgroundSprite()
    {
        const int size = 32;
        const float radiusPx = 12f;

        float radius = Mathf.Max(0.01f, slotLabelCornerRadius);

        if (labelBgSprite != null && Mathf.Approximately(labelBgSpriteRadius, radius))
            return labelBgSprite;

        if (labelBgTexture == null)
        {
            labelBgTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            labelBgTexture.wrapMode = TextureWrapMode.Clamp;
            labelBgTexture.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float dx = Mathf.Max(0f, Mathf.Max(radiusPx - px, px - (size - radiusPx)));
                    float dy = Mathf.Max(0f, Mathf.Max(radiusPx - py, py - (size - radiusPx)));
                    float alpha = Mathf.Clamp01(radiusPx - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            labelBgTexture.SetPixels32(pixels);
            labelBgTexture.Apply();
        }

        // pixelsPerUnit hace que la esquina mida 'radius' unidades de mundo.
        labelBgSprite = Sprite.Create(
            labelBgTexture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            radiusPx / radius,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radiusPx, radiusPx, radiusPx, radiusPx));

        labelBgSpriteRadius = radius;
        return labelBgSprite;
    }

    // Contorno real de las letras visibles (espacio local de la etiqueta). textBounds incluye el hueco reservado para letras
    // altas y el relleno de la fuente, y deja mucho espacio muerto encima del texto.
    private static Bounds GetGlyphBounds(TextMeshPro label)
    {
        label.ForceMeshUpdate();
        TMP_TextInfo info = label.textInfo;

        bool any = false;
        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;

        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo ch = info.characterInfo[i];

            if (!ch.isVisible)
                continue;

            Vector2 tl = ch.topLeft;
            Vector2 br = ch.bottomRight;

            if (!any)
            {
                min = new Vector2(tl.x, br.y);
                max = new Vector2(br.x, tl.y);
                any = true;
            }
            else
            {
                min = Vector2.Min(min, new Vector2(tl.x, br.y));
                max = Vector2.Max(max, new Vector2(br.x, tl.y));
            }
        }

        if (!any)
            return label.textBounds;

        Bounds bounds = new Bounds();
        bounds.SetMinMax(new Vector3(min.x, min.y, 0f), new Vector3(max.x, max.y, 0f));
        return bounds;
    }

    // Anade (o reemplaza) el fondo de una etiqueta, ajustado al texto.
    private void AddLabelBackground(TextMeshPro label, int layerId, int order)
    {
        label.ForceMeshUpdate();
        Bounds bounds = GetGlyphBounds(label);

        GameObject bg = new GameObject("Fondo");
        bg.transform.SetParent(label.transform, false);
        bg.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, 0f);

        SpriteRenderer renderer = bg.AddComponent<SpriteRenderer>();
        renderer.sprite = GetLabelBackgroundSprite();
        renderer.drawMode = SpriteDrawMode.Sliced;

        Vector3 scale = label.transform.lossyScale;
        float padX = slotLabelBgPadding.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x));
        float padY = slotLabelBgPadding.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y));
        float minSide = 2f * Mathf.Max(0.01f, slotLabelCornerRadius);

        renderer.size = new Vector2(
            Mathf.Max(minSide, bounds.size.x + padX * 2f),
            Mathf.Max(minSide, bounds.size.y + padY * 2f));

        renderer.color = slotLabelBackgroundColor;
        renderer.sortingLayerID = layerId;
        renderer.sortingOrder = order - 1;   // siempre detras de su texto
    }

    // Muestra solo las etiquetas de las casillas enemigas ocupadas (p. ej. 2 enemigos = P1 y P2). Una unidad grande
    // cuenta sus dos casillas. Se llama al reconstruir etiquetas y cada vez que cambia la formacion.
    private void RefreshEnemySlotLabels()
    {
        for (int i = 0; i < enemyLabels.Length; i++)
        {
            if (enemyLabels[i] == null)
                continue;

            bool visible = !enemyLabelsOnlyOccupied || GetUnitOrWideAt(TeamSide.Enemy, i + 1) != null;

            if (enemyLabels[i].gameObject.activeSelf != visible)
                enemyLabels[i].gameObject.SetActive(visible);
        }
    }

    // Busca la Sorting Layer y el Order in Layer que deben usar las etiquetas.
    // Con slotLabelsBehindUnits copia el de una unidad existente y resta niveles, para quedar siempre detras de los personajes.
    private void ResolveLabelSorting(out int layerId, out int order)
    {
        layerId = SortingLayer.NameToID(slotLabelSortingLayer);
        order = slotLabelSorting;

        // Arriba no hay suelo detras: se dibujan DELANTE de todas las unidades para que ninguna cabeza las tape.
        if (slotLabelsOnTop && slotLabelsInFrontWhenOnTop)
        {
            bool found = false;
            int maxOrder = int.MinValue;

            foreach (List<BattleUnit> team in new[] { playerUnits, enemyUnits })
            {
                foreach (BattleUnit unit in team)
                {
                    SpriteRenderer sprite = unit != null ? unit.GetComponent<SpriteRenderer>() : null;

                    if (sprite == null)
                        continue;

                    if (!found)
                        layerId = sprite.sortingLayerID;

                    found = true;
                    maxOrder = Mathf.Max(maxOrder, sprite.sortingOrder);
                }
            }

            // +10 de holgura por si alguna unidad sube su orden al actuar; el fondo usa order - 1.
            if (found)
                order = maxOrder + 10;

            return;
        }

        if (!slotLabelsBehindUnits)
            return;

        SpriteRenderer reference = FindReferenceSprite(playerUnits);

        if (reference == null)
            reference = FindReferenceSprite(enemyUnits);

        if (reference == null)
            return;   // aun no hay unidades: se usan los valores manuales

        layerId = reference.sortingLayerID;
        order = reference.sortingOrder - Mathf.Max(1, slotLabelOrderBelowUnits);
    }

    private static SpriteRenderer FindReferenceSprite(List<BattleUnit> team)
    {
        foreach (BattleUnit unit in team)
        {
            if (unit == null)
                continue;

            SpriteRenderer sprite = unit.GetComponent<SpriteRenderer>();

            if (sprite != null)
                return sprite;
        }

        return null;
    }

    // Crea una etiqueta "P1".."P4" hija de cada punto visual (las casillas son fijas).
    private void BuildSlotLabels(Transform[] slots, float rowY, TextMeshPro[] store)
    {
        ResolveLabelSorting(out int layerId, out int order);

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            string labelName = "Etiqueta_P" + (i + 1);

            // Evita etiquetas duplicadas si se reconstruyen.
            // (Destroy es diferido: se desactivan y renombran ya, por si se reconstruye varias veces en el mismo frame.)
            for (int c = slots[i].childCount - 1; c >= 0; c--)
            {
                Transform old = slots[i].GetChild(c);

                if (old.name != labelName)
                    continue;

                old.name = labelName + "_old";
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }

            GameObject go = new GameObject(labelName);
            go.transform.SetParent(slots[i], false);
            go.transform.localPosition = slotLabelsOnTop
                ? new Vector3(slotLabelOffset.x, slotLabelTopFallback, slotLabelOffset.z)
                : slotLabelOffset;

            if (slotLabelsOneRow)
            {
                // Misma altura de mundo para ambos bandos; la X sigue a su casilla.
                Vector3 world = slots[i].position;
                world.x += slotLabelOffset.x;
                world.y = rowY;
                go.transform.position = world;
            }

            TextMeshPro label = go.AddComponent<TextMeshPro>();

            if (slotLabelFont != null)
                label.font = slotLabelFont;

            label.text = "P" + (i + 1);
            label.fontSize = slotLabelFontSize;
            label.color = slotLabelTextColor;
            label.alignment = TextAlignmentOptions.Center;

            // Se dibuja detras de los personajes (el fondo, un nivel mas atras).
            MeshRenderer meshRenderer = label.GetComponent<MeshRenderer>();
            meshRenderer.sortingLayerID = layerId;
            meshRenderer.sortingOrder = order;

            if (slotLabelBackground)
                AddLabelBackground(label, layerId, order);

            // Arriba: rowY es el borde inferior de la etiqueta, para que el fondo no toque las cabezas.
            if (slotLabelsOnTop)
            {
                Bounds glyphs = GetGlyphBounds(label);
                float halfHeight = glyphs.extents.y +
                                   (slotLabelBackground ? slotLabelBgPadding.y / Mathf.Max(0.0001f, Mathf.Abs(go.transform.lossyScale.y)) : 0f);
                go.transform.position += Vector3.up * (halfHeight - glyphs.center.y) * go.transform.lossyScale.y;

                // Si el borde superior se mete bajo el HUD, se baja hasta el limite.
                float limitY = ViewportLimitWorldY(go.transform.position.z);
                float topY = go.transform.position.y +
                             (glyphs.center.y + halfHeight) * go.transform.lossyScale.y;

                if (topY > limitY)
                    go.transform.position += Vector3.down * (topY - limitY);
            }

            if (store != null && i < store.Length)
                store[i] = label;
        }
    }

    // Comprueba que las posiciones iniciales sean validas.
    private void ValidateTeam(
        List<BattleUnit> team,
        TeamSide expectedSide
    )
    {
        HashSet<int> usedPositions = new HashSet<int>();

        if (team.Count > 4)
        {
            Debug.LogError(
                "Un equipo no puede tener mas de 4 unidades."
            );
        }

        foreach (BattleUnit unit in team)
        {
            if (unit == null)
                continue;

            if (unit.Side != expectedSide)
            {
                Debug.LogError(
                    unit.Data.unitName +
                    " pertenece al equipo equivocado."
                );
            }

            if (unit.Position < 1 || unit.Position > 4)
            {
                Debug.LogError(
                    unit.Data.unitName +
                    " tiene una posicion invalida."
                );
            }

            if (!usedPositions.Add(unit.Position))
            {
                Debug.LogError(
                    "Posicion duplicada: " + unit.Position
                );
            }
        }
    }

    // Muestra la formacion actual en consola.
    private void ShowTeamPositions(
        List<BattleUnit> team,
        string teamName
    )
    {
        Debug.Log("=== FORMACION: " + teamName + " ===");

        List<BattleUnit> orderedTeam =
            new List<BattleUnit>(team);

        orderedTeam.RemoveAll(unit => unit == null);

        orderedTeam.Sort((a, b) =>
            a.Position.CompareTo(b.Position));

        foreach (BattleUnit unit in orderedTeam)
        {
            Debug.Log(
                unit.Data.unitName +
                " - Posicion: " + unit.Position
            );
        }
    }

    // Obtiene la unidad viva en una posicion determinada.
    public BattleUnit GetUnitAtPosition(
        TeamSide side,
        int position
    )
    {
        List<BattleUnit> team =
            side == TeamSide.Player
            ? playerUnits
            : enemyUnits;

        foreach (BattleUnit unit in team)
        {
            if (unit != null &&
                unit.IsAlive &&
                unit.Position == position)
            {
                return unit;
            }
        }

        return null;
    }

    // Enemigo grande (UnitData.occupiesTwoSlots): ocupa su casilla y la siguiente, como en Darkest Dungeon.
    public static bool IsWide(BattleUnit unit)
    {
        return unit != null && unit.Data != null && unit.Data.occupiesTwoSlots;
    }

    // Unidad viva que ocupa 'position' contando la casilla extra de las unidades grandes (para areas y bloqueos).
    public BattleUnit GetUnitOrWideAt(TeamSide side, int position)
    {
        BattleUnit unit = GetUnitAtPosition(side, position);
        return unit != null ? unit : GetWideUnitReserving(side, position, null);
    }

    // Unidad grande cuya casilla extra es 'position' (la que tiene justo delante).
    private BattleUnit GetWideUnitReserving(TeamSide side, int position, BattleUnit ignore)
    {
        List<BattleUnit> team = side == TeamSide.Player ? playerUnits : enemyUnits;

        foreach (BattleUnit unit in team)
            if (unit != null && unit != ignore && unit.IsAlive && IsWide(unit) && unit.Position + 1 == position)
                return unit;

        return null;
    }

    // Reacomoda a los sobrevivientes hacia el frente.
    // deferVisual = la logica (posiciones P1-P4) se reordena ya, pero el movimiento visual de los supervivientes
    // espera (lo hace quien llama): asi no tapan al caido mientras se desvanece.
    public void ReorganizeTeam(TeamSide side, bool deferVisual = false)
    {
        List<BattleUnit> team =
            side == TeamSide.Player
            ? playerUnits
            : enemyUnits;

        // Eliminar las unidades derrotadas de la formacion.
        team.RemoveAll(unit =>
            unit == null || !unit.IsAlive);

        // Ordenar por posicion actual.
        team.Sort((a, b) =>
            a.Position.CompareTo(b.Position));

        // Asignar posiciones consecutivas (una unidad grande ocupa dos).
        int nextPosition = 1;

        for (int i = 0; i < team.Count; i++)
        {
            team[i].SetPosition(nextPosition);
            nextPosition += IsWide(team[i]) ? 2 : 1;
        }

        ShowTeamPositions(
            team,
            side.ToString()
        );

        // Actualizar la ubicacion visual.
        if (!deferVisual)
            UpdateVisualPositions();
    }


    public void HandleDefeatedUnit(BattleUnit unit)
    {
        if (unit == null || unit.IsAlive)
            return;

        Debug.Log(
            unit.Data.unitName + " ha sido derrotado."
        );

        // Los heroes NO mueren: quedan caidos en el suelo (sin turno ni objetivo) hasta que se les cura.
        if (unit.Side == TeamSide.Player)
        {
            HandleHeroDowned(unit);
            return;
        }

        // El personaje derrotado se oscurece, muestra un craneo y se desvanece (UnitDeathEffect); al terminar queda oculto.
        SpriteRenderer sprite =
            unit.GetComponent<SpriteRenderer>();

        if (sprite != null)
        {
            UnitDeathEffect.Play(unit, sprite);
        }

        // Recordar su lugar en la formacion completa (contando los huecos de
        // los caidos) para poder restaurarlo si revive.
        defeatedAt[unit] = FullFormationSlot(unit);

        // Reorganizar al equipo correspondiente.
        // Los supervivientes se recolocan visualmente cuando termina la animacion del caido (si no, tapan su silueta).
        ReorganizeTeam(unit.Side, true);

        if (sprite != null && sprite.enabled && isActiveAndEnabled)
            StartCoroutine(UpdateVisualsAfterDeath());
        else
            UpdateVisualPositions();
    }

    private void HandleHeroDowned(BattleUnit unit, bool silent = false)
    {
        // Pierde estados, regeneracion y preparaciones: al levantarse empieza limpio.
        StatusEffectController statuses = unit.GetComponent<StatusEffectController>();

        if (statuses != null)
            statuses.ClearAllEffects();

        RegenerationEffect regeneration = unit.GetComponent<RegenerationEffect>();

        if (regeneration != null)
            regeneration.ClearEffect();

        PreparedAbilityController prepared = unit.GetComponent<PreparedAbilityController>();

        if (prepared != null)
            prepared.Cancel("esta caido");

        if (!downedHeroes.Contains(unit))
            downedHeroes.Add(unit);

        defeatedAt[unit] = FullFormationSlot(unit);
        UnitDeathEffect.Cancel(unit);
        UnitDownedVisual.Play(unit);
        if (!silent)
            CombatFeedback.Show(unit, "¡Caído!", FeedbackKind.Debuff);

        // La logica (P1-P4) se reordena ya; el movimiento visual espera a que termine la caida.
        ReorganizeTeam(unit.Side, true);

        if (isActiveAndEnabled)
            StartCoroutine(UpdateVisualsAfterFall());
        else
            UpdateVisualPositions();
    }

    private System.Collections.IEnumerator UpdateVisualsAfterFall()
    {
        yield return new WaitForSeconds(0.45f);
        UpdateVisualPositions();
    }

    private System.Collections.IEnumerator UpdateVisualsAfterDeath()
    {
        yield return new WaitForSeconds(UnitDeathEffect.TotalSeconds + 0.05f);
        UpdateVisualPositions();
    }


    // Un enemigo que huye desaparece del campo y de la formacion, sin mensaje de derrota (no es una baja).
    private void HandleFledUnit(BattleUnit unit)
    {
        if (unit == null)
            return;

        SpriteRenderer sprite = unit.GetComponent<SpriteRenderer>();

        if (sprite != null)
            sprite.enabled = false;

        ReorganizeTeam(unit.Side);
    }

    // Actualiza las posiciones visuales de ambos equipos.
    public void UpdateVisualPositions()
    {
        UpdateTeamVisualPositions(
            playerUnits,
            playerPositions,
            true
        );

        UpdateTeamVisualPositions(
            enemyUnits,
            enemyPositions
        );

        PlaceDownedUnits();
        RefreshEnemySlotLabels();
    }

    // Coloca cada unidad en su punto visual correspondiente.
    private void UpdateTeamVisualPositions(
        List<BattleUnit> team,
        Transform[] positions,
        bool skipFallenSlots = false
    )
    {
        foreach (BattleUnit unit in team)
        {
            if (unit == null || !unit.IsAlive)
                continue;

            // Convertir posicion 1-4 a indice 0-3. Los heroes saltan las casillas de sus caidos (que no se mueven de su lugar).
            int index = (skipFallenSlots ? VisualSlotOf(unit.Side, unit.Position) : unit.Position) - 1;

            if (index < 0 || index >= positions.Length)
            {
                Debug.LogWarning(
                    "Posicion visual fuera de rango: " +
                    unit.Position
                );

                continue;
            }

            Transform targetPosition = positions[index];

            if (targetPosition == null)
            {
                Debug.LogWarning(
                    "Falta configurar el punto visual " +
                    unit.Position
                );

                continue;
            }

            // Mover el GameObject a su posicion asignada (una unidad grande queda entre sus dos casillas).
            Vector3 world = targetPosition.position;

            if (IsWide(unit) && index + 1 < positions.Length && positions[index + 1] != null)
            {
                world = (world + positions[index + 1].position) * 0.5f;

                if (unit.Data != null)
                    world.x += unit.Data.wideCenterOffsetX;   // alineacion fina con el resto de la formacion
            }

            // Si va justo detras de una unidad grande, se reparte el hueco (UnitData.offsetAfterWideUnit).
            if (unit.Data != null && unit.Data.offsetAfterWideUnit != 0f)
            {
                foreach (BattleUnit other in team)
                    if (other != null && other != unit && other.IsAlive && IsWide(other) && other.Position + 2 == unit.Position)
                        world.x += unit.Data.offsetAfterWideUnit;
            }

            unit.transform.position = world;
            homePos[unit] = world;
        }
    }


    // Anade una unidad a su formacion (carga de encuentros). Se rechaza si
    // ya esta registrada, si su posicion es invalida o si esta ocupada.
    public bool RegisterUnit(BattleUnit unit)
    {
        if (unit == null || unit.Position < 1 || unit.Position > 4)
            return false;

        List<BattleUnit> team =
            unit.Side == TeamSide.Player
            ? playerUnits
            : enemyUnits;

        if (team.Contains(unit) ||
            GetUnitAtPosition(unit.Side, unit.Position) != null ||
            GetWideUnitReserving(unit.Side, unit.Position, null) != null ||
            (IsWide(unit) && GetUnitAtPosition(unit.Side, unit.Position + 1) != null))
            return false;

        team.Add(unit);

        unit.OnDefeated -= HandleDefeatedUnit;
        unit.OnDefeated += HandleDefeatedUnit;
        unit.OnFled -= HandleFledUnit;
        unit.OnFled += HandleFledUnit;
        unit.OnRevived -= HandleRevivedUnit;
        unit.OnRevived += HandleRevivedUnit;

        UpdateVisualPositions();

        // Las etiquetas de arriba dependen de la altura de los sprites: se recolocan con cada unidad nueva.
        if (showSlotLabels && slotLabelsOnTop && slotLabelsBuilt)
            RebuildSlotLabels();

        return true;
    }

    // Vacia la formacion de un equipo (carga de encuentros).
    public void ClearTeam(TeamSide side)
    {
        List<BattleUnit> team =
            side == TeamSide.Player
            ? playerUnits
            : enemyUnits;

        foreach (BattleUnit unit in team)
        {
            if (unit != null)
            {
                unit.OnFled -= HandleFledUnit;
                unit.OnDefeated -= HandleDefeatedUnit;
                unit.OnRevived -= HandleRevivedUnit;
            }
        }

        // Los caidos de este equipo ya no cuentan para la formacion.
        List<BattleUnit> stale = new List<BattleUnit>();

        foreach (BattleUnit key in defeatedAt.Keys)
        {
            if (key == null || key.Side == side)
                stale.Add(key);
        }

        foreach (BattleUnit key in stale)
            defeatedAt.Remove(key);

        team.Clear();
    }

    // Comprueba, sin modificar nada, si MoveUnit() tendria exito.
    public bool CanMoveUnit(BattleUnit unit, int direction)
    {
        if (unit == null || !unit.IsAlive)
            return false;

        if (direction != -1 && direction != 1)
            return false;

        List<BattleUnit> team =
            unit.Side == TeamSide.Player
            ? playerUnits
            : enemyUnits;

        if (!team.Contains(unit))
            return false;

        int targetPosition = unit.Position + direction;

        if (targetPosition < 1 || targetPosition > 4)
            return false;

        // Las unidades grandes no se mueven, y nadie puede pisar sus dos casillas.
        return !IsWide(unit) &&
               !IsWide(GetUnitAtPosition(unit.Side, targetPosition)) &&
               GetWideUnitReserving(unit.Side, targetPosition, unit) == null;
    }

    // Mueve una unidad una posicion adelante o atras.
    // direction: -1 = avanzar, +1 = retroceder.
    public bool MoveUnit(BattleUnit unit, int direction)
    {
        if (unit == null || !unit.IsAlive)
        {
            Debug.LogWarning("La unidad no puede moverse.");
            return false;
        }

        if (direction != -1 && direction != 1)
        {
            Debug.LogWarning("Direccion no valida.");
            return false;
        }

        // Comprobar que pertenece a la formacion.
        List<BattleUnit> team =
            unit.Side == TeamSide.Player
            ? playerUnits
            : enemyUnits;

        if (!team.Contains(unit))
        {
            Debug.LogWarning(
                "La unidad no pertenece a este combate."
            );
            return false;
        }

        int originalPosition = unit.Position;
        int targetPosition = originalPosition + direction;

        // Validar los limites de la formacion.
        if (targetPosition < 1 || targetPosition > 4)
        {
            Debug.Log(
                "No es posible moverse fuera de la formacion."
            );
            return false;
        }

        if (!CanMoveUnit(unit, direction))
        {
            Debug.Log("Una unidad grande bloquea ese movimiento.");
            return false;
        }

        // Buscar si otro aliado ocupa el destino.
        BattleUnit otherUnit = GetUnitAtPosition(
            unit.Side,
            targetPosition
        );

        if (otherUnit != null && otherUnit != unit)
        {
            // Intercambiar posiciones.
            otherUnit.SetPosition(originalPosition);

            Debug.Log(
                unit.Data.unitName +
                " intercambia posicion con " +
                otherUnit.Data.unitName
            );
        }

        // Mover la unidad a la nueva posicion.
        unit.SetPosition(targetPosition);

        // Actualizar los sprites.
        UpdateVisualPositions();

        Debug.Log(
            unit.Data.unitName +
            " ahora esta en posicion " +
            unit.Position
        );

        return true;
    }

}