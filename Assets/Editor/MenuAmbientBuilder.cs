using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Prepara el menu principal con el fondo nuevo, el farol y las hojas (hijos del fondo, detras de los botones).
// Es repetible: borra y vuelve a crear solo los objetos del farol y las hojas.
public static class MenuAmbientBuilder
{
    const string Folder = "Assets/Sprites/Backgrounds/MenuI/";
    const int LeafCount = 10;

    [MenuItem("Reencarnacion/Construir animaciones del menu")]
    public static void BuildAll() { Debug.Log(Build()); }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static void MakeSingle(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);

        if (importer == null || importer.spriteImportMode == SpriteImportMode.Single)
            return;

        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePivot = new Vector2(0.5f, 0.5f);
        importer.SaveAndReimport();
    }

    // Sprite de luz suave (circulo con caida cuadratica) generado una sola vez como recurso del proyecto.
    static Sprite EnsureGlowSprite()
    {
        const string path = "Assets/Sprites/UI/LuzSuave.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (existing != null)
            return existing;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }

        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static string Build()
    {
        if (EditorApplication.isPlaying)
            return "Sal de Play antes de construir.";

        foreach (string n in new[] { "Menu", "Farol", "Hojas" })
            MakeSingle(Folder + n + ".png");

        Sprite menu = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "Menu.png");
        Sprite farol = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "Farol.png");
        Sprite hojas = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "Hojas.png");

        if (menu == null || farol == null || hojas == null)
            return "Faltan sprites en " + Folder;

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity", OpenSceneMode.Single);
        GameObject bgObject = GameObject.Find("MenuBackground");

        if (bgObject == null)
            return "No existe MenuBackground.";

        Image bg = bgObject.GetComponent<Image>();
        bg.sprite = menu;
        bg.preserveAspect = false;
        var fitter = bgObject.GetComponent<AspectRatioFitter>();

        if (fitter != null)
            fitter.aspectRatio = menu.rect.width / menu.rect.height;

        RectTransform bgRect = bgObject.GetComponent<RectTransform>();

        foreach (string n in new[] { "FarolPivote", "LucesAmbiente", "HojasAmbiente" })
        {
            Transform old = bgRect.Find(n);

            if (old != null)
                Object.DestroyImmediate(old.gameObject);
        }

        // Farol: pivote en la parte alta de la cadena, hijo con la imagen.
        var pivotObject = new GameObject("FarolPivote", typeof(RectTransform));
        pivotObject.transform.SetParent(bgRect, false);
        var lanternObject = new GameObject("Farol", typeof(RectTransform), typeof(Image));
        lanternObject.transform.SetParent(pivotObject.transform, false);
        Image lanternImage = lanternObject.GetComponent<Image>();
        lanternImage.sprite = farol;
        lanternImage.raycastTarget = false;

        // Luces: farol (hijo del pivote, detras del cuerpo), lamparas, ventanas del castillo y luciernagas.
        Sprite glowSprite = EnsureGlowSprite();
        var lightsObject = new GameObject("LucesAmbiente", typeof(RectTransform));
        lightsObject.transform.SetParent(bgRect, false);
        Stretch(lightsObject.GetComponent<RectTransform>());
        var glows = new System.Collections.Generic.List<MenuAmbientLights.Glow>();

        MenuAmbientLights.Glow AddGlow(string name, Transform parent, MenuAmbientLights.Kind kind, Vector2 pixel, float size, Color color, float alpha, float amount, float speed, float seed)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.sprite = glowSprite;
            img.raycastTarget = false;
            var glow = new MenuAmbientLights.Glow
            {
                rect = go.GetComponent<RectTransform>(), kind = kind, pixel = pixel, size = size, color = color,
                alpha = alpha, amount = amount, speed = speed, seed = seed
            };
            glows.Add(glow);
            return glow;
        }

        Color flame = new Color(1f, 0.70f, 0.30f, 1f);
        Color warm = new Color(1f, 0.78f, 0.45f, 1f);
        var lanternGlow = AddGlow("Luz_Farol", pivotObject.transform, MenuAmbientLights.Kind.Lantern, new Vector2(0f, 101f), 230f, flame, 0.55f, 0.35f, 1.1f, 1.3f);
        lanternGlow.rect.SetSiblingIndex(0);

        Vector2[] lamps = { new Vector2(1388f, 677f), new Vector2(1206f, 751f), new Vector2(1137f, 770f) };
        for (int i = 0; i < lamps.Length; i++)
            AddGlow("Luz_Lampara_" + (i + 1), lightsObject.transform, MenuAmbientLights.Kind.Lamp, lamps[i], i == 0 ? 120f : 84f, flame, 0.55f, 0.35f, 1.0f, 2.1f + i * 1.7f);

        // Ventanas y puertas del castillo (px de la imagen original) con su tamano.
        Vector3[] castle =
        {
            new Vector3(1341f, 164f, 30f), new Vector3(1315f, 192f, 20f), new Vector3(1356f, 332f, 44f), new Vector3(1287f, 340f, 26f),
            new Vector3(1470f, 340f, 26f), new Vector3(1382f, 267f, 20f), new Vector3(1417f, 320f, 20f), new Vector3(1325f, 277f, 18f),
            new Vector3(1072f, 370f, 34f), new Vector3(1502f, 347f, 22f)
        };
        for (int i = 0; i < castle.Length; i++)
            AddGlow("Luz_Castillo_" + (i + 1), lightsObject.transform, MenuAmbientLights.Kind.Castle, new Vector2(castle[i].x, castle[i].y), castle[i].z * 1.5f, warm, 0.65f, 0.6f, 0.5f, 4.3f + i * 2.9f);

        // Luciernagas: posiciones repartidas de forma fija sobre la mitad baja.
        var rng = new System.Random(7);
        for (int i = 0; i < 18; i++)
        {
            var fly = AddGlow("Luciernaga_" + (i + 1), lightsObject.transform, MenuAmbientLights.Kind.Firefly,
                new Vector2(140f + (float)rng.NextDouble() * 1450f, 500f + (float)rng.NextDouble() * 380f), 30f + (float)rng.NextDouble() * 18f,
                new Color(1f, 0.82f, 0.38f, 1f), 0.9f, 0f, 0.6f + (float)rng.NextDouble() * 0.5f, 1f + i * 3.7f);
            var core = new GameObject("Nucleo", typeof(RectTransform), typeof(Image));
            core.transform.SetParent(fly.rect, false);
            Image coreImage = core.GetComponent<Image>();
            coreImage.sprite = glowSprite;
            coreImage.raycastTarget = false;
            coreImage.color = new Color(1f, 0.97f, 0.8f, 1f);
            RectTransform cr = core.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.34f, 0.34f);
            cr.anchorMax = new Vector2(0.66f, 0.66f);
            cr.offsetMin = cr.offsetMax = Vector2.zero;
        }

        // Hojas: contenedor y una Image por hoja.
        var leavesObject = new GameObject("HojasAmbiente", typeof(RectTransform));
        leavesObject.transform.SetParent(bgRect, false);
        Stretch(leavesObject.GetComponent<RectTransform>());
        var leafRects = new RectTransform[LeafCount];

        for (int i = 0; i < LeafCount; i++)
        {
            var leaf = new GameObject("Hoja_" + (i + 1), typeof(RectTransform), typeof(Image));
            leaf.transform.SetParent(leavesObject.transform, false);
            Image img = leaf.GetComponent<Image>();
            img.sprite = hojas;
            img.raycastTarget = false;
            leafRects[i] = leaf.GetComponent<RectTransform>();
        }

        var previous = bgObject.GetComponent<MenuAmbientAnimator>();

        if (previous != null)
            Object.DestroyImmediate(previous);

        var animator = (MenuAmbientAnimator)null;

        if (animator == null)
            animator = bgObject.AddComponent<MenuAmbientAnimator>();

        animator.background = bgRect;
        animator.backgroundPixelWidth = menu.rect.width;
        animator.backgroundPixelHeight = menu.rect.height;
        animator.lanternPivot = pivotObject.GetComponent<RectTransform>();
        animator.lanternNativeSize = new Vector2(farol.rect.width, farol.rect.height);
        animator.leaves = leafRects;

        var previousLights = bgObject.GetComponent<MenuAmbientLights>();

        if (previousLights != null)
            Object.DestroyImmediate(previousLights);

        var lights = bgObject.AddComponent<MenuAmbientLights>();
        lights.background = bgRect;
        lights.backgroundPixelWidth = menu.rect.width;
        lights.glows = glows.ToArray();

        EditorUtility.SetDirty(bgObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Menu listo: fondo " + menu.rect.width + "x" + menu.rect.height + ", farol " + farol.rect.width + "x" + farol.rect.height +
               ", hoja " + hojas.rect.width + "x" + hojas.rect.height + ", " + LeafCount + " hojas.";
    }
}
