using System.Collections.Generic;
using UnityEngine;

// Indicadores visuales persistentes de estados (solo presentacion: leen el estado, nunca lo cambian):
//   - Evasion extra (Tiro Evasivo): un anillo verde palido en el suelo y chispas ligeras que suben alrededor del que esquiva.
//
// Va en la escena de combate junto a AttackCinematic y StunIndicator. Las marcas siguen al sprite de la unidad.
public class StatusVisuals : MonoBehaviour
{
    [Header("Quemadura")]
    [SerializeField] private Color burnColor = new Color(1f, 0.5f, 0.12f, 1f);
    [Range(3, 14)] [SerializeField] private int burnFlames = 8;

    [Header("Evasion extra")]
    [SerializeField] private Color evasionColor = new Color(0.62f, 1f, 0.78f, 1f);
    [Range(2, 8)] [SerializeField] private int evasionSparks = 4;

    private class Marker
    {
        public GameObject root;
        public SpriteRenderer main;        // mira o anillo
        public SpriteRenderer[] sparks;    // solo evasion
        public float age;
    }

    private readonly Dictionary<BattleUnit, Marker> burning = new Dictionary<BattleUnit, Marker>();
    private readonly Dictionary<BattleUnit, Marker> evasion = new Dictionary<BattleUnit, Marker>();
    private BattleUnit[] units = new BattleUnit[0];
    private float nextRefresh;

    private static Sprite ringSprite;
    private static Sprite glowSprite;

    private void OnDisable()
    {
        DestroyAll(evasion);
        DestroyAll(burning);
    }

