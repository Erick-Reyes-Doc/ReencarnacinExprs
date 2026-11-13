using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReencarnacionExpress.EditorTools
{
    // Encuadra la Scene View al abrir cada escena de la demo para que se pueda
    // inspeccionar sin buscar el contenido. Solo toca la camara del editor y la
    // visibilidad de Scene View (no se guarda en la escena).
    [InitializeOnLoad]
    public static class SceneViewFraming
    {
        const string AutoKey = "RE_SceneFraming_Auto";
        const string FillKey = "RE_SceneFraming_HideAspectFill";

        // Rellenos del mapa pensados para pantallas que no son 16:9; en Scene View
        // aparecen como bandas fuera del lienzo.
        const string MapFillPath = "WorldMapCanvas/MapArea/FondoBase/MundoBordes";

        static SceneViewFraming()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        public static bool Auto
        {
            get => EditorPrefs.GetBool(AutoKey, true);
            set => EditorPrefs.SetBool(AutoKey, value);
        }

        public static bool HideAspectFill
        {
            get => EditorPrefs.GetBool(FillKey, true);
            set => EditorPrefs.SetBool(FillKey, value);
        }

        static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (Application.isPlaying || !Auto || mode != OpenSceneMode.Single)
                return;

            EditorApplication.delayCall += () => Frame(scene);
        }

        [MenuItem("Reencarnación/Escenas/Encuadrar escena actual")]
        static void FrameCurrent()
        {
            Frame(SceneManager.GetActiveScene());
        }

        [MenuItem("Reencarnación/Escenas/Encuadre automático al abrir")]
        static void ToggleAuto()
        {
            Auto = !Auto;
        }

        [MenuItem("Reencarnación/Escenas/Encuadre automático al abrir", true)]
        static bool ToggleAutoValidate()
        {
            Menu.SetChecked("Reencarnación/Escenas/Encuadre automático al abrir", Auto);
            return true;
        }

        [MenuItem("Reencarnación/Escenas/Ocultar rellenos de aspecto del mapa")]
        static void ToggleFill()
        {
            HideAspectFill = !HideAspectFill;
            ApplyMapFill(SceneManager.GetActiveScene());
        }

        [MenuItem("Reencarnación/Escenas/Ocultar rellenos de aspecto del mapa", true)]
        static bool ToggleFillValidate()
        {
            Menu.SetChecked("Reencarnación/Escenas/Ocultar rellenos de aspecto del mapa", HideAspectFill);
            return true;
        }

        public static void Frame(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            SceneView view = SceneView.lastActiveSceneView;

            if (view == null)
                return;

            view.in2DMode = true;

            switch (scene.name)
            {
                case "Menu":
                case "Map":
                    // Canvas overlay: ocupa en el mundo el tamano en pixeles del Game View,
                    // asi que se encuadra su rectangulo real y no 1920 x 1080 fijos.
                    if (SceneGuides.TryGetOverlayCanvasRect(scene, out Rect canvas))
                    {
                        view.pivot = new Vector3(canvas.center.x, canvas.center.y, 0f);
                        view.size = canvas.height * 0.555f;
                    }
                    else
                    {
                        view.pivot = new Vector3(960f, 540f, 0f);
                        view.size = 600f;
                    }
                    break;
                case "CombateIntegracion":
                    // Camara ortografica de tamano 5: ve 17.78 x 10 unidades.
                    view.pivot = Vector3.zero;
                    view.size = 5.8f;
                    break;
                default:
                    return;
            }

            ApplyMapFill(scene);
            view.Repaint();
        }

        static void ApplyMapFill(Scene scene)
        {
            if (scene.name != "Map")
                return;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform fill = root.transform.Find(MapFillPath);

                if (fill == null)
                    continue;

                if (HideAspectFill)
                    SceneVisibilityManager.instance.Hide(fill.gameObject, true);
                else
                    SceneVisibilityManager.instance.Show(fill.gameObject, true);
            }
        }
    }
}
