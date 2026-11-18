#if UNITY_EDITOR
using System.IO;
using System.Linq;
using ChimeraGroove.Prologue;
using ProjectRTD.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ProjectRTD.Gameplay
{
    // Creates the comic prologue and registers it as the first build scene.
    internal static class PrologueSceneSetup
    {
        const string ScenePath = "Assets/00. Scenes/" + SceneFlow.PrologueScene + ".unity";
        static readonly Color Paper = new Color32(0xfb, 0xf3, 0xe2, 0xff);
        static readonly Color Ink = new Color32(0x1d, 0x1b, 0x26, 0xff);

        [InitializeOnLoadMethod]
        static void AutoInstall() => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenePath)) Install();
            else EnsureInBuild();
        };

        [MenuItem("Tools/RTD/Rebuild Prologue scene")]
        static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            // A scene can't be saved over while it is open: step back to the title first.
            if (UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath).isLoaded)
                EditorSceneManager.OpenScene("Assets/00. Scenes/" + SceneFlow.TitleScene + ".unity", OpenSceneMode.Single);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.orthographicSize = 5.4f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Ink;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true; // same retro TV filter as the stages
                EditorSceneManager.MoveGameObjectToScene(camera.gameObject, scene);

                var prologue = new GameObject("Prologue").AddComponent<ProloguePlayer>();
                prologue.viewCamera = camera;
                prologue.handleInput = false;
                prologue.fill = 0.78f; // leaves room for the SKIP button and hint line around tall cuts
                EditorSceneManager.MoveGameObjectToScene(prologue.gameObject, scene);

                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                EditorSceneManager.MoveGameObjectToScene(events, scene);

                var canvas = new GameObject("Prologue UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvasComponent = canvas.GetComponent<Canvas>();
                canvasComponent.renderMode = RenderMode.ScreenSpaceCamera; // UI goes through the filter too, like the stage HUD
                canvasComponent.worldCamera = camera;
                canvasComponent.planeDistance = 5f;
                canvasComponent.sortingOrder = 10000;
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                EditorSceneManager.MoveGameObjectToScene(canvas, scene);

                var button = BuildSkipButton(canvas.transform);
                BuildHint(canvas.transform);

                var flow = new GameObject("Prologue Flow").AddComponent<PrologueFlow>();
                var fields = new SerializedObject(flow);
                fields.FindProperty("player").objectReferenceValue = prologue;
                fields.FindProperty("skipButton").objectReferenceValue = button;
                fields.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MoveGameObjectToScene(flow.gameObject, scene);

                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException(ScenePath);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            EnsureInBuild();
            Debug.Log("PrologueScene ready as the first scene in the build.");
        }

        static Button BuildSkipButton(Transform canvas)
        {
            var root = Rect("Skip Button", canvas, new Vector2(-60f, -54f), new Vector2(210f, 78f));
            root.anchorMin = root.anchorMax = Vector2.one;
            root.pivot = new Vector2(1f, 1f);

            var shadow = Rect("Shadow", root, Vector2.zero, Vector2.zero);
            Stretch(shadow);
            shadow.offsetMin = new Vector2(9f, -9f); shadow.offsetMax = new Vector2(9f, -9f);
            Img(shadow, new Color(0f, 0f, 0f, 0.45f));

            var border = Rect("Border", root, Vector2.zero, Vector2.zero);
            Stretch(border);
            var borderImage = Img(border, Ink);

            var fill = Rect("Fill", border, Vector2.zero, Vector2.zero);
            Stretch(fill);
            fill.offsetMin = new Vector2(6f, 6f); fill.offsetMax = new Vector2(-6f, -6f);
            var fillImage = Img(fill, Paper);

            var label = Rect("Label", fill, Vector2.zero, Vector2.zero);
            Stretch(label);
            var text = label.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = "SKIP  >>";
            text.fontSize = 38f;
            text.fontStyle = FontStyles.Bold | FontStyles.Italic;
            text.characterSpacing = 4f;
            text.color = Ink;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = borderImage;
            button.transition = Selectable.Transition.None;
            var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;

            var fx = root.gameObject.AddComponent<ComicButtonFx>();
            var fxFields = new SerializedObject(fx);
            fxFields.FindProperty("fill").objectReferenceValue = fillImage;
            fxFields.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        static void BuildHint(Transform canvas)
        {
            var hint = Rect("Hint", canvas, new Vector2(0f, 34f), new Vector2(900f, 44f));
            hint.anchorMin = hint.anchorMax = new Vector2(0.5f, 0f);
            hint.pivot = new Vector2(0.5f, 0f);
            var text = hint.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = "CLICK / SPACE : NEXT        ESC : SKIP";
            text.fontSize = 22f;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 6f;
            text.color = new Color(Paper.r, Paper.g, Paper.b, 0.6f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }

        static void EnsureInBuild()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Count > 0 && scenes[0].path == ScenePath && scenes[0].enabled &&
                scenes.Count(scene => scene.path == ScenePath) == 1) return;
            scenes.RemoveAll(scene => scene.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static Image Img(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }
    }
}
#endif
