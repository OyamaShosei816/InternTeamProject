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
        private static readonly Color Ink = new Color32(37, 73, 85, 255);
        private static readonly Color Paper = new Color32(193, 228, 243, 255);
        private static Font font;
        [MenuItem("Tools/Prototype/Build Result Scene %&r")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ResultScreen.ScenePath);
            var existing = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Result UI");
            if (existing != null) Object.DestroyImmediate(existing);
            Camera.main.clearFlags = CameraClearFlags.SolidColor; Camera.main.backgroundColor = Paper;
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJKjp-Regular.otf");
            if (font == null) throw new System.InvalidOperationException("Japanese font missing.");
            var root = new GameObject("Result UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720,1280); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var content = Rect("Content", root.transform, Vector2.zero, new Vector2(720,1280));
            var title = Panel("Title", content, new Vector2(0,510), new Vector2(260,76));
            Label("Title Text", title, "リザルト", 36, Vector2.zero, new Vector2(240,70));
            var scorePanel = Panel("Score Panel", content, new Vector2(0,365), new Vector2(580,126));
            Label("Score Label", scorePanel, "スコア", 22, new Vector2(-226,40), new Vector2(100,30), TextAnchor.MiddleLeft);
            var score = Label("Score", scorePanel, "00000000", 58, new Vector2(0,-7), new Vector2(550,82));
            var summary = Rect("Breakdown", content, new Vector2(0,20), new Vector2(600,400));
            var time = Label("Time", summary, "--:--.--", 34, new Vector2(-147,100), new Vector2(270,65), TextAnchor.MiddleLeft);
            var timeBonus = Label("Time Bonus", summary, "+0", 32, new Vector2(152,100), new Vector2(280,65), TextAnchor.MiddleRight);
            var stock = Label("Stock", summary, "残り残機  --個", 30, new Vector2(-147,-35), new Vector2(285,65), TextAnchor.MiddleLeft);
            var stockBonus = Label("Stock Bonus", summary, "+0", 32, new Vector2(152,-35), new Vector2(280,65), TextAnchor.MiddleRight);
            var hint = Label("Hint", content, "タップでランキングを表示", 24, new Vector2(0,-460), new Vector2(620,55));
            var revealRect = Rect("Reveal Ranking", content, Vector2.zero, new Vector2(720,1280));
            var revealImage = revealRect.gameObject.AddComponent<Image>(); revealImage.color = Color.clear;
            var reveal = revealRect.gameObject.AddComponent<Button>(); reveal.targetGraphic = revealImage; reveal.transition = Selectable.Transition.None;
            var ranking = Rect("Ranking", content, Vector2.zero, new Vector2(640,800));
            var rankingTitle = Panel("Ranking Title", ranking, new Vector2(0,230), new Vector2(250,64));
            Label("Label", rankingTitle, "ランキング", 28, Vector2.zero, new Vector2(235,60));
            var scrollPanel = Panel("Scroll View", ranking, new Vector2(0,-105), new Vector2(600,570));
            var scroll = scrollPanel.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", scrollPanel, new Vector2(-15,0), new Vector2(548,544));
            viewport.gameObject.AddComponent<RectMask2D>();
            var rows = Rect("Rows", viewport, Vector2.zero, new Vector2(0,560));
            rows.anchorMin = new Vector2(0,1); rows.anchorMax = Vector2.one; rows.pivot = new Vector2(0.5f,1);
            scroll.viewport = viewport; scroll.content = rows;
            var track = Panel("Scrollbar", scrollPanel, new Vector2(278,0), new Vector2(20,544));
            var handle = Rect("Handle", track, Vector2.zero, Vector2.zero);
            handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one;
            var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = Ink;
            var scrollbar = track.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop; scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            var row = Panel("Row Template", rows, Vector2.zero, new Vector2(540,114));
            row.anchorMin = row.anchorMax = new Vector2(0.5f,1); row.pivot = new Vector2(0.5f,1);
            Label("Name", row, "1位  あなた", 24, new Vector2(-4,31), new Vector2(504,42), TextAnchor.MiddleLeft);
            Label("Score", row, "00000000", 38, new Vector2(-4,-16), new Vector2(504,58), TextAnchor.MiddleRight);
            row.gameObject.SetActive(false);
            var empty = Label("Empty", scrollPanel, "記録はまだありません", 26, Vector2.zero, new Vector2(540,70));
            var nextRect = Panel("Next", content, new Vector2(218,-480), new Vector2(165,70));
            var next = nextRect.gameObject.AddComponent<Button>(); next.targetGraphic = nextRect.GetComponent<Image>();
            Label("Label", nextRect, "次へ", 30, Vector2.zero, new Vector2(155,64));
            var settings = new SerializedObject(root.AddComponent<ResultScreen>());
            Set(settings,"scoreText",score); Set(settings,"timeText",time); Set(settings,"stockText",stock);
            Set(settings,"timeBonusText",timeBonus); Set(settings,"stockBonusText",stockBonus); Set(settings,"hintText",hint);
            Set(settings,"breakdown",summary.gameObject); Set(settings,"ranking",ranking.gameObject);
            Set(settings,"revealButton",reveal); Set(settings,"nextButton",next); Set(settings,"rankingContent",rows);
            Set(settings,"rowTemplate",row.gameObject); Set(settings,"emptyText",empty); Set(settings,"rankingScroll",scroll);
            settings.ApplyModifiedPropertiesWithoutUndo();
            // 編集中は最終レイアウトを表示。再生時には加点画面から始まる。
            summary.gameObject.SetActive(false); reveal.gameObject.SetActive(false); hint.text = "";
            if (Object.FindFirstObjectByType<EventSystem>() == null) new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s=>s.path==ResultScreen.ScenePath)) scenes.Add(new EditorBuildSettingsScene(ResultScreen.ScenePath,true));
            else scenes.First(s=>s.path==ResultScreen.ScenePath).enabled=true;
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log("PDF ResultScene saved.");
        }
        private static void Set(SerializedObject target,string name,Object value) { target.FindProperty(name).objectReferenceValue=value; }
        private static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent,false); r.anchorMin=r.anchorMax=r.pivot=new Vector2(0.5f,0.5f); r.anchoredPosition=pos;r.sizeDelta=size; return r;
        }
        private static RectTransform Panel(string name,Transform parent,Vector2 pos,Vector2 size)
        {
            var r=Rect(name,parent,pos,size); var image=r.gameObject.AddComponent<Image>(); image.color=Paper;
            var outline=r.gameObject.AddComponent<Outline>(); outline.effectColor=new Color32(88,119,130,255);outline.effectDistance=new Vector2(3,-3); return r;
        }
        private static Text Label(string name,Transform parent,string value,int size,Vector2 pos,Vector2 dimensions,TextAnchor alignment=TextAnchor.MiddleCenter)
        {
            var text=Rect(name,parent,pos,dimensions).gameObject.AddComponent<Text>();text.font=font;text.text=value;text.fontSize=size;
            text.color=Ink;text.alignment=alignment;text.raycastTarget=false;text.verticalOverflow=VerticalWrapMode.Overflow; return text;
        }
    }
}



