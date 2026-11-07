using System;
using UnityEngine;
using UnityEngine.UI;

// Luces ambientales del menu principal: parpadeo del farol y las lamparas, titileo de las ventanas del castillo
// y luciernagas. Igual que MenuAmbientAnimator, vive en coordenadas de la imagen original del fondo (1670x941),
// asi que se escala con ella. No captura clics y no modifica el fondo.
[ExecuteAlways]
public class MenuAmbientLights : MonoBehaviour
{
    public enum Kind { Lantern, Lamp, Castle, Firefly }

    [Serializable]
    public class Glow
    {
        public RectTransform rect;
        public Kind kind;
        [Tooltip("Posicion en pixeles de la imagen original (origen arriba a la izquierda). Farol: desplazamiento respecto al pivote.")]
        public Vector2 pixel;
        public float size = 40f;
        public Color color = Color.white;
        [Range(0f, 1f)] public float alpha = 0.3f;
        [Range(0f, 1f)] public float amount = 0.2f;
        public float speed = 1f;
        public float seed;
    }

    public RectTransform background;
    public float backgroundPixelWidth = 1670f;
    public Glow[] glows = new Glow[0];

    private void LateUpdate()
    {
        if (background == null || glows == null)
            return;

        float k = background.rect.width / Mathf.Max(backgroundPixelWidth, 1f);

        if (k <= 0f)
            return;

        float t = Application.isPlaying ? Time.unscaledTime : 0f;

        foreach (Glow g in glows)
        {
            if (g == null || g.rect == null)
                continue;

            float noise = Mathf.PerlinNoise(t * g.speed + g.seed * 13.7f, g.seed * 3.1f) * 2f - 1f;   // -1..1
            float intensity;
            Vector2 p = g.pixel;
            float size = g.size;

            switch (g.kind)
            {
                case Kind.Castle:
                    // Titileo lento e irregular de ventanas y puertas.
                    intensity = Mathf.Clamp01(1f - g.amount * (0.5f - 0.5f * noise) * 2f);
                    break;

                case Kind.Firefly:
                    p.x += 55f * Mathf.Sin(t * 0.17f * g.speed + g.seed * 3f) + 22f * Mathf.Sin(t * 0.41f * g.speed + g.seed * 7f);
                    p.y += 32f * Mathf.Sin(t * 0.23f * g.speed + g.seed * 5f) + 14f * Mathf.Sin(t * 0.53f * g.speed + g.seed * 2f);
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t * 0.8f * g.speed + g.seed * 11f);
                    intensity = 0.3f + 0.7f * pulse;
                    break;

                default:
                    // Llama: oscilacion suave de brillo y un cambio minimo de tamano.
                    intensity = 1f + g.amount * noise;
                    size *= 1f + 0.05f * noise;
                    break;
            }

            RectTransform r = g.rect;
            r.anchorMin = r.anchorMax = g.kind == Kind.Lantern ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(size * k, size * k);
            r.anchoredPosition = new Vector2(p.x * k, -p.y * k);

            Graphic graphic = r.GetComponent<Graphic>();

            if (graphic != null)
            {
                Color c = g.color;
                c.a = Mathf.Clamp01(g.alpha * intensity);
                graphic.color = c;

                Transform core = r.Find("Nucleo");

                if (core != null)
                {
                    Graphic cg = core.GetComponent<Graphic>();
                    Color cc = cg.color;
                    cc.a = Mathf.Clamp01(intensity);
                    cg.color = cc;
                }
            }
        }
    }
}
