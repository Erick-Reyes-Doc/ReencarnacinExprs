using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ReencarnacionExpress.EditorTools
{
    // Atajos para cambiar la resolucion de Game View (1920x1080 de referencia y
    // 1280x720 para comprobar el escalado). Usa la API interna del editor; si
    // cambia entre versiones, devuelve un mensaje en lugar de fallar.
    public static class GameViewResolution
    {
        [MenuItem("Reencarnación/Game View/1920 x 1080 (referencia)")]
        static void Ref() { Debug.Log(Set(1920, 1080)); }

        [MenuItem("Reencarnación/Game View/1280 x 720 (comprobación)")]
        static void Small() { Debug.Log(Set(1280, 720)); }

        public static string Set(int width, int height)
        {
            try
            {
                Assembly asm = typeof(Editor).Assembly;
                Type sizesType = asm.GetType("UnityEditor.GameViewSizes");
                Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                object instance = singleton.GetProperty("instance").GetValue(null);
                Type groupEnum = asm.GetType("UnityEditor.GameViewSizeGroupType");
                object standalone = Enum.ToObject(groupEnum, 0);
                object group = sizesType.GetMethod("GetGroup").Invoke(instance, new[] { standalone });
                Type groupType = group.GetType();

                int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
                Type sizeType = asm.GetType("UnityEditor.GameViewSize");
                int index = -1;

                for (int i = 0; i < total; i++)
                {
                    object s = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                    int w = (int)sizeType.GetProperty("width").GetValue(s);
                    int h = (int)sizeType.GetProperty("height").GetValue(s);
                    object kind = sizeType.GetProperty("sizeType").GetValue(s);

                    if (w == width && h == height && Convert.ToInt32(kind) == 1)
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0)
                {
                    Type kindEnum = asm.GetType("UnityEditor.GameViewSizeType");
                    object fixedRes = Enum.ToObject(kindEnum, 1);
                    object size = Activator.CreateInstance(sizeType, fixedRes, width, height, width + "x" + height);
                    groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    index = total;
                }

                Type gameView = asm.GetType("UnityEditor.GameView");
                EditorWindow window = EditorWindow.GetWindow(gameView);
                MethodInfo select = gameView.GetMethod("SizeSelectionCallback",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                select.Invoke(window, new object[] { index, null });
                window.Repaint();
                return "Game View: " + width + " x " + height;
            }
            catch (Exception e)
            {
                return "No se pudo cambiar la resolución de Game View: " + e.Message;
            }
        }
    }
}
