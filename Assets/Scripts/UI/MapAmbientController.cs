using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Ambientacion animada del mapa (Paso 63). Solo anade capas decorativas
// transparentes detras de marcadores y paneles (NubesAnimadas,
// NieblaAmbiental, EfectosRegionales): no toca la ilustracion, los
// marcadores ni la logica. Todo el material sale de un grupo fijo de
// objetos creado una vez (nada se instancia mientras corre) y ninguno
// recibe raycast.
public class MapAmbientController : MonoBehaviour
{
    [Serializable]
    public class RegionSettings
    {
        [Tooltip("0 = apagado, 1 = valor de diseno, 2 = el doble de visible.")]
        [Range(0f, 2f)] public float intensity = 1f;
        [Tooltip("Multiplicador de velocidad de la animacion.")]
        [Range(0f, 3f)] public float speed = 1f;
    }

    [Header("General")]
    [SerializeField] private MapArtSet art;
    [SerializeField] private bool effectsEnabled = true;
    [SerializeField] private bool paused;
    [Tooltip("Detiene la animacion cuando la ventana pierde el foco (apagado por defecto: en el editor el foco de Game View pausaria los efectos sin motivo).")]
    [SerializeField] private bool pauseWhenUnfocused = false;
    [Tooltip("Diagnostico: efectos mucho mas visibles y rapidos (opacidad x2.5, velocidad x3, intensidad minima 1). No cambia los valores normales.")]
    [SerializeField] private bool visualDebugMode = false;

    [Header("Regiones")]
    [SerializeField] private RegionSettings meadow = new RegionSettings();
    [SerializeField] private RegionSettings forest = new RegionSettings { intensity = 1f, speed = 1f };
    [SerializeField] private RegionSettings corruption = new RegionSettings { intensity = 1f, speed = 1f };

    private class Mote
    {
        public RectTransform rect;
        public Image image;
        public Vector2 start, velocity;
        public float duration, phase, size, alpha, spin;
        public float life;          // 0..1, se reinicia al terminar
        public float order;         // para ocultar los sobrantes con intensidad baja
    }

    private class Cloud
    {
        public RectTransform rect;
        public Image[] blobs;
        public float speed, y, baseAlpha;
    }

    private readonly List<Cloud> clouds = new List<Cloud>();
    private readonly List<Image> mists = new List<Image>();
    private readonly List<Vector2> mistBase = new List<Vector2>();
    private readonly List<float> mistPhase = new List<float>();
    private readonly List<Mote> pollen = new List<Mote>();
    private readonly List<Mote> leaves = new List<Mote>();
    private readonly List<Mote> fireflies = new List<Mote>();
    private readonly List<Mote> sparks = new List<Mote>();
    private Image throneGlow;
    private RectTransform groupClouds, groupMist, groupEffects;
    private float tMeadow, tForest, tCorruption;
    private System.Random rng;

    private const float ThroneX = 1760f, ThroneY = 905f;
    private const float ShrineX = 985f, ShrineY = 735f;

    public bool EffectsEnabled { get => effectsEnabled; set => effectsEnabled = value; }
    public bool Paused { get => paused; set => paused = value; }
    public int ActiveObjects => clouds.Count * 3 + mists.Count + pollen.Count + leaves.Count + fireflies.Count + sparks.Count + (throneGlow != null ? 1 : 0);

    // ---------------- Construccion ----------------

    // Crea los objetos decorativos dentro de los grupos de MapArea. Se llama
    // desde WorldMapController.BuildUI despues de crear la jerarquia.
    public void Build(RectTransform area)
    {
        groupClouds = Prepare(area, "NubesAnimadas");
        groupMist = Prepare(area, "NieblaAmbiental");
        groupEffects = Prepare(area, "EfectosRegionales");

        clouds.Clear(); mists.Clear(); mistBase.Clear(); mistPhase.Clear();
        pollen.Clear(); leaves.Clear(); fireflies.Clear(); sparks.Clear();
        throneGlow = null;

        if (groupClouds == null || groupMist == null || groupEffects == null || art == null || art.softCircle == null)
            return;

        rng = new System.Random(63);
        BuildClouds();
        BuildMist();
        BuildPollenAndLeaves();
        BuildFireflies();
        BuildCorruption();
        Apply(0f);
    }

