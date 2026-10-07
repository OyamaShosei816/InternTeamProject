using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Prototype.Editor
{
    public static class ResultSceneBuilder
    {
        private static readonly Color Ink = new Color32(15, 31, 39, 255);
        private static readonly Color Teal = new Color32(82, 175, 180, 255);
        private static readonly Color Paper = new Color32(230, 239, 232, 255);

        [MenuItem("Tools/Prototype/Build Result Scene %&r")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ResultScreen.ScenePath);
            var existing = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Result UI");
            if (existing != null) Object.DestroyImmediate(existing);
            var camera = Camera.main;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ink;

            var root = new GameObject("Result UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var content = Rect("Content", root.transform, Vector2.zero, new Vector2(720, 1280));
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            Label("Result Label", content, "RESULT", 24, new Vector2(0, 495), new Vector2(560, 40), Teal, font);
            var icon = Rect("Q Icon", content, new Vector2(0, 365), new Vector2(220, 220)).gameObject.AddComponent<Image>();
            icon.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Texture/UI/UI_q_icon_lv1.png").OfType<Sprite>().First();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var title = Label("Result", content, "CLEAR!", 82, new Vector2(0, 205), new Vector2(640, 110), new Color32(241, 227, 152, 255), font);
            title.fontStyle = FontStyle.BoldAndItalic;
            Line("Top Rule", content, 110);
            Label("Time Label", content, "TIME", 26, new Vector2(-165, 30), new Vector2(180, 45), Teal, font, TextAnchor.MiddleLeft);
            var time = Label("Time", content, "--:--.--", 48, new Vector2(115, 30), new Vector2(300, 70), Paper, font, TextAnchor.MiddleRight);
            Label("Stock Label", content, "STOCK", 26, new Vector2(-165, -80), new Vector2(180, 45), Teal, font, TextAnchor.MiddleLeft);
            var stock = Label("Stock", content, "--", 44, new Vector2(115, -80), new Vector2(300, 65), Paper, font, TextAnchor.MiddleRight);
            Line("Bottom Rule", content, -165);

            var buttonRect = Rect("Retry", content, new Vector2(0, -335), new Vector2(500, 100));
            var background = buttonRect.gameObject.AddComponent<Image>();
            background.color = Teal;
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.16f, 1.16f, 1.16f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.8f, 0.8f);
            button.colors = colors;
            Label("Label", buttonRect, "RETRY", 36, Vector2.zero, new Vector2(480, 90), Ink, font).fontStyle = FontStyle.Bold;
            var serialized = new SerializedObject(root.AddComponent<ResultScreen>());
            serialized.FindProperty("resultText").objectReferenceValue = title;
            serialized.FindProperty("timeText").objectReferenceValue = time;
            serialized.FindProperty("stockText").objectReferenceValue = stock;
            serialized.FindProperty("retryButton").objectReferenceValue = button;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var scenes = EditorBuildSettings.scenes.ToList();
            var entry = scenes.FirstOrDefault(s => s.path == ResultScreen.ScenePath);
            if (entry == null) scenes.Add(new EditorBuildSettingsScene(ResultScreen.ScenePath, true));
            else entry.enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log("ResultScene saved. Open Game view to preview.");
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Text Label(string name, Transform parent, string value, int size, Vector2 position,
            Vector2 dimensions, Color color, Font font, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var text = Rect(name, parent, position, dimensions).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static void Line(string name, Transform parent, float y)
        {
            var image = Rect(name, parent, new Vector2(0, y), new Vector2(530, 2)).gameObject.AddComponent<Image>();
            image.color = new Color32(57, 87, 95, 255);
            image.raycastTarget = false;
        }
    }
}
