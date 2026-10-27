using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReencarnacionExpress.EditorTools
{
    // Vista previa del HUD de combate SOLO para el editor.
    //  - Renderiza la camara principal de la escena abierta en un RenderTexture
    //    de 1920 x 1080 (el HUD ya esta guardado en la escena).
    //  - Superpone guias: huecos de protagonistas/enemigos, fichas de unidad y
    //    textos de MUESTRA donde el HUD estaria vacio hasta Play Mode.
    // No crea sesion, no toca GameSession, estadisticas ni inventario, no marca
    // la escena como modificada y no deja objetos: se limpia al cerrar la ventana.
    public class CombatHudPreviewWindow : EditorWindow
    {
        const int RefWidth = 1920;
        const int RefHeight = 1080;

        // Textos de muestra por nombre de objeto (solo visuales, marcados como muestra).
        static readonly Dictionary<string, string> Sample = new Dictionary<string, string>
        {
            { "Region", "Corrupción 2" },
            { "Ronda", "Ronda 1" },
            { "TurnoActivo", "Turno de Elaina" },
            { "Recursos", "Coronas 390" },
            { "Info", "Elaina    HP 95 / 95    Posición P3" },
            { "TituloHabilidad", "Disparo Certero" },
            { "Detalle", "Ataque a distancia contra un objetivo." },
            { "Ayuda", "Selecciona una habilidad o consumible." },
            { "Pocion", "Poción de curación   x1" },
            { "Antidoto", "Antídoto   x0" },
            { "Mensaje", "Mensaje de combate" },
            { "Preparacion", "Aviso de ataque preparado" },
            { "Banner", "" }
        };

        RenderTexture target;
        bool showSlots = true;
        bool showTiles = true;
        bool showSample = true;
        bool showRegions = true;
        Vector2 scroll;

        [MenuItem("Reencarnación/Combate/Vista previa del HUD (solo editor)")]
        static void Open()
        {
            GetWindow<CombatHudPreviewWindow>("HUD de combate").Show();
        }

        // Exporta la misma composicion (render 1920 x 1080 + guias de contorno) a un PNG.
        // No toca sesion ni escena; el texto de muestra solo existe en la ventana.
        public static string Export(string path)
        {
            Camera cam = Camera.main;

            if (cam == null || Application.isPlaying)
                return "Sin cámara principal o en Play Mode.";

            RenderTexture rt = new RenderTexture(RefWidth, RefHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = cam.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            cam.targetTexture = previousTarget;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(RefWidth, RefHeight, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, RefWidth, RefHeight), 0, 0);
            RenderTexture.active = previousActive;

            Vector3[] c = new Vector3[4];

            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    bool player = t.name.StartsWith("PlayerPos");
                    bool enemy = t.name.StartsWith("EnemyPos");

                    if (player || enemy)
                    {
                        Vector3 v = cam.WorldToViewportPoint(t.position);
                        DrawCross(tex, Mathf.RoundToInt(v.x * (RefWidth - 1)), Mathf.RoundToInt(v.y * (RefHeight - 1)),
                            player ? new Color(0.35f, 0.7f, 1f) : new Color(1f, 0.45f, 0.4f));
                        continue;
                    }

                    RectTransform rect = t as RectTransform;

                    if (rect == null)
                        continue;

                    bool panel = rect.name == "BarraSuperior" || rect.name == "PanelConsumibles" ||
                                 rect.name == "PanelPersonaje" || rect.name == "PanelInformacion";

                    if (!panel)
                        continue;

                    rect.GetWorldCorners(c);
                    Vector3 a = cam.WorldToViewportPoint(c[0]);
                    Vector3 b = cam.WorldToViewportPoint(c[2]);
                    DrawRect(tex, Mathf.RoundToInt(a.x * (RefWidth - 1)), Mathf.RoundToInt(a.y * (RefHeight - 1)),
                        Mathf.RoundToInt(b.x * (RefWidth - 1)), Mathf.RoundToInt(b.y * (RefHeight - 1)),
                        new Color(0.7f, 0.5f, 1f));
                }
            }

            Vector2 cardSize = CardSize();

            foreach (BattleUnit unit in FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude))
            {
                if (TryCard(cam, unit, cardSize, out Rect vr))
                    DrawRect(tex, Mathf.RoundToInt(vr.xMin * (RefWidth - 1)), Mathf.RoundToInt(vr.yMin * (RefHeight - 1)),
                        Mathf.RoundToInt(vr.xMax * (RefWidth - 1)), Mathf.RoundToInt(vr.yMax * (RefHeight - 1)),
                        new Color(0.9f, 0.9f, 0.9f));
            }

            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            DestroyImmediate(tex);
            rt.Release();
            DestroyImmediate(rt);
            return "Exportado: " + path;
        }

        // Tamano de la tarjeta de unidad (ficha) guardado en el HUD.
        static Vector2 CardSize()
        {
            foreach (RectTransform rt in FindObjectsByType<RectTransform>(FindObjectsInactive.Include))
                if (rt.name == "Casilla_0")
                    return rt.sizeDelta;

            return new Vector2(202f, 98f);
        }

        // Misma formula que CombatHudController.PlaceUnitCard: la tarjeta se coloca
        // bajo los pies de la unidad y nunca por debajo de los paneles inferiores.
        // Devuelve el rectangulo en coordenadas de viewport (0-1, origen abajo-izquierda).
        static bool TryCard(Camera cam, BattleUnit unit, Vector2 size, out Rect viewport)
        {
            viewport = default;
            SpriteRenderer sr = unit.GetComponentInChildren<SpriteRenderer>();

            if (sr == null || !cam.orthographic)
                return false;

            float ppu = RefHeight / (2f * cam.orthographicSize);
            float feet = sr.bounds.min.y + sr.bounds.size.y * 0.09f;
            float x = (unit.transform.position.x - cam.transform.position.x) * ppu;
            float y = (feet - 0.08f - cam.transform.position.y) * ppu - size.y * 0.5f;
            float minY = -RefHeight * 0.5f + RefHeight * 0.297f + 8f + size.y * 0.5f;
            y = Mathf.Max(y, minY);
            viewport = Rect.MinMaxRect(0.5f + (x - size.x * 0.5f) / RefWidth, 0.5f + (y - size.y * 0.5f) / RefHeight,
                                       0.5f + (x + size.x * 0.5f) / RefWidth, 0.5f + (y + size.y * 0.5f) / RefHeight);
            return true;
        }

        static void SetPx(Texture2D tex, int x, int y, Color color)
        {
            if (x >= 0 && y >= 0 && x < tex.width && y < tex.height)
                tex.SetPixel(x, y, color);
        }

        static void DrawRect(Texture2D tex, int x0, int y0, int x1, int y1, Color color)
        {
            for (int t = 0; t < 2; t++)
            {
                for (int x = x0; x <= x1; x++) { SetPx(tex, x, y0 + t, color); SetPx(tex, x, y1 - t, color); }
                for (int y = y0; y <= y1; y++) { SetPx(tex, x0 + t, y, color); SetPx(tex, x1 - t, y, color); }
            }
        }

        static void DrawCross(Texture2D tex, int cx, int cy, Color color)
        {
            for (int d = -22; d <= 22; d++)
            {
                for (int t = -1; t <= 1; t++)
                {
                    SetPx(tex, cx + d, cy + t, color);
                    SetPx(tex, cx + t, cy + d, color);
                }
            }
        }

        void OnDisable()
        {
            if (target != null)
            {
                target.Release();
                DestroyImmediate(target);
                target = null;
            }
        }

        void OnGUI()
        {
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("La vista previa solo funciona fuera de Play Mode.", MessageType.Info);
                return;
            }

            Scene scene = SceneManager.GetActiveScene();

            if (scene.name != "CombateIntegracion")
            {
                EditorGUILayout.HelpBox("Abre la escena CombateIntegracion para ver la vista previa del HUD.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                showSlots = GUILayout.Toggle(showSlots, "Huecos P1-P4 / E1-E4", EditorStyles.toolbarButton);
                showTiles = GUILayout.Toggle(showTiles, "Fichas de unidad", EditorStyles.toolbarButton);
                showRegions = GUILayout.Toggle(showRegions, "Paneles", EditorStyles.toolbarButton);
                showSample = GUILayout.Toggle(showSample, "Textos de muestra", EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Exportar PNG", EditorStyles.toolbarButton))
                {
                    System.IO.Directory.CreateDirectory("Docs/Capturas/Paso78");
                    Debug.Log(Export("Docs/Capturas/Paso78/HUD_vista_previa.png"));
                }

                GUILayout.Label("Referencia 1920 x 1080", EditorStyles.miniLabel);
            }

            Camera cam = Camera.main;

            if (cam == null)
            {
                EditorGUILayout.HelpBox("No se encontró la cámara principal.", MessageType.Warning);
                return;
            }

            Rect area = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            float scale = Mathf.Min(area.width / RefWidth, area.height / RefHeight);

            if (scale <= 0f)
                return;

            Rect view = new Rect(area.x + (area.width - RefWidth * scale) * 0.5f,
                                 area.y + (area.height - RefHeight * scale) * 0.5f,
                                 RefWidth * scale, RefHeight * scale);

            Render(cam);

            if (Event.current.type != EventType.Repaint)
                return;

            GUI.DrawTexture(view, target, ScaleMode.StretchToFill, false);
            DrawOverlay(cam, view);
        }

        void Render(Camera cam)
        {
            if (target == null)
                target = new RenderTexture(RefWidth, RefHeight, 24, RenderTextureFormat.ARGB32);

            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            cam.targetTexture = previous;
        }

        static Vector2 ToGui(Camera cam, Rect view, Vector3 world)
        {
            Vector3 v = cam.WorldToViewportPoint(world);
            return new Vector2(view.x + v.x * view.width, view.y + (1f - v.y) * view.height);
        }

        static Rect ToGuiRect(Camera cam, Rect view, RectTransform rt)
        {
            Vector3[] c = new Vector3[4];
            rt.GetWorldCorners(c);
            Vector2 a = ToGui(cam, view, c[0]);
            Vector2 b = ToGui(cam, view, c[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        static void Outline(Rect r, Color color)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawSolidRectangleWithOutline(r, new Color(color.r, color.g, color.b, 0.06f), color);
            Handles.EndGUI();
        }

        void DrawOverlay(Camera cam, Rect view)
        {
            GUIStyle small = new GUIStyle(EditorStyles.miniBoldLabel);
            Scene scene = SceneManager.GetActiveScene();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (showSlots)
                {
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        bool player = t.name.StartsWith("PlayerPos");
                        bool enemy = t.name.StartsWith("EnemyPos");

                        if (!player && !enemy)
                            continue;

                        Vector2 p = ToGui(cam, view, t.position);
                        Color c = player ? new Color(0.35f, 0.7f, 1f) : new Color(1f, 0.45f, 0.4f);
                        Handles.BeginGUI();
                        Handles.color = c;
                        Handles.DrawWireDisc(p, Vector3.forward, 22f * view.width / RefWidth * 2f);
                        Handles.EndGUI();
                        small.normal.textColor = c;
                        GUI.Label(new Rect(p.x - 10f, p.y + 18f, 40f, 16f), (player ? "P" : "E") + t.name.Substring(t.name.Length - 1), small);
                    }
                }

                if (showTiles)
                {
                    Vector2 cardSize = CardSize();

                    foreach (BattleUnit unit in root.GetComponentsInChildren<BattleUnit>(false))
                    {
                        if (!TryCard(cam, unit, cardSize, out Rect vr))
                            continue;

                        Rect r = Rect.MinMaxRect(view.x + vr.xMin * view.width, view.y + (1f - vr.yMax) * view.height,
                                                 view.x + vr.xMax * view.width, view.y + (1f - vr.yMin) * view.height);
                        Outline(r, new Color(0.9f, 0.9f, 0.9f));
                        small.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
                        GUI.Label(r, " Ficha: " + unit.name, small);
                    }
                }

                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas.renderMode != RenderMode.ScreenSpaceCamera)
                        continue;

                    foreach (RectTransform rt in canvas.GetComponentsInChildren<RectTransform>(true))
                    {
                        if (showRegions && (rt.name == "BarraSuperior" || rt.name == "PanelConsumibles" ||
                                            rt.name == "PanelPersonaje" || rt.name == "PanelInformacion"))
                        {
                            Outline(ToGuiRect(cam, view, rt), new Color(0.7f, 0.5f, 1f));
                        }

                        if (!showSample || !rt.gameObject.activeInHierarchy)
                            continue;

                        Text text = rt.GetComponent<Text>();

                        if (text == null || !string.IsNullOrEmpty(text.text))
                            continue;

                        if (!Sample.TryGetValue(rt.name, out string sample) || string.IsNullOrEmpty(sample))
                            continue;

                        GUIStyle ghost = new GUIStyle(EditorStyles.label);
                        ghost.fontSize = Mathf.Max(9, Mathf.RoundToInt(text.fontSize * view.width / RefWidth * 0.8f));
                        ghost.alignment = TextAnchor.MiddleLeft;
                        ghost.normal.textColor = new Color(1f, 1f, 1f, 0.45f);
                        GUI.Label(ToGuiRect(cam, view, rt), sample + "  [muestra]", ghost);
                    }
                }
            }
        }
    }
}
