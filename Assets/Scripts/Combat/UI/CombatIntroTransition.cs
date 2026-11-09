using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Transicion de inicio de combate: la pantalla se oscurece, una espada entra por la izquierda y otra por la derecha,
// se cruzan formando una X con un pequeno destello dorado y se retiran para revelar el combate (1.5 s).
//
// - Solo presentacion: dibuja sobre su propio Canvas y se destruye al terminar.
// - Mientras dura, el combate esta detenido (Time.timeScale = 0) y todo se anima con tiempo no escalado: no avanzan
//   turnos, IA ni ataques, el fondo oscuro bloquea los clics y la pausa no se puede abrir. Al terminar (o al destruirse
//   el componente) se restaura el tiempo.
// - Se reproduce una vez al cargar la escena de cada encuentro; no se repite dentro del mismo encuentro.
// - Las dos espadas son el sprite UICombate+/Espada: la segunda es su reflejo horizontal. No se anade ni se modifica
//   ningun sprite.
public class CombatIntroTransition : MonoBehaviour
{
    [Header("Sprite (existente)")]
    [Tooltip("Sprite de la espada (Assets/Sprites/UICombate+/Espada). La segunda espada es su reflejo.")]
    [SerializeField] private Sprite swordSprite;

    [Header("Tiempos (segundos)")]
    [SerializeField] private float duration = 2f;
    [Tooltip("Segundos extra que las espadas se quedan cruzadas tras el impacto, antes de retirarse y revelar el combate.")]
    [SerializeField, Min(0f)] private float crossHoldSeconds = 1.5f;

    [Header("Aspecto")]
    [Tooltip("Ancho en pixeles (lienzo 1920x1080) de cada espada.")]
    [SerializeField] private float swordWidth = 330f;
    [Tooltip("Centro del cruce: desplazamiento vertical desde el centro de la pantalla (positivo = arriba), para quedar entre el encabezado y los paneles.")]
    [SerializeField] private float crossCenterY = 90f;
    [Tooltip("Tamano maximo del destello (1 = 128 px).")]
    [SerializeField] private float flashSize = 2.2f;
    [SerializeField] private Color veilColor = new Color(0.063f, 0.082f, 0.118f, 1f);   // #10151E
    [SerializeField] private float veilOpacity = 0.55f;
    [SerializeField] private Color flashColor = new Color(0.83f, 0.69f, 0.42f, 1f);     // #D4AF6A

    // true mientras la animacion esta en curso (el HUD ignora Esc, el combate esta congelado).
    public static bool IsPlaying { get; private set; }

    private bool played;
    private float timeScaleBefore = 1f;
    private Canvas canvas;
    private Sprite flashSprite;

    private void Awake()
    {
        // Se congela cuanto antes: ningun turno ni ataque puede empezar antes de que termine la animacion.
        if (!played && swordSprite != null)
        {
            timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            IsPlaying = true;
        }
    }

    private void Start()
    {
        if (played)
            return;

        played = true;

        if (!IsPlaying)
            return;   // sin sprite o sin malla: no hay transicion y el combate sigue

        StartCoroutine(Play());
    }

    private void OnDestroy()
    {
        Release();
    }

    private void Release()
    {
        if (!IsPlaying)
            return;

        IsPlaying = false;
        Time.timeScale = timeScaleBefore;
    }

    // ------------------------------------------------------------
    // Animacion
    // ------------------------------------------------------------

