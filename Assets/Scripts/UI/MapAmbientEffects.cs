using System;
using UnityEngine;
using UnityEngine.UI;

// Ambiente muy sutil del mapa: pulso morado en la zona corrupta (Nido y Trono) y destellos tenues sobre el lago.
// Son Images sobre la ilustracion (no la modifican), no capturan clics y usan tiempo no escalado.
// Posiciones en pixeles del lienzo 1920x1080 con origen abajo-izquierda (igual que los marcadores).
public class MapAmbientEffects : MonoBehaviour
{
    public enum Kind { Pulse, Glint }

    [Serializable]
    public class Spot
    {
        public Image image;
        public Kind kind;
        [Range(0f, 1f)] public float alpha = 0.15f;
        [Range(0f, 1f)] public float amount = 0.5f;
        [Tooltip("Segundos de un ciclo.")] public float period = 6f;
        public float phase;
    }

    public Spot[] spots = new Spot[0];

    private void Update()
    {
        float t = Time.unscaledTime;

        foreach (Spot s in spots)
        {
            if (s == null || s.image == null)
                continue;

            float u = Mathf.Repeat(t / Mathf.Max(s.period, 0.1f) + s.phase, 1f);
            float wave = 0.5f - 0.5f * Mathf.Cos(u * Mathf.PI * 2f);   // 0..1 suave
            float a;

            if (s.kind == Kind.Pulse)
                a = s.alpha * (1f - s.amount + s.amount * wave);
            else
                a = s.alpha * Mathf.Pow(wave, 4f);   // destello breve y la mayor parte del tiempo apagado

            Color c = s.image.color;
            c.a = a;
            s.image.color = c;
        }
    }
}