    private static RectTransform Prepare(RectTransform area, string name)
    {
        Transform t = area.Find(name);

        if (t == null)
            return null;

        for (int i = t.childCount - 1; i >= 0; i--)
            DestroyImmediate(t.GetChild(i).gameObject);

        return (RectTransform)t;
    }

    private float R(float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    private Image Blob(string name, Transform parent, Vector2 pos, Vector2 size, Color color, float rotation = 0f)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        Image image = go.GetComponent<Image>();
        image.sprite = art.softCircle;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void BuildClouds()
    {
        // Dos capas de velo en la franja del cielo, sobre Pradera y Bosque.
        float[] ys = { 965f, 925f, 985f };
        float[] speeds = { 12f, 18f, 8f };
        float[] xs = { 150f, 650f, 1000f };

        for (int i = 0; i < 3; i++)
        {
            RectTransform root = (RectTransform)new GameObject("Nube_" + i, typeof(RectTransform)).transform;
            root.SetParent(groupClouds, false);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;
            root.anchoredPosition = new Vector2(xs[i], ys[i]);

            Cloud cloud = new Cloud { rect = root, speed = speeds[i], y = ys[i], baseAlpha = i == 2 ? 0.24f : 0.34f, blobs = new Image[3] };
            Vector2[] off = { new Vector2(-120f, 0f), new Vector2(10f, 14f), new Vector2(130f, -6f) };
            Vector2[] size = { new Vector2(300f, 70f), new Vector2(360f, 84f), new Vector2(280f, 64f) };

            for (int b = 0; b < 3; b++)
                cloud.blobs[b] = Blob("Velo", root, off[b], size[b], new Color(1f, 1f, 1f, 0f));

            clouds.Add(cloud);
        }
    }

    private void BuildMist()
    {
        Vector2[] pos = { new Vector2(820f, 420f), new Vector2(1000f, 560f), new Vector2(1150f, 330f), new Vector2(900f, 250f), new Vector2(1230f, 620f) };

        for (int i = 0; i < pos.Length; i++)
        {
            mists.Add(Blob("Bruma_" + i, groupMist, pos[i], new Vector2(R(620f, 780f), R(180f, 240f)), new Color(0.78f, 0.90f, 0.90f, 0f)));
            mistBase.Add(pos[i]);
            mistPhase.Add(R(0f, 6.28f));
        }
    }

    private Mote NewMote(List<Mote> list, string name, Vector2 size, Color color, float rotation = 0f)
    {
        Image image = Blob(name, groupEffects, Vector2.zero, size, new Color(color.r, color.g, color.b, 0f), rotation);
        Mote mote = new Mote
        {
            rect = image.rectTransform, image = image, size = size.x,
            alpha = color.a, phase = R(0f, 6.28f), life = R(0f, 1f), order = list.Count
        };
        list.Add(mote);
        return mote;
    }

    private void BuildPollenAndLeaves()
    {
        for (int i = 0; i < 12; i++)
        {
            float s = R(6f, 10f);
            Mote m = NewMote(pollen, "Polen", new Vector2(s, s), new Color(1f, 0.96f, 0.72f, 0.9f));
            m.duration = R(16f, 26f);
            Respawn(m, 40f, 900f, 140f, 560f, new Vector2(R(10f, 18f), R(10f, 16f)));
        }

        for (int i = 0; i < 6; i++)
        {
            Mote m = NewMote(leaves, "Hoja", new Vector2(18f, 9f), new Color(0.72f, 0.82f, 0.32f, 0.9f), R(0f, 180f));
            m.duration = R(22f, 34f);
            m.spin = R(-18f, 18f);
            Respawn(m, 60f, 880f, 640f, 900f, new Vector2(R(10f, 18f), -R(16f, 26f)));
        }
    }

    private void BuildFireflies()
    {
        for (int i = 0; i < 12; i++)
        {
            float s = R(9f, 15f);
            Mote m = NewMote(fireflies, "Luciernaga", new Vector2(s, s), new Color(1f, 0.95f, 0.52f, 0.95f));
            m.start = new Vector2(R(0.35f, 1f), R(0.35f, 1f));      // radios relativos
            m.velocity = new Vector2(R(0.25f, 0.50f), R(0.20f, 0.42f)); // frecuencias
            m.duration = R(0.5f, 1.3f);                                  // pulso del brillo
        }
    }

    private void BuildCorruption()
    {
        for (int i = 0; i < 14; i++)
        {
            float s = R(7f, 12f);
            Mote m = NewMote(sparks, "Particula", new Vector2(s, s), new Color(0.78f, 0.52f, 1f, 0.9f));
            m.duration = R(14f, 24f);
            Respawn(m, 1380f, 1900f, 120f, 640f, new Vector2(R(-6f, 6f), R(12f, 20f)));
        }

        throneGlow = Blob("PulsoTrono", groupEffects, new Vector2(ThroneX, ThroneY), new Vector2(560f, 320f), new Color(0.82f, 0.30f, 0.90f, 0f));
    }

    private void Respawn(Mote m, float x0, float x1, float y0, float y1, Vector2 velocity)
    {
        m.start = new Vector2(R(x0, x1), R(y0, y1));
        m.velocity = velocity;
        m.phase = R(0f, 6.28f);
    }

    // ---------------- Animacion ----------------

    // Las listas de objetos no se serializan: al entrar en Play Mode (o tras
    // recargar scripts) quedan vacias aunque las capas sigan en la escena.
    // Por eso se reconstruyen aqui si hace falta.
    private bool logged;

    private void EnsureBuilt()
    {
        if (groupClouds != null && clouds.Count > 0)
            return;

        Transform areaT = transform.Find("WorldMapCanvas/MapArea");

        if (areaT != null)
            Build((RectTransform)areaT);
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            EnsureBuilt();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        EnsureBuilt();

        if (!logged)
        {
            logged = true;
            Debug.Log("[MapAmbient] inicializado: efectos=" + effectsEnabled + ", objetos=" + ActiveObjects +
                      ", debug=" + visualDebugMode + ", pausa por foco=" + pauseWhenUnfocused);
        }

        Tick(Time.unscaledDeltaTime);
    }

    private float Spd(RegionSettings r)
    {
        return r.speed * (visualDebugMode ? 3f : 1f);
    }

    private float Int(RegionSettings r)
    {
        return visualDebugMode ? Mathf.Max(r.intensity, 1f) * 2.5f : r.intensity;
    }

    // Avanza la animacion dt segundos (publico para poder probarla en editor).
    public void Tick(float dt)
    {
        EnsureBuilt();

        bool run = effectsEnabled && !paused && !(pauseWhenUnfocused && Application.isPlaying && !Application.isFocused);

        if (groupClouds == null)
            return;

        bool show = effectsEnabled;
        foreach (RectTransform g in new[] { groupClouds, groupMist, groupEffects })
            if (g.gameObject.activeSelf != show) g.gameObject.SetActive(show);

        if (!show)
            return;

        if (run)
        {
            tMeadow += dt * Spd(meadow);
            tForest += dt * Spd(forest);
            tCorruption += dt * Spd(corruption);
        }

        Apply(run ? dt : 0f);
    }

    private static float Envelope(float life)
    {
        return Mathf.Sin(Mathf.Clamp01(life) * Mathf.PI);
    }

    private static bool Visible(Mote m, int count, float intensity)
    {
        return m.order < Mathf.Ceil(count * Mathf.Clamp01(intensity));
    }

    private void Apply(float dt)
    {
        // Nubes: derivan lento y se desvanecen en los extremos del recorrido.
        for (int i = 0; i < clouds.Count; i++)
        {
            Cloud c = clouds[i];
            float span = 1700f;
            float x = -300f + Mathf.Repeat(c.rect.anchoredPosition.x + 300f + c.speed * Spd(meadow) * dt, span);
            c.rect.anchoredPosition = new Vector2(x, c.y);
            float edge = Mathf.Clamp01(Mathf.Min(x + 300f, 1400f - x) / 260f);
            float a = c.baseAlpha * Int(meadow) * edge;

            foreach (Image blob in c.blobs)
                SetAlpha(blob, a);
        }

        // Niebla del bosque.
        for (int i = 0; i < mists.Count; i++)
        {
            float t = tForest;
            Vector2 b = mistBase[i];
            mists[i].rectTransform.anchoredPosition = b + new Vector2(Mathf.Sin(t * 0.10f + mistPhase[i]) * 90f, Mathf.Sin(t * 0.07f + mistPhase[i] * 1.7f) * 14f);
            float breathe = 0.75f + 0.25f * Mathf.Sin(t * 0.12f + mistPhase[i]);
            SetAlpha(mists[i], 0.18f * Int(forest) * breathe);
        }

        StepDrifters(pollen, dt, tMeadow, meadow, true);
        StepDrifters(leaves, dt, tMeadow, meadow, true);
        StepDrifters(sparks, dt, tCorruption, corruption, false);

        // Luciernagas alrededor del Santuario Velado.
        for (int i = 0; i < fireflies.Count; i++)
        {
            Mote m = fireflies[i];
            bool on = Visible(m, fireflies.Count, Int(forest));
            float t = tForest;
            Vector2 p = new Vector2(
                ShrineX + Mathf.Sin(t * m.velocity.x + m.phase) * 170f * m.start.x,
                ShrineY + Mathf.Cos(t * m.velocity.y + m.phase * 1.3f) * 110f * m.start.y);
            m.rect.anchoredPosition = p;
            float pulse = 0.25f + 0.75f * (0.5f + 0.5f * Mathf.Sin(t * m.duration * 1.6f + m.phase));
            SetAlpha(m.image, on ? m.alpha * pulse * Mathf.Min(1f, Int(forest)) : 0f);
        }

        // Pulso lento del Trono Sellado.
        if (throneGlow != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(tCorruption * 0.35f);
            SetAlpha(throneGlow, (0.07f + 0.13f * pulse) * Int(corruption));
        }
    }

    private void StepDrifters(List<Mote> list, float dt, float t, RegionSettings region, bool sway)
    {
        for (int i = 0; i < list.Count; i++)
        {
            Mote m = list[i];

            if (dt > 0f)
            {
                m.life += dt * Spd(region) / m.duration;

                if (m.life >= 1f)
                {
                    m.life -= 1f;

                    if (list == pollen) Respawn(m, 40f, 900f, 140f, 560f, m.velocity);
                    else if (list == leaves) Respawn(m, 60f, 880f, 640f, 900f, m.velocity);
                    else Respawn(m, 1380f, 1900f, 120f, 640f, m.velocity);
                }
            }

            float age = m.life * m.duration;
            Vector2 p = m.start + m.velocity * age;
            p.x += Mathf.Sin(age * 0.5f + m.phase) * (sway ? 18f : 10f);
            m.rect.anchoredPosition = p;

            if (m.spin != 0f)
                m.rect.localRotation = Quaternion.Euler(0f, 0f, m.spin * age + m.phase * 30f);

            bool on = Visible(m, list.Count, Int(region));
            SetAlpha(m.image, on ? m.alpha * Envelope(m.life) * Mathf.Min(1f, Int(region)) : 0f);
        }
    }

    private static void SetAlpha(Image image, float alpha)
    {
        Color c = image.color;

        if (!Mathf.Approximately(c.a, alpha))
        {
            c.a = alpha;
            image.color = c;
        }
    }
}