    private IEnumerator Play()
    {
        BuildSprites();
        BuildCanvas(out Image veil, out RectTransform swordA, out RectTransform swordB, out Image imageA, out Image imageB, out RectTransform flash, out Image flashImage);

        // Linea de tiempo (s): oscurecer 0-0.25 | espada izq. 0.10-0.55 | espada der. 0.20-0.65 | destello 0.62-0.90 |
        // retirada 0.90-1.30 | revelar combate 0.90-1.50.
        const float offX = 1100f;
        float scale = swordWidth / swordSprite.rect.width;
        float elapsed = 0f;
        float total = Mathf.Max(duration, 0.5f);
        float k = total / 1.5f;   // si se cambia la duracion, todo se escala proporcionalmente
        float hold = Mathf.Max(0f, crossHoldSeconds);
        const float holdAt = 0.82f;   // momento (de la linea de tiempo) en que las espadas ya estan cruzadas y termino el destello
        float holdStart = holdAt * k;

        while (elapsed < total + hold)
        {
            elapsed += Time.unscaledDeltaTime;

            // Pausa con las espadas cruzadas: la linea de tiempo se detiene en el cruce y despues continua.
            float t = elapsed < holdStart ? elapsed / k
                    : elapsed < holdStart + hold ? holdAt
                    : (elapsed - hold) / k;

            SetAlpha(veil, Mathf.Lerp(0f, veilOpacity, Smooth(t / 0.25f)) * (1f - Smooth((t - 0.90f) / 0.60f)));

            // Entrada: se desliza con rotacion (la hoja apunta al centro y se endereza en la X).
            float inA = EaseOut((t - 0.10f) / 0.45f);
            float inB = EaseOut((t - 0.20f) / 0.45f);
            float outAll = Smooth((t - 0.90f) / 0.40f);

            Place(swordA, imageA, -1f, Mathf.Lerp(-offX, 0f, inA) + Mathf.Lerp(0f, -offX * 0.6f, outAll), crossCenterY, Mathf.Lerp(-25f, 0f, inA), scale, inA, outAll);
            Place(swordB, imageB, 1f, Mathf.Lerp(offX, 0f, inB) + Mathf.Lerp(0f, offX * 0.6f, outAll), crossCenterY, Mathf.Lerp(25f, 0f, inB), scale, inB, outAll);

            // Golpe en el cruce: las dos espadas dan un pequeno rebote de escala (y durante la pausa respiran apenas).
            float punch = 1f + 0.05f * Mathf.Sin(Mathf.Clamp01((t - 0.62f) / 0.20f) * Mathf.PI);

            if (elapsed >= holdStart && elapsed < holdStart + hold)
                punch += 0.012f * Mathf.Sin((elapsed - holdStart) * 3.2f);
            swordA.localScale = Vector3.Scale(swordA.localScale, new Vector3(punch, punch, 1f));
            swordB.localScale = Vector3.Scale(swordB.localScale, new Vector3(punch, punch, 1f));

            // Destello dorado en el punto de impacto.
            float f = Mathf.Clamp01((t - 0.62f) / 0.20f);
            flash.gameObject.SetActive(f > 0f && f < 1f);
            flash.localScale = Vector3.one * Mathf.Lerp(0.4f, flashSize, EaseOut(f));
            flash.anchoredPosition = new Vector2(0f, crossCenterY);
            SetAlpha(flashImage, Mathf.Sin(f * Mathf.PI) * 0.85f);

            yield return null;
        }

        Destroy(canvas.gameObject);
        Release();
    }

    private static void Place(RectTransform sword, Image image, float side, float x, float y, float rotation, float scale, float appear, float fadeOut)
    {
        // 'side' = -1 espada original ("/"), +1 su reflejo ("\"). La rotacion se refleja con ella.
        sword.anchoredPosition = new Vector2(x, y);
        sword.localRotation = Quaternion.Euler(0f, 0f, rotation);
        sword.localScale = new Vector3(side < 0f ? scale : -scale, scale, 1f);
        SetAlpha(image, Mathf.Clamp01(appear * 4f) * (1f - fadeOut));
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1f - t;
        return 1f - u * u * u;
    }

    private static void SetAlpha(Image image, float alpha)
    {
        Color c = image.color;
        c.a = Mathf.Clamp01(alpha);
        image.color = c;
    }

    // ------------------------------------------------------------
    // Construccion (en ejecucion, sobre un Canvas propio)
    // ------------------------------------------------------------

    private void BuildCanvas(out Image veil, out RectTransform swordA, out RectTransform swordB,
        out Image imageA, out Image imageB, out RectTransform flash, out Image flashImage)
    {
        GameObject canvasGo = new GameObject("CanvasIntroCombate", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        // Mismo modo que el HUD (Screen Space - Camera): se dibuja sobre el HUD (orden 100) y bajo el menu de pausa (200).
        Camera cam = Camera.main;

        if (cam != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        canvas.sortingOrder = 150;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        // Fondo oscuro a pantalla completa: tambien bloquea los clics sobre el combate.
        veil = Child("Velo", canvasGo.transform, null);
        veil.color = new Color(veilColor.r, veilColor.g, veilColor.b, 0f);
        veil.raycastTarget = true;
        RectTransform veilRect = veil.rectTransform;
        veilRect.anchorMin = Vector2.zero;
        veilRect.anchorMax = Vector2.one;
        veilRect.offsetMin = veilRect.offsetMax = Vector2.zero;

        imageA = Child("EspadaIzquierda", canvasGo.transform, swordSprite);
        imageB = Child("EspadaDerecha", canvasGo.transform, swordSprite);
        flashImage = Child("Destello", canvasGo.transform, flashSprite);
        flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);

        foreach (Image sword in new[] { imageA, imageB })
        {
            sword.preserveAspect = true;
            sword.color = new Color(1f, 1f, 1f, 0f);
            sword.SetNativeSize();   // tamano nativo del sprite; la escala se aplica con localScale
        }

        flashImage.SetNativeSize();
        swordA = imageA.rectTransform;
        swordB = imageB.rectTransform;
        flash = flashImage.rectTransform;
        flash.gameObject.SetActive(false);
    }

    private static Image Child(string name, Transform parent, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        return image;
    }

    private void BuildSprites()
    {
        // Destello: degradado radial procedimental (sin archivos): blanco-dorado en el centro, transparente en el borde.
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        flashSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);   // 100 ppu = tamano nativo 128 px en el lienzo
    }
}
