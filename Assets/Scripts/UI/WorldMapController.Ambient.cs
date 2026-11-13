#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Construye en la escena guardada el ambiente sutil del mapa (vineta, pulso corrupto y destellos del lago) bajo
// MapArea/FondoBase, detras de los marcadores y de la interfaz. Repetible: borra y recrea solo "AmbienteMapa".
public partial class WorldMapController
{
    private const string VignettePath = "Assets/Sprites/UI/MapaVineta.png";
    private const string GlowPath = "Assets/Sprites/UI/LuzSuave.png";

    private static Sprite EnsureVignetteSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(VignettePath);

        if (existing != null)
            return existing;

        // Radial elíptica: centro transparente y borde azul marino (#10151E) hasta ~45 % de opacidad en las esquinas.
        const int w = 256, h = 144;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f;
                float dy = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, d)) * 0.45f;
                tex.SetPixel(x, y, new Color(0.063f, 0.082f, 0.118f, a));
            }

        tex.Apply();
        System.IO.File.WriteAllBytes(VignettePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(VignettePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(VignettePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(VignettePath);
    }

    [ContextMenu("Construir ambiente sutil del mapa")]
    public void BuildAmbientInEditor()
    {
        if (canvas == null)
            return;

        Transform area = canvas.transform.Find("MapArea");
        Transform baseGroup = area != null ? area.Find("FondoBase") : null;

        if (baseGroup == null)
        {
            Debug.LogWarning("Ambiente del mapa: no se encontro MapArea/FondoBase.");
            return;
        }

        Transform old = baseGroup.Find("AmbienteMapa");

        if (old != null)
            DestroyImmediate(old.gameObject);

        Sprite glow = AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath);
        Sprite vignette = EnsureVignetteSprite();

        if (glow == null || vignette == null)
        {
            Debug.LogWarning("Ambiente del mapa: faltan los sprites de luz o vineta.");
            return;
        }

        RectTransform root = (RectTransform)new GameObject("AmbienteMapa", typeof(RectTransform)).transform;
        root.SetParent(baseGroup, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;

        var spots = new System.Collections.Generic.List<MapAmbientEffects.Spot>();

        Image MakeImage(string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // Posicion en el lienzo 1920x1080 con origen abajo-izquierda.
        void Place2(Image img, float x, float y, float w, float h)
        {
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
        }

        // Pulso morado muy suave sobre la zona corrupta: Nido de Wyverns, Trono Sellado y la mancha general.
        var purple = new Color(0.62f, 0.30f, 0.95f, 0f);
        (string name, float x, float y, float w, float h, float alpha, float period, float phase)[] pulses =
        {
            ("Corrupcion_Nido", 1375f, 1080f - 245f, 340f, 300f, 0.34f, 5.5f, 0.00f),
            ("Corrupcion_Trono", 1760f, 1080f - 235f, 380f, 340f, 0.32f, 6.5f, 0.35f),
            ("Corrupcion_Zona", 1640f, 1080f - 430f, 900f, 620f, 0.13f, 9.0f, 0.65f),
        };

        foreach (var p in pulses)
        {
            Image img = MakeImage(p.name, glow, purple);
            Place2(img, p.x, p.y, p.w, p.h);
            spots.Add(new MapAmbientEffects.Spot { image = img, kind = MapAmbientEffects.Kind.Pulse, alpha = p.alpha, amount = 0.6f, period = p.period, phase = p.phase });
        }

        // Destellos tenues sobre el lago (elipse conservadora para no salirse del agua).
        var rng = new System.Random(11);
        var glint = new Color(0.90f, 0.97f, 1f, 0f);

        for (int i = 0; i < 12; i++)
        {
            double angle = rng.NextDouble() * Mathf.PI * 2.0;
            double radius = System.Math.Sqrt(rng.NextDouble());
            float x = 900f + (float)(System.Math.Cos(angle) * radius) * 230f;
            float y = 1080f - (745f + (float)(System.Math.Sin(angle) * radius) * 55f);
            float size = 16f + (float)rng.NextDouble() * 14f;
            Image img = MakeImage("Destello_" + (i + 1), glow, glint);
            Place2(img, x, y, size, size);
            spots.Add(new MapAmbientEffects.Spot
            {
                image = img, kind = MapAmbientEffects.Kind.Glint, alpha = 0.65f, amount = 1f,
                period = 2.4f + (float)rng.NextDouble() * 2.4f, phase = (float)rng.NextDouble()
            });
        }

        // Vineta: ultimo elemento del ambiente, por encima de los brillos pero por debajo de marcadores e interfaz.
        Image vig = MakeImage("Vineta", vignette, Color.white);
        vig.rectTransform.anchorMin = Vector2.zero;
        vig.rectTransform.anchorMax = Vector2.one;
        vig.rectTransform.offsetMin = vig.rectTransform.offsetMax = Vector2.zero;

        MapAmbientEffects effects = root.gameObject.AddComponent<MapAmbientEffects>();
        effects.spots = spots.ToArray();
        EditorUtility.SetDirty(this);
    }
}
#endif
