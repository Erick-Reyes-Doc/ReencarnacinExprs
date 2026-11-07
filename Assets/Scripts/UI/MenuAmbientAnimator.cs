using UnityEngine;
using UnityEngine.UI;

// Animaciones ambientales del menu principal: balanceo del farol izquierdo y hojas rojas cayendo.
// Ambos elementos son hijos del fondo (que se ajusta al lienzo con AspectRatioFitter), asi que viven en
// coordenadas de la imagen original (1670x941) y se escalan con ella: siguen alineados en cualquier resolucion.
// Usan tiempo no escalado, no capturan clics y no tocan el fondo.
[ExecuteAlways]
public class MenuAmbientAnimator : MonoBehaviour
{
    [Header("Fondo (referencia de escala)")]
    public RectTransform background;
    [Tooltip("Ancho en pixeles de la imagen original del fondo.")]
    public float backgroundPixelWidth = 1670f;
    public float backgroundPixelHeight = 941f;

    [Header("Farol")]
    [Tooltip("Punto de giro: parte superior de la cadena, sobre la argolla del soporte de madera.")]
    public RectTransform lanternPivot;
    [Tooltip("Posicion del punto de giro en pixeles de la imagen original (origen arriba a la izquierda).")]
    public Vector2 lanternPivotPixel = new Vector2(221f, 352f);
    public Vector2 lanternNativeSize = new Vector2(1024f, 1536f);
    [Tooltip("Escala del farol sobre la imagen original.")]
    public float lanternScale = 0.10f;
    public float lanternAngle = 3f;
    [Tooltip("Segundos de una oscilacion completa.")]
    public float lanternPeriod = 4.6f;

    [Header("Hojas")]
    public RectTransform[] leaves = new RectTransform[0];
    [Tooltip("Zona de nacimiento (pixeles de la imagen original, origen arriba a la izquierda): copa del arbol superior izquierdo.")]
    public Rect leafSpawnArea = new Rect(110f, 60f, 520f, 170f);
    [Tooltip("Largo visible de la hoja (px de la imagen original), minimo y maximo.")]
    public Vector2 leafLength = new Vector2(32f, 50f);
    [Tooltip("Lado del sprite entero respecto al largo visible de la hoja.")]
    public float leafSpriteRatio = 1.458f;
    public Vector2 fallSpeed = new Vector2(40f, 68f);
    public Vector2 windSpeed = new Vector2(8f, 20f);
    public Vector2 swayAmplitude = new Vector2(10f, 22f);
    [Range(0f, 1f)] public float leafAlpha = 1f;

    private class Leaf
    {
        public float x, y, fall, wind, swayA, swayW, swayP, rot, spin, rotA, rotW, rotP, size, age, fade;
    }

    private Leaf[] state;
    private float lastWidth = -1f;

    private void OnEnable()
    {
        ResetLeaves();
        Layout();
    }

    private void ResetLeaves()
    {
        state = new Leaf[leaves != null ? leaves.Length : 0];

        for (int i = 0; i < state.Length; i++)
        {
            state[i] = new Leaf();
            Respawn(state[i]);
            // Reparto inicial a lo largo de su recorrido para que desde el primer instante haya hojas visibles.
            float advance = Random.value * 8f;
            state[i].age = advance;
            state[i].y += state[i].fall * advance;
            state[i].x += state[i].wind * advance;
            state[i].fade = 1f;
        }
    }

