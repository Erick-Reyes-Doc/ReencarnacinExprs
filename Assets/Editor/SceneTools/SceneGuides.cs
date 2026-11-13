using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReencarnacionExpress.EditorTools
{
    // Guias dibujadas solo en Scene View (fuera de Play Mode): no crean objetos
    // ni se guardan en la escena. Muestran el area 16:9 de referencia
    // (1920 x 1080), los huecos de combate y las zonas del HUD.
    [InitializeOnLoad]
    public static class SceneGuides
    {
        const string EnabledKey = "RE_SceneGuides_Enabled";

        static readonly string[] HudRegions =
        {
            "BarraSuperior", "PanelConsumibles", "PanelPersonaje", "PanelInformacion"
        };

        static readonly List<RectTransform> hudCache = new List<RectTransform>();
        static readonly List<Transform> playerSlots = new List<Transform>();
        static readonly List<Transform> enemySlots = new List<Transform>();
        static Scene cachedScene;
        static bool dirty = true;

        static SceneGuides()
        {
            SceneView.duringSceneGui += OnSceneGui;
            EditorApplication.hierarchyChanged += () => dirty = true;
        }

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        [MenuItem("Reencarnación/Escenas/Guías de Scene View (1920x1080, slots, HUD)")]
        static void Toggle()
        {
            Enabled = !Enabled;
            SceneView.RepaintAll();
        }

        [MenuItem("Reencarnación/Escenas/Guías de Scene View (1920x1080, slots, HUD)", true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked("Reencarnación/Escenas/Guías de Scene View (1920x1080, slots, HUD)", Enabled);
            return true;
        }

        static void Refresh()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (!dirty && scene == cachedScene)
                return;

            cachedScene = scene;
            dirty = false;
            hudCache.Clear();
            playerSlots.Clear();
            enemySlots.Clear();

            if (scene.name != "CombateIntegracion")
                return;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (System.Array.IndexOf(HudRegions, rect.name) >= 0)
                        hudCache.Add(rect);
                }

                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("PlayerPos"))
                        playerSlots.Add(t);
                    else if (t.name.StartsWith("EnemyPos"))
                        enemySlots.Add(t);
                }
            }
        }

        static void OnSceneGui(SceneView view)
        {
            if (!Enabled || Application.isPlaying)
                return;

            Refresh();
            Scene scene = SceneManager.GetActiveScene();

            if (scene.name == "Menu" || scene.name == "Map")
                DrawOverlayCanvasFrame(scene);
            else if (scene.name == "CombateIntegracion")
                DrawCombat();
        }

        static void DrawFrame(Rect rect, Color color, string label)
        {
            Handles.color = color;
            Handles.DrawSolidRectangleWithOutline(rect, new Color(color.r, color.g, color.b, 0.03f), color);
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = color;
            Handles.Label(new Vector3(rect.xMin + rect.width * 0.01f, rect.yMax, 0f), label, style);
        }

        // Rectangulo, en coordenadas de Scene View, que ocupa el Canvas Overlay raiz de la escena.
        // Un Canvas Overlay se dibuja en el mundo con el tamano en PIXELES del Game View (origen
        // en 0,0), no con 1920 x 1080 fijos: el RectTransform conserva 1920 x 1080 unidades de
        // Canvas y Unity ajusta la escala (p. ej. 2 si el Game View es 3840 x 2160).
        public static bool TryGetOverlayCanvasRect(Scene scene, out Rect worldRect)
        {
            worldRect = default;
            Vector3[] c = new Vector3[4];

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                        continue;

                    ((RectTransform)canvas.transform).GetWorldCorners(c);

                    if (c[2].x - c[0].x < 1f)
                        continue;

                    worldRect = Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
                    return true;
                }
            }

            return false;
        }

        static void DrawOverlayCanvasFrame(Scene scene)
        {
            if (!TryGetOverlayCanvasRect(scene, out Rect canvas))
                return;

            // Area de referencia 16:9 (1920 x 1080 a escala del Canvas): la mayor 16:9 que cabe
            // centrada en el Canvas. Con el Game View en 16:9 coincide exactamente con el Canvas.
            float w = Mathf.Min(canvas.width, canvas.height * (16f / 9f));
            float h = w * (9f / 16f);
            Rect reference = new Rect(canvas.center.x - w * 0.5f, canvas.center.y - h * 0.5f, w, h);
            DrawFrame(reference, new Color(0.3f, 1f, 0.5f, 1f),
                "Referencia 16:9 (1920 x 1080 unidades de Canvas) - Canvas " + Mathf.RoundToInt(canvas.width) + " x " + Mathf.RoundToInt(canvas.height) + " px del Game View");
        }

        static void DrawCombat()
        {
            Camera cam = Camera.main;

            if (cam != null && cam.orthographic)
            {
                float h = cam.orthographicSize * 2f;
                float w = h * (16f / 9f);
                Vector3 c = cam.transform.position;
                DrawFrame(new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h), new Color(1f, 0.85f, 0.2f, 1f),
                    "Camara 16:9 = Game View 1920 x 1080");
            }

            foreach (Transform t in playerSlots)
                DrawSlot(t, new Color(0.35f, 0.7f, 1f, 1f), "P" + t.name.Substring(t.name.Length - 1));

            foreach (Transform t in enemySlots)
                DrawSlot(t, new Color(1f, 0.45f, 0.4f, 1f), "E" + t.name.Substring(t.name.Length - 1));

            Vector3[] corners = new Vector3[4];

            foreach (RectTransform rect in hudCache)
            {
                if (rect == null)
                    continue;

                rect.GetWorldCorners(corners);
                Rect r = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
                DrawFrame(r, new Color(0.7f, 0.5f, 1f, 1f), "HUD: " + rect.name);
            }
        }

        static void DrawSlot(Transform t, Color color, string label)
        {
            Vector3 p = t.position;
            Handles.color = color;
            Handles.DrawWireDisc(p, Vector3.forward, 0.35f);
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = color;
            Handles.Label(p + new Vector3(-0.1f, -0.45f, 0f), label, style);
        }
    }
}
