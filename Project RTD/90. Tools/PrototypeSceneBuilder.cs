using ProjectRTD.Core;
using ProjectRTD.Gameplay;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PrototypeSceneBuilder
{
    private const string SceneFolder = "Assets/00. Scenes";
    private const string SquareSpritePath = "Assets/03. Sprites/PrototypeSquare.png";
    private const string PlayerPrefabPath = "Assets/02. Prefabs/Player.prefab";

    private static Sprite squareSprite;
    private static TMP_FontAsset defaultFont;

    public static string BuildAll()
    {
        EnsureTextMeshProResources();
        LoadRequiredAssets();

        BuildTitleScene();
        BuildStage01Scene();
        BuildEndingScene();
        ConfigureBuildScenes();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "Built TitleScene -> Stage01 -> EndingScene and configured Build Settings.";
    }

    private static void EnsureTextMeshProResources()
    {
        TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(
            "Assets/TextMesh Pro/Resources/TMP Settings.asset");
        if (settings == null)
        {
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();
        }

        defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
    }

    private static void LoadRequiredAssets()
    {
        squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
        if (squareSprite == null)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(SquareSpritePath);
            foreach (Object asset in assets)
            {
                if (asset is Sprite sprite)
                {
                    squareSprite = sprite;
                    break;
                }
            }
        }

        if (squareSprite == null)
        {
            throw new System.InvalidOperationException($"Missing square sprite at {SquareSpritePath}");
        }

        if (defaultFont == null)
        {
            throw new System.InvalidOperationException("TextMesh Pro Essentials could not be imported.");
        }
    }

    private static void BuildTitleScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;

        Camera camera = CreateCamera("Main Camera", false);
        Canvas canvas = CreateCanvas("Title Canvas", camera, 0);
        CreateEventSystem();

        CreateStretchImage(canvas.transform, "Backdrop", PrototypePalette.Backdrop, Vector2.zero, Vector2.zero);
        CreateAnchoredImage(canvas.transform, "Upper Route", new Vector2(0f, 120f), new Vector2(1100f, 22f), PrototypePalette.Platform);
        CreateAnchoredImage(canvas.transform, "Lower Route", new Vector2(-40f, -95f), new Vector2(1250f, 18f), PrototypePalette.Water);
        CreateAnchoredImage(canvas.transform, "Tower", new Vector2(290f, 22f), new Vector2(90f, 310f), PrototypePalette.Platform);
        CreateAnchoredImage(canvas.transform, "Player Body", new Vector2(-420f, 10f), new Vector2(78f, 108f), PrototypePalette.Player);
        CreateAnchoredImage(canvas.transform, "Goal", new Vector2(545f, -18f), new Vector2(90f, 160f), PrototypePalette.Goal);

        CreateUIText(
            canvas.transform,
            "Game Title",
            "PROJECT RTD",
            new Vector2(0f, 255f),
            new Vector2(1100f, 150f),
            92f,
            PrototypePalette.Goal,
            TextAlignmentOptions.Center);

        CreateUIText(
            canvas.transform,
            "Subtitle",
            "MIX THE PARTS. FIND THE ROUTE.",
            new Vector2(0f, 160f),
            new Vector2(900f, 70f),
            30f,
            PrototypePalette.Surface,
            TextAlignmentOptions.Center);

        GameObject flowObject = new("Screen Flow");
        ScreenFlowController flow = flowObject.AddComponent<ScreenFlowController>();
        flow.Configure(ScreenFlowController.ConfirmAction.StartStage01);

        UnityEngine.UI.Button startButton = CreateButton(
            canvas.transform,
            "Start Button",
            "START STAGE 01",
            new Vector2(0f, -240f),
            new Vector2(430f, 92f));
        UnityEventTools.AddPersistentListener(startButton.onClick, flow.StartStage01);

        CreateUIText(
            canvas.transform,
            "Start Prompt",
            "ENTER / SPACE",
            new Vector2(0f, -330f),
            new Vector2(500f, 55f),
            24f,
            WithAlpha(PrototypePalette.Surface, 0.78f),
            TextAlignmentOptions.Center);

        CreateUIText(
            canvas.transform,
            "Debug Help",
            "DEBUG  1: STAGE 01   2-4: RESERVED   F1: TITLE   F9: ENDING",
            new Vector2(0f, -485f),
            new Vector2(1300f, 45f),
            20f,
            WithAlpha(PrototypePalette.Surface, 0.65f),
            TextAlignmentOptions.Center);

        EditorSceneManager.SaveScene(scene, $"{SceneFolder}/{SceneFlow.TitleScene}.unity");
    }

    private static void BuildStage01Scene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;

        Camera camera = CreateCamera("Main Camera", true);
        CreateGlobalLight();

        GameObject stageRoot = new("Stage 01");
        GameObject environment = CreateChild(stageRoot.transform, "Environment");
        GameObject upperRoute = CreateChild(stageRoot.transform, "Upper Route");
        GameObject lowerRoute = CreateChild(stageRoot.transform, "Lower Route");
        GameObject mechanics = CreateChild(stageRoot.transform, "Mechanics");
        GameObject goalRoot = CreateChild(stageRoot.transform, "Goal");

        Transform spawnPoint = CreateChild(stageRoot.transform, "Spawn Point").transform;
        spawnPoint.position = new Vector3(-21f, 2.65f, 0f);

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
        {
            throw new System.InvalidOperationException($"Missing player prefab at {PlayerPrefabPath}");
        }

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, stageRoot.transform);
        player.name = "Player";
        player.transform.position = spawnPoint.position;

        StageRuntime runtime = stageRoot.AddComponent<StageRuntime>();
        runtime.Configure(player.transform, spawnPoint);

        CreateGameplayCamera(player.transform);

        CreatePlatform(environment.transform, "Start Platform", new Vector2(-20f, 1.55f), new Vector2(6f, 0.8f), PrototypePalette.Platform);
        CreatePlatform(upperRoute.transform, "Head Ledge A", new Vector2(-15.8f, 3.05f), new Vector2(3.5f, 0.65f), PrototypePalette.Platform);
        CreatePlatform(upperRoute.transform, "Head Ledge B", new Vector2(-11.7f, 1.55f), new Vector2(4.5f, 0.65f), PrototypePalette.Platform);
        CreateWorldLabel(upperRoute.transform, "HEAD ROUTE", new Vector2(-14.2f, 4.15f), PrototypePalette.Danger);

        CreatePlatform(upperRoute.transform, "Weight Bridge", new Vector2(-5.4f, 0.65f), new Vector2(10.5f, 0.7f), PrototypePalette.Surface);
        CreateWorldLabel(upperRoute.transform, "WEIGHT", new Vector2(-2.2f, 2.25f), PrototypePalette.Danger);

        GameObject crate = CreateSpriteBox(
            mechanics.transform,
            "Heavy Crate",
            new Vector2(-6.7f, 1.7f),
            new Vector2(1.25f, 1.25f),
            PrototypePalette.Platform,
            true,
            3);
        Rigidbody2D crateBody = crate.AddComponent<Rigidbody2D>();
        crateBody.bodyType = RigidbodyType2D.Dynamic;
        crateBody.mass = 3f;
        crateBody.gravityScale = 3f;
        crateBody.constraints = RigidbodyConstraints2D.FreezeRotation;

        GameObject door = CreatePlatform(
            mechanics.transform,
            "Weight Door",
            new Vector2(1.2f, 2.75f),
            new Vector2(0.85f, 4.1f),
            PrototypePalette.Danger);
        Vector3 closedDoorPosition = door.transform.position;
        Vector3 openDoorPosition = closedDoorPosition + Vector3.down * 5f;

        GameObject plate = CreateSpriteBox(
            mechanics.transform,
            "Mass Pressure Plate",
            new Vector2(-1.8f, 1.17f),
            new Vector2(2.2f, 0.28f),
            PrototypePalette.Danger,
            false,
            4);
        BoxCollider2D plateCollider = plate.AddComponent<BoxCollider2D>();
        plateCollider.isTrigger = true;
        MassPressurePlate2D pressurePlate = plate.AddComponent<MassPressurePlate2D>();
        pressurePlate.Configure(door.transform, closedDoorPosition, openDoorPosition, 2.5f);

        CreatePlatform(upperRoute.transform, "Jump Step A", new Vector2(3.1f, 1.7f), new Vector2(2.2f, 0.55f), PrototypePalette.Platform);
        CreatePlatform(upperRoute.transform, "Jump Step B", new Vector2(4.9f, 2.85f), new Vector2(2.2f, 0.55f), PrototypePalette.Platform);
        CreateWorldLabel(upperRoute.transform, "JUMP / SPEED", new Vector2(4.7f, 4.0f), PrototypePalette.Danger);

        CreatePlatform(environment.transform, "Central Tower", new Vector2(8f, -0.8f), new Vector2(4f, 10f), PrototypePalette.Platform);
        CreatePlatform(upperRoute.transform, "Tower Top", new Vector2(8f, 4.35f), new Vector2(4.5f, 0.55f), PrototypePalette.Platform);
        CreatePlatform(upperRoute.transform, "Exit Ledge A", new Vector2(12.5f, 2.7f), new Vector2(3f, 0.55f), PrototypePalette.Platform);
        CreatePlatform(upperRoute.transform, "Exit Ledge B", new Vector2(16f, 1.35f), new Vector2(4f, 0.65f), PrototypePalette.Platform);

        CreateSpriteBox(
            lowerRoute.transform,
            "Water Basin",
            new Vector2(-14f, -7.2f),
            new Vector2(14f, 3.4f),
            WithAlpha(PrototypePalette.Water, 0.82f),
            false,
            -4);
        CreateWorldLabel(lowerRoute.transform, "BODY ROUTE", new Vector2(-14f, -4.15f), PrototypePalette.Water);
        CreatePlatform(lowerRoute.transform, "Water Step A", new Vector2(-18f, -4.55f), new Vector2(3.5f, 0.55f), PrototypePalette.Surface);
        CreatePlatform(lowerRoute.transform, "Water Step B", new Vector2(-13.8f, -5.05f), new Vector2(3.2f, 0.55f), PrototypePalette.Surface);
        CreatePlatform(lowerRoute.transform, "Water Step C", new Vector2(-9.8f, -4.8f), new Vector2(3.5f, 0.55f), PrototypePalette.Surface);
        CreatePlatform(lowerRoute.transform, "Lower Floor A", new Vector2(-5.2f, -5.65f), new Vector2(5.5f, 0.7f), PrototypePalette.Platform);
        CreatePlatform(lowerRoute.transform, "Lower Floor B", new Vector2(0.9f, -5.65f), new Vector2(5.5f, 0.7f), PrototypePalette.Platform);
        CreatePlatform(lowerRoute.transform, "Lower Floor C", new Vector2(8.6f, -5.65f), new Vector2(7.5f, 0.7f), PrototypePalette.Platform);
        CreateWorldLabel(lowerRoute.transform, "HEAD CHECK", new Vector2(-4.4f, -4.35f), PrototypePalette.Water);
        CreatePlatform(lowerRoute.transform, "Right Stair A", new Vector2(13.1f, -4.45f), new Vector2(2.5f, 0.55f), PrototypePalette.Platform);
        CreatePlatform(lowerRoute.transform, "Right Stair B", new Vector2(14.9f, -3.2f), new Vector2(2.5f, 0.55f), PrototypePalette.Platform);
        CreatePlatform(lowerRoute.transform, "Right Stair C", new Vector2(16.6f, -1.9f), new Vector2(2.5f, 0.55f), PrototypePalette.Platform);

        CreatePlatform(goalRoot.transform, "Goal Platform", new Vector2(20.2f, 0.25f), new Vector2(5.5f, 0.75f), PrototypePalette.Platform);
        CreateSpriteBox(goalRoot.transform, "Goal Left Post", new Vector2(19.2f, 1.85f), new Vector2(0.35f, 2.8f), PrototypePalette.Goal, false, 2);
        CreateSpriteBox(goalRoot.transform, "Goal Right Post", new Vector2(21.2f, 1.85f), new Vector2(0.35f, 2.8f), PrototypePalette.Goal, false, 2);
        CreateSpriteBox(goalRoot.transform, "Goal Top", new Vector2(20.2f, 3.1f), new Vector2(2.35f, 0.35f), PrototypePalette.Goal, false, 2);
        GameObject goalTrigger = CreateSpriteBox(
            goalRoot.transform,
            "Exit Trigger",
            new Vector2(20.2f, 1.8f),
            new Vector2(1.6f, 2.4f),
            WithAlpha(PrototypePalette.Goal, 0.18f),
            false,
            1);
        BoxCollider2D goalCollider = goalTrigger.AddComponent<BoxCollider2D>();
        goalCollider.isTrigger = true;
        goalTrigger.AddComponent<StageGoal>();
        CreateWorldLabel(goalRoot.transform, "EXIT", new Vector2(20.2f, 3.95f), PrototypePalette.Goal);

        GameObject respawnZone = new("Respawn Zone");
        respawnZone.transform.SetParent(stageRoot.transform);
        respawnZone.transform.position = new Vector3(0f, -11.4f, 0f);
        BoxCollider2D respawnCollider = respawnZone.AddComponent<BoxCollider2D>();
        respawnCollider.size = new Vector2(70f, 2f);
        respawnCollider.isTrigger = true;
        respawnZone.AddComponent<RespawnZone>();

        CreateStageHud(camera);

        EditorSceneManager.SaveScene(scene, $"{SceneFolder}/{SceneFlow.Stage01Scene}.unity");
    }

    private static void BuildEndingScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;

        Camera camera = CreateCamera("Main Camera", false);
        Canvas canvas = CreateCanvas("Ending Canvas", camera, 0);
        CreateEventSystem();

        CreateStretchImage(canvas.transform, "Backdrop", PrototypePalette.Backdrop, Vector2.zero, Vector2.zero);
        CreateAnchoredImage(canvas.transform, "Goal Glow", new Vector2(0f, 90f), new Vector2(920f, 330f), WithAlpha(PrototypePalette.Goal, 0.12f));

        CreateUIText(
            canvas.transform,
            "Ending Title",
            "PROTOTYPE COMPLETE",
            new Vector2(0f, 170f),
            new Vector2(1200f, 140f),
            76f,
            PrototypePalette.Goal,
            TextAlignmentOptions.Center);
        CreateUIText(
            canvas.transform,
            "Ending Detail",
            "STAGE 01 CLEAR",
            new Vector2(0f, 55f),
            new Vector2(800f, 80f),
            34f,
            PrototypePalette.Surface,
            TextAlignmentOptions.Center);
        CreateUIText(
            canvas.transform,
            "Ending Note",
            "STAGES 02-04 ARE RESERVED FOR THE NEXT LEVEL PASS",
            new Vector2(0f, -40f),
            new Vector2(1100f, 70f),
            24f,
            WithAlpha(PrototypePalette.Surface, 0.72f),
            TextAlignmentOptions.Center);

        GameObject flowObject = new("Screen Flow");
        ScreenFlowController flow = flowObject.AddComponent<ScreenFlowController>();
        flow.Configure(ScreenFlowController.ConfirmAction.ReturnToTitle);

        UnityEngine.UI.Button titleButton = CreateButton(
            canvas.transform,
            "Title Button",
            "RETURN TO TITLE",
            new Vector2(0f, -225f),
            new Vector2(430f, 92f));
        UnityEventTools.AddPersistentListener(titleButton.onClick, flow.ReturnToTitle);

        CreateUIText(
            canvas.transform,
            "Return Prompt",
            "ENTER / SPACE",
            new Vector2(0f, -320f),
            new Vector2(500f, 55f),
            24f,
            WithAlpha(PrototypePalette.Surface, 0.75f),
            TextAlignmentOptions.Center);

        EditorSceneManager.SaveScene(scene, $"{SceneFolder}/{SceneFlow.EndingScene}.unity");
    }

    private static Camera CreateCamera(string name, bool addCinemachineBrain)
    {
        GameObject cameraObject = new(name);
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 6.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = PrototypePalette.Backdrop;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;

        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<UniversalAdditionalCameraData>();
        if (addCinemachineBrain)
        {
            cameraObject.AddComponent<CinemachineBrain>();
        }

        return camera;
    }

    private static void CreateGameplayCamera(Transform player)
    {
        GameObject cameraObject = new("Gameplay Camera");
        cameraObject.transform.position = player.position + Vector3.back * 10f;

        CinemachineCamera virtualCamera = cameraObject.AddComponent<CinemachineCamera>();
        virtualCamera.Follow = player;
        LensSettings lens = virtualCamera.Lens;
        lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        lens.OrthographicSize = 6.5f;
        lens.NearClipPlane = 0.1f;
        lens.FarClipPlane = 100f;
        virtualCamera.Lens = lens;

        CinemachinePositionComposer composer = cameraObject.AddComponent<CinemachinePositionComposer>();
        composer.CameraDistance = 10f;
        composer.Damping = new Vector3(0.65f, 0.22f, 0f);
        composer.TargetOffset = new Vector3(0f, 0.6f, 0f);
        composer.Composition = new ScreenComposerSettings
        {
            ScreenPosition = new Vector2(-0.12f, 0.04f),
            DeadZone = new ScreenComposerSettings.DeadZoneSettings
            {
                Enabled = true,
                Size = new Vector2(0.1f, 0.1f)
            },
            HardLimits = new ScreenComposerSettings.HardLimitSettings
            {
                Enabled = true,
                Size = new Vector2(0.75f, 0.72f),
                Offset = Vector2.zero
            }
        };
        composer.Lookahead = new LookaheadSettings
        {
            Enabled = true,
            Time = 0.65f,
            Smoothing = 6f,
            IgnoreY = true
        };
    }

    private static void CreateGlobalLight()
    {
        GameObject lightObject = new("Global Light 2D");
        Light2D light = lightObject.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;
        light.color = Color.white;
    }

    private static Canvas CreateCanvas(string name, Camera camera, int sortingOrder)
    {
        GameObject canvasObject = new(
            name,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 5f;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystem = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private static void CreateStageHud(Camera camera)
    {
        Canvas canvas = CreateCanvas("Stage HUD", camera, 10);
        CreateStretchImage(
            canvas.transform,
            "Top Scrim",
            WithAlpha(PrototypePalette.Ink, 0.72f),
            new Vector2(0f, 0.82f),
            Vector2.zero);

        CreateUITextAnchored(
            canvas.transform,
            "Stage Name",
            "STAGE 01",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(260f, -38f),
            new Vector2(420f, 72f),
            34f,
            PrototypePalette.Goal,
            TextAlignmentOptions.TopLeft);

        CreateUITextAnchored(
            canvas.transform,
            "Goal Text",
            "REACH THE EXIT",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(260f, -100f),
            new Vector2(560f, 60f),
            24f,
            PrototypePalette.Surface,
            TextAlignmentOptions.TopLeft);

        CreateUITextAnchored(
            canvas.transform,
            "Controls",
            "MOVE  A/D OR ARROWS     JUMP  SPACE     RESTART  R",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-690f, -44f),
            new Vector2(650f, 56f),
            20f,
            WithAlpha(PrototypePalette.Surface, 0.78f),
            TextAlignmentOptions.TopRight);
    }

    private static GameObject CreatePlatform(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        return CreateSpriteBox(parent, name, position, size, color, true, 0);
    }

    private static GameObject CreateSpriteBox(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        Color color,
        bool addCollider,
        int sortingOrder)
    {
        GameObject gameObject = new(name);
        gameObject.transform.SetParent(parent);
        gameObject.transform.position = new Vector3(position.x, position.y, 0f);
        gameObject.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = squareSprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        if (addCollider)
        {
            gameObject.AddComponent<BoxCollider2D>();
        }

        return gameObject;
    }

    private static void CreateWorldLabel(Transform parent, string text, Vector2 position, Color color)
    {
        GameObject labelObject = new($"Label - {text}");
        labelObject.transform.SetParent(parent);
        labelObject.transform.position = new Vector3(position.x, position.y, -0.1f);
        labelObject.transform.localScale = Vector3.one * 0.18f;

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.font = defaultFont;
        label.text = text;
        label.fontSize = 7f;
        label.fontStyle = FontStyles.Bold;
        label.characterSpacing = 3f;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.outlineWidth = 0.12f;
        label.outlineColor = PrototypePalette.Ink;
        label.rectTransform.sizeDelta = new Vector2(42f, 10f);
        label.sortingOrder = 8;
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        GameObject child = new(name);
        child.transform.SetParent(parent);
        return child;
    }

    private static Image CreateStretchImage(
        Transform parent,
        string name,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject imageObject = new(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin == Vector2.zero && anchorMax == Vector2.zero ? Vector2.zero : anchorMin;
        rect.anchorMax = anchorMin == Vector2.zero && anchorMax == Vector2.zero ? Vector2.one : anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image CreateAnchoredImage(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        GameObject imageObject = new(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateUIText(
        Transform parent,
        string name,
        string value,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment)
    {
        return CreateUITextAnchored(
            parent,
            name,
            value,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            position,
            size,
            fontSize,
            color,
            alignment);
    }

    private static TextMeshProUGUI CreateUITextAnchored(
        Transform parent,
        string name,
        string value,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = defaultFont;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 3f;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.outlineWidth = 0.1f;
        text.outlineColor = PrototypePalette.Ink;
        return text;
    }

    private static UnityEngine.UI.Button CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 position,
        Vector2 size)
    {
        GameObject buttonObject = new(name, typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = PrototypePalette.Platform;
        image.raycastTarget = true;

        UnityEngine.UI.Button button = buttonObject.GetComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = PrototypePalette.Platform;
        colors.highlightedColor = PrototypePalette.Goal;
        colors.pressedColor = PrototypePalette.Surface;
        colors.selectedColor = PrototypePalette.Goal;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        GameObject labelObject = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
        labelText.font = defaultFont;
        labelText.text = label;
        labelText.fontSize = 28f;
        labelText.fontStyle = FontStyles.Bold;
        labelText.characterSpacing = 3f;
        labelText.color = PrototypePalette.Ink;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.raycastTarget = false;

        return button;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static void ConfigureBuildScenes()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene($"{SceneFolder}/{SceneFlow.TitleScene}.unity", true),
            new EditorBuildSettingsScene($"{SceneFolder}/{SceneFlow.Stage01Scene}.unity", true),
            new EditorBuildSettingsScene($"{SceneFolder}/{SceneFlow.EndingScene}.unity", true)
        };
    }
}