    private static void DestroyAll(Dictionary<BattleUnit, Marker> map)
    {
        foreach (Marker m in map.Values)
        {
            if (m.root != null)
                Destroy(m.root);
        }

        map.Clear();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextRefresh)
        {
            units = FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude);
            nextRefresh = Time.unscaledTime + 0.4f;
        }

        EnsureSprites();

        foreach (BattleUnit unit in units)
        {
            if (unit == null)
                continue;

            SpriteRenderer sr = VisibleRenderer(unit);
            StatusEffectController statuses = sr != null ? unit.GetComponent<StatusEffectController>() : null;

            if (statuses != null && statuses.HasEffect(StatusEffectType.EvasionBoost))
                ShowEvasion(unit, sr);
            else
                Remove(evasion, unit);

            if (statuses != null && statuses.HasEffect(StatusEffectType.Burn))
                ShowBurn(unit, sr);
            else
                Remove(burning, unit);
        }

        Prune(evasion);
        Prune(burning);
    }

    private static SpriteRenderer VisibleRenderer(BattleUnit unit)
    {
        if (!unit.IsAlive || UnitDownedVisual.IsActive(unit))
            return null;

        SpriteRenderer sr = unit.GetComponentInChildren<SpriteRenderer>();
        return sr != null && sr.enabled && sr.sprite != null ? sr : null;
    }

    // ------------------------------------------------------------
    // Quemadura: llamas que suben por el cuerpo y un resplandor naranja en el suelo mientras dure el estado.
    // ------------------------------------------------------------

    private void ShowBurn(BattleUnit unit, SpriteRenderer sr)
    {
        if (!burning.TryGetValue(unit, out Marker m) || m.root == null)
        {
            m = NewMarker(unit, "Burn_", glowSprite, burnFlames);
            burning[unit] = m;
        }

        m.age += Time.unscaledDeltaTime;
        Bounds b = sr.bounds;
        float h = Mathf.Max(b.size.y, 1f);
        float pop = Mathf.Clamp01(m.age / 0.25f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(m.age * 7f);
        float z = unit.transform.position.z;
        float glowUnit = glowSprite.bounds.size.x;

        // Resplandor naranja plano en el suelo.
        m.main.transform.position = new Vector3(b.center.x, b.min.y + h * 0.03f, z);
        m.main.transform.localScale = new Vector3(h * 0.7f / glowUnit, h * 0.2f / glowUnit, 1f);
        m.main.sortingLayerID = sr.sortingLayerID;
        m.main.sortingOrder = sr.sortingOrder - 1;
        m.main.color = new Color(burnColor.r, burnColor.g, burnColor.b, pop * Mathf.Lerp(0.3f, 0.55f, pulse));

        // Llamas: puntos brillantes que suben, se achican y se apagan; cada una a su ritmo.
        for (int i = 0; i < m.sparks.Length; i++)
        {
            float cycle = 0.9f + 0.1f * (i % 4);
            float t = ((m.age + i * 0.37f) % cycle) / cycle;
            float side = Mathf.Sin(i * 2.1f + m.age * 3f) * h * 0.17f;
            SpriteRenderer sp = m.sparks[i];
            sp.transform.position = new Vector3(b.center.x + side, b.min.y + h * (0.08f + 0.62f * t), z);
            sp.transform.localScale = Vector3.one * (h * 0.075f / glowUnit) * (1f - 0.65f * t);
            sp.sortingLayerID = sr.sortingLayerID;
            sp.sortingOrder = sr.sortingOrder + 11;
            Color c = Color.Lerp(new Color(1f, 0.9f, 0.45f, 1f), burnColor, t);
            sp.color = new Color(c.r, c.g, c.b, pop * Mathf.Sin(t * Mathf.PI) * 0.95f);
        }
    }

    // La Marca del Cazador no tiene indicador persistente sobre el cuerpo: ya la muestra el icono de estado bajo la barra de
    // vida. Solo se ve su animacion de impacto (AttackCinematic).

    // ------------------------------------------------------------
    // Evasion extra
    // ------------------------------------------------------------

    private void ShowEvasion(BattleUnit unit, SpriteRenderer sr)
    {
        if (!evasion.TryGetValue(unit, out Marker m) || m.root == null)
        {
            m = NewMarker(unit, "Evasion_", ringSprite, evasionSparks);
            evasion[unit] = m;
        }

        m.age += Time.unscaledDeltaTime;
        Bounds b = sr.bounds;
        float h = Mathf.Max(b.size.y, 1f);
        float pop = Mathf.Clamp01(m.age / 0.25f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(m.age * 3f);
        float z = unit.transform.position.z;

        // Anillo plano en el suelo (elipse) que respira.
        Vector3 feet = new Vector3(b.center.x, b.min.y + h * 0.015f, z);
        m.main.transform.position = feet;
        float ringW = h * 0.62f * (1f + 0.06f * pulse) / ringSprite.bounds.size.x;
        m.main.transform.localScale = new Vector3(ringW, ringW * 0.28f, 1f);
        m.main.sortingLayerID = sr.sortingLayerID;
        m.main.sortingOrder = sr.sortingOrder - 1;
        m.main.color = new Color(evasionColor.r, evasionColor.g, evasionColor.b, pop * Mathf.Lerp(0.35f, 0.7f, pulse));

        // Chispas ligeras que suben y se reinician (como viento).
        for (int i = 0; i < m.sparks.Length; i++)
        {
            float cycle = 1.6f;
            float t = ((m.age + i * cycle / m.sparks.Length) % cycle) / cycle;
            float side = Mathf.Sin(i * 2.4f + m.age * 1.2f) * h * 0.2f;
            SpriteRenderer sp = m.sparks[i];
            sp.transform.position = new Vector3(b.center.x + side, b.min.y + h * (0.05f + 0.6f * t), z);
            sp.transform.localScale = Vector3.one * (h * 0.035f / glowSprite.bounds.size.x) * (1f - 0.5f * t);
            sp.sortingLayerID = sr.sortingLayerID;
            sp.sortingOrder = sr.sortingOrder + 11;
            sp.color = new Color(evasionColor.r, evasionColor.g, evasionColor.b, pop * Mathf.Sin(t * Mathf.PI) * 0.9f);
        }
    }

    // ------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------

    private Marker NewMarker(BattleUnit unit, string prefix, Sprite mainSprite, int sparkCount)
    {
        GameObject root = new GameObject(prefix + unit.name);
        root.transform.SetParent(transform, false);

        GameObject mainGo = new GameObject("Main");
        mainGo.transform.SetParent(root.transform, false);
        SpriteRenderer main = mainGo.AddComponent<SpriteRenderer>();
        main.sprite = mainSprite;
        main.color = new Color(1f, 1f, 1f, 0f);

        Marker m = new Marker { root = root, main = main, sparks = new SpriteRenderer[sparkCount], age = 0f };

        for (int i = 0; i < sparkCount; i++)
        {
            GameObject go = new GameObject("Spark" + i);
            go.transform.SetParent(root.transform, false);
            SpriteRenderer sp = go.AddComponent<SpriteRenderer>();
            sp.sprite = glowSprite;
            sp.color = new Color(1f, 1f, 1f, 0f);
            m.sparks[i] = sp;
        }

        return m;
    }

    private static void Remove(Dictionary<BattleUnit, Marker> map, BattleUnit unit)
    {
        if (map.TryGetValue(unit, out Marker m))
        {
            if (m.root != null)
                Destroy(m.root);

            map.Remove(unit);
        }
    }

    // Marcadores de unidades destruidas.
    private static void Prune(Dictionary<BattleUnit, Marker> map)
    {
        List<BattleUnit> orphans = null;

        foreach (KeyValuePair<BattleUnit, Marker> pair in map)
        {
            if (pair.Key == null || pair.Value.root == null)
            {
                if (orphans == null)
                    orphans = new List<BattleUnit>();

                if (pair.Value.root != null)
                    Destroy(pair.Value.root);

                orphans.Add(pair.Key);
            }
        }

        if (orphans != null)
        {
            foreach (BattleUnit key in orphans)
                map.Remove(key);
        }
    }

    private static void EnsureSprites()
    {
        if (ringSprite == null)
            ringSprite = BuildRingSprite();

        if (glowSprite == null)
            glowSprite = BuildGlowSprite();
    }

    private static Sprite BuildRingSprite()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                float line = Mathf.Exp(-Mathf.Pow((d - 0.86f) / 0.05f, 2f));
                float halo = Mathf.Exp(-Mathf.Pow((d - 0.86f) / 0.14f, 2f)) * 0.35f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(line + halo)));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite BuildGlowSprite()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2f)));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