    private void Respawn(Leaf l)
    {
        l.x = leafSpawnArea.x + Random.value * leafSpawnArea.width;
        l.y = leafSpawnArea.y + Random.value * leafSpawnArea.height;
        l.fall = Random.Range(fallSpeed.x, fallSpeed.y);
        l.wind = Random.Range(windSpeed.x, windSpeed.y);
        l.swayA = Random.Range(swayAmplitude.x, swayAmplitude.y);
        l.swayW = Random.Range(0.8f, 1.5f);
        l.swayP = Random.value * 6.2832f;
        l.rot = Random.Range(0f, 360f);
        l.spin = Random.Range(-14f, 14f);
        l.rotA = Random.Range(18f, 40f);
        l.rotW = Random.Range(0.8f, 1.5f);
        l.rotP = Random.value * 6.2832f;
        l.size = Random.Range(leafLength.x, leafLength.y);
        l.age = 0f;
        l.fade = 0f;
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            AnimateLantern();
            AnimateLeaves(dt);
        }
    }

    private void LateUpdate()
    {
        Layout();
    }

    // Balanceo suave: seno principal mas una componente lenta que varia la amplitud, sin sacudidas.
    private void AnimateLantern()
    {
        if (lanternPivot == null)
            return;

        float t = Time.unscaledTime;
        float w = 2f * Mathf.PI / Mathf.Max(lanternPeriod, 0.5f);
        float amplitude = lanternAngle * (0.88f + 0.12f * Mathf.Sin(t * w * 0.37f + 1.3f));
        lanternPivot.localRotation = Quaternion.Euler(0f, 0f, amplitude * Mathf.Sin(t * w));
    }

    private void AnimateLeaves(float dt)
    {
        if (state == null || state.Length != (leaves != null ? leaves.Length : 0))
            ResetLeaves();

        for (int i = 0; i < state.Length; i++)
        {
            Leaf l = state[i];
            l.age += dt;
            l.y += l.fall * dt;
            l.x += l.wind * dt;
            l.rot += l.spin * dt;
            l.fade = Mathf.Min(1f, l.fade + dt / 0.8f);

            // Fuera de la pantalla (abajo o a la derecha): se reutiliza desde la copa del arbol.
            if (l.y > backgroundPixelHeight + 40f || l.x > backgroundPixelWidth + 40f)
                Respawn(l);
        }
    }

    private void Layout()
    {
        if (background == null)
            return;

        float k = background.rect.width / Mathf.Max(backgroundPixelWidth, 1f);

        if (k <= 0f)
            return;

        if (lanternPivot != null)
        {
            lanternPivot.anchorMin = lanternPivot.anchorMax = new Vector2(0f, 1f);
            lanternPivot.pivot = new Vector2(0.5f, 0.5f);
            lanternPivot.sizeDelta = Vector2.zero;
            lanternPivot.anchoredPosition = new Vector2(lanternPivotPixel.x * k, -lanternPivotPixel.y * k);

            RectTransform lantern = lanternPivot.Find("Farol") as RectTransform;

            if (lantern != null)
            {
                lantern.anchorMin = lantern.anchorMax = new Vector2(0.5f, 0.5f);
                lantern.pivot = new Vector2(0.498f, 0.9544f);   // argolla superior de la cadena
                lantern.anchoredPosition = Vector2.zero;
                lantern.sizeDelta = lanternNativeSize * (lanternScale * k);
            }
        }

        if (state == null || leaves == null || state.Length != leaves.Length)
            return;

        float time = Application.isPlaying ? Time.unscaledTime : 0f;

        for (int i = 0; i < leaves.Length; i++)
        {
            RectTransform r = leaves[i];

            if (r == null)
                continue;

            Leaf l = state[i];
            float side = l.size * leafSpriteRatio * k;
            float sway = l.swayA * Mathf.Sin(time * l.swayW + l.swayP);
            float bob = l.swayA * 0.25f * Mathf.Sin(time * l.swayW * 2f + l.swayP);

            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(side, side);
            r.anchoredPosition = new Vector2((l.x + sway) * k, -(l.y + bob) * k);
            r.localRotation = Quaternion.Euler(0f, 0f, l.rot + l.rotA * Mathf.Sin(time * l.rotW + l.rotP));

            Graphic g = r.GetComponent<Graphic>();

            if (g != null)
            {
                Color c = g.color;
                c.a = leafAlpha * Mathf.SmoothStep(0f, 1f, l.fade);
                g.color = c;
            }
        }
    }
}
