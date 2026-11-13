using System.Collections.Generic;
using UnityEngine;

// Retrato provisional de un protagonista: recorte de la cabeza de su sprite de combate (el mismo criterio que los
// retratos del orden de turnos del HUD de combate). No modifica ni crea archivos: el recorte es un Sprite en memoria.
public static class HeroPortraits
{
    private static readonly Dictionary<Sprite, Sprite> cache = new Dictionary<Sprite, Sprite>();

    public static Sprite Get(Sprite source)
    {
        if (source == null)
            return null;

        if (cache.TryGetValue(source, out Sprite cached) && cached != null)
            return cached;

        // Area visible del sprite (malla ajustada) para ignorar el margen transparente.
        Rect r = source.textureRect;
        Rect body = r;
        Vector2[] verts = source.vertices;

        if (verts != null && verts.Length > 2)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

            foreach (Vector2 v in verts)
            {
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
            }

            float ppu = source.pixelsPerUnit;
            Rect fitted = Rect.MinMaxRect(
                r.x + source.pivot.x + minX * ppu, r.y + source.pivot.y + minY * ppu,
                r.x + source.pivot.x + maxX * ppu, r.y + source.pivot.y + maxY * ppu);

            if (fitted.width > 4f && fitted.height > 4f && r.Contains(new Vector2(fitted.center.x, fitted.center.y)))
                body = fitted;
        }

        float side = Mathf.Min(body.height, body.width * 0.62f);
        float centerX = body.x + body.width * 0.42f;
        Rect crop = new Rect(
            Mathf.Clamp(centerX - side * 0.5f, body.x, Mathf.Max(body.x, body.xMax - side)),
            body.yMax - side,
            side,
            side);

        Sprite portrait = Sprite.Create(source.texture, crop, new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        portrait.name = "Retrato_" + source.name;
        cache[source] = portrait;
        return portrait;
    }
}
