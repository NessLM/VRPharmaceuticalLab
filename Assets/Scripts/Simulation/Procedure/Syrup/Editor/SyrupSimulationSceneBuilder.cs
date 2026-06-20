using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

[InitializeOnLoad]
public static class SyrupSimulationSceneBuilder
{
    private const string TargetScenePath = "Assets/Scenes/VRLabSimulation.unity";
    private const string RigName = "[UI] Syrup Etiket World Panel";
    private const int RigVersion = 3;

    static SyrupSimulationSceneBuilder()
    {
        EditorApplication.delayCall += TryUpgradeLoadedScene;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    [MenuItem("Tools/VR Lab/Rebuild Syrup Step 7 World UI")]
    public static void RebuildFromMenu()
    {
        BuildLoadedScene(forceRebuild: true);
    }

    private static void TryUpgradeLoadedScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        BuildLoadedScene(forceRebuild: false);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += TryUpgradeLoadedScene;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path == TargetScenePath)
            EditorApplication.delayCall += TryUpgradeLoadedScene;
    }

    private static void BuildLoadedScene(bool forceRebuild)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != TargetScenePath)
            return;

        SyrupEtiketPanelRig existingRig = FindInScene<SyrupEtiketPanelRig>(scene);
        if (!forceRebuild && existingRig != null && existingRig.SceneVersion >= RigVersion && existingRig.IsConfigured)
        {
            CalibrateLiquidVisuals(scene);
            return;
        }

        if (existingRig != null)
            Object.DestroyImmediate(existingRig.gameObject);

        GameObject uiParent = FindGameObject(scene, "UI");
        GameObject root = new GameObject(
            RigName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(TrackedDeviceGraphicRaycaster),
            typeof(LazyFollow),
            typeof(KeyboardManager),
            typeof(SyrupEtiketPanelRig));

        SceneManager.MoveGameObjectToScene(root, scene);
        if (uiParent != null)
            root.transform.SetParent(uiParent.transform, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(860f, 660f);
        rootRect.localScale = Vector3.one * 0.001f;
        rootRect.localPosition = Vector3.zero;
        rootRect.localRotation = Quaternion.identity;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 80;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        scaler.referencePixelsPerUnit = 100f;

        LazyFollow lazyFollow = root.GetComponent<LazyFollow>();
        lazyFollow.targetOffset = new Vector3(0f, -0.04f, 1.18f);
        lazyFollow.applyTargetInLocalSpace = true;
        lazyFollow.positionFollowMode = LazyFollow.PositionFollowMode.Follow;
        lazyFollow.rotationFollowMode = LazyFollow.RotationFollowMode.LookAtWithWorldUp;
        lazyFollow.movementSpeed = 4.5f;
        lazyFollow.minDistanceAllowed = 0.08f;
        lazyFollow.maxDistanceAllowed = 0.38f;
        lazyFollow.timeUntilThresholdReachesMaxDistance = 1.25f;
        lazyFollow.snapOnEnable = true;

        Image shell = CreateImage("Panel Shell", root.transform, Vector2.zero, new Vector2(840f, 640f), new Color(0.045f, 0.06f, 0.08f, 0.97f));
        CreateImage("Panel Border", shell.transform, Vector2.zero, new Vector2(828f, 628f), new Color(0.11f, 0.15f, 0.19f, 1f));
        Image content = CreateImage("Panel Content", shell.transform, Vector2.zero, new Vector2(816f, 616f), new Color(0.055f, 0.07f, 0.09f, 1f));

        GameObject choicePanel = CreatePanel("PNL Pilih Etiket", content.transform);
        CreateText("Judul", choicePanel.transform, "PILIH ETIKET OBAT", 38f, FontStyles.Bold, new Vector2(0f, 248f), new Vector2(740f, 60f), Color.white);
        CreateText(
            "Keterangan",
            choicePanel.transform,
            "Arahkan ray controller dan tekan trigger.\nPutih untuk obat dalam • Biru untuk obat luar.",
            22f,
            FontStyles.Normal,
            new Vector2(0f, 185f),
            new Vector2(740f, 70f),
            new Color(0.78f, 0.86f, 0.94f, 1f));

        Button whiteButton = CreateEtiketChoice(
            "BTN Etiket Putih",
            choicePanel.transform,
            new Vector2(-195f, -10f),
            new Color(0.97f, 0.98f, 0.96f, 1f),
            Color.black,
            "ETIKET PUTIH",
            "OBAT DALAM");

        Button blueButton = CreateEtiketChoice(
            "BTN Etiket Biru",
            choicePanel.transform,
            new Vector2(195f, -10f),
            new Color(0.16f, 0.66f, 0.86f, 1f),
            new Color(0.02f, 0.06f, 0.09f, 1f),
            "ETIKET BIRU",
            "OBAT LUAR");

        CreateText(
            "Petunjuk Bawah",
            choicePanel.transform,
            "Untuk resep sirup, pilihan yang tepat adalah etiket putih.",
            20f,
            FontStyles.Italic,
            new Vector2(0f, -248f),
            new Vector2(730f, 44f),
            new Color(1f, 0.72f, 0.22f, 1f));

        GameObject formPanel = CreatePanel("PNL Form Etiket", content.transform);
        TMP_Text formTitle = CreateText("Judul Form", formPanel.transform, "ISI ETIKET OBAT", 34f, FontStyles.Bold, new Vector2(0f, 262f), new Vector2(740f, 52f), Color.white);

        Image previewBorder = CreateImage("Preview Border", formPanel.transform, new Vector2(0f, 150f), new Vector2(710f, 166f), Color.black);
        Image previewCard = CreateImage("Preview Card", previewBorder.transform, Vector2.zero, new Vector2(694f, 150f), new Color(0.97f, 0.98f, 0.96f, 1f));
        CreateImage("Preview Divider", previewCard.transform, new Vector2(0f, 26f), new Vector2(655f, 4f), Color.black);
        TMP_Text previewHeader = CreateText("Preview Header", previewCard.transform, "ETIKET OBAT - OBAT DALAM", 25f, FontStyles.Bold, new Vector2(0f, 55f), new Vector2(650f, 38f), Color.black);
        TMP_Text previewBody = CreateText("Preview Body", previewCard.transform, "No: 001      Tgl: -\nNama: -\nUntuk: -", 19f, FontStyles.Normal, new Vector2(0f, -34f), new Vector2(640f, 92f), Color.black);
        previewBody.alignment = TextAlignmentOptions.MidlineLeft;
        previewBody.margin = new Vector4(14f, 4f, 14f, 4f);

        TMP_InputField numberInput = CreateInput("INP Nomor", formPanel.transform, "No. etiket", new Vector2(-180f, 34f), new Vector2(340f, 54f));
        TMP_InputField dateInput = CreateInput("INP Tanggal", formPanel.transform, "Tanggal", new Vector2(180f, 34f), new Vector2(340f, 54f));
        TMP_InputField nameInput = CreateInput("INP Nama", formPanel.transform, "Nama pasien", new Vector2(0f, -36f), new Vector2(700f, 54f));
        TMP_InputField usageInput = CreateInput("INP Kegunaan", formPanel.transform, "Untuk / aturan pakai", new Vector2(0f, -106f), new Vector2(700f, 54f));

        TMP_Text formStatus = CreateText(
            "Status Form",
            formPanel.transform,
            "Pilih kolom dengan ray controller untuk membuka keyboard VR.",
            18f,
            FontStyles.Normal,
            new Vector2(0f, -164f),
            new Vector2(700f, 38f),
            new Color(0.78f, 0.86f, 0.94f, 1f));

        Button chooseAgain = CreateButton("BTN Pilih Ulang", formPanel.transform, "PILIH ULANG", new Vector2(-180f, -230f), new Vector2(260f, 62f), new Color(0.22f, 0.27f, 0.33f, 1f), Color.white);
        Button createLabel = CreateButton("BTN Buat Etiket", formPanel.transform, "BUAT ETIKET", new Vector2(180f, -230f), new Vector2(260f, 62f), new Color(1f, 0.62f, 0.08f, 1f), Color.black);

        GameObject successPanel = CreatePanel("PNL Simulasi Selesai", content.transform);
        CreateText("Judul Selesai", successPanel.transform, "SIMULASI SELESAI", 48f, FontStyles.Bold, new Vector2(0f, 125f), new Vector2(720f, 72f), new Color(0.32f, 1f, 0.58f, 1f));
        CreateText(
            "Detail Selesai",
            successPanel.transform,
            "Sirup Difenhidramin 250 mg / 100 ml sudah dibuat,\ndimasukkan ke botol, dan diberi etiket.",
            25f,
            FontStyles.Normal,
            new Vector2(0f, 20f),
            new Vector2(710f, 110f),
            Color.white);
        Button backButton = CreateButton("BTN Back", successPanel.transform, "BACK", new Vector2(0f, -110f), new Vector2(280f, 70f), new Color(1f, 0.62f, 0.08f, 1f), Color.black);

        GameObject keyboardRoot = CreateKeyboard(scene, root.transform);
        KeyboardManager keyboardManager = root.GetComponent<KeyboardManager>();
        SerializedObject keyboardSo = new SerializedObject(keyboardManager);
        keyboardSo.FindProperty("_KeyboardGameobject").objectReferenceValue = keyboardRoot;
        keyboardSo.FindProperty("_NumpadGameObject").objectReferenceValue = null;
        keyboardSo.ApplyModifiedPropertiesWithoutUndo();

        SyrupEtiketPanelRig rig = root.GetComponent<SyrupEtiketPanelRig>();
        SerializedObject rigSo = new SerializedObject(rig);
        SetObject(rigSo, "worldCanvas", canvas);
        SetObject(rigSo, "lazyFollow", lazyFollow);
        SetObject(rigSo, "choicePanel", choicePanel);
        SetObject(rigSo, "formPanel", formPanel);
        SetObject(rigSo, "successPanel", successPanel);
        SetObject(rigSo, "keyboardRoot", keyboardRoot);
        SetObject(rigSo, "whiteEtiketButton", whiteButton);
        SetObject(rigSo, "blueEtiketButton", blueButton);
        SetObject(rigSo, "formTitle", formTitle);
        SetObject(rigSo, "previewCard", previewCard);
        SetObject(rigSo, "previewHeader", previewHeader);
        SetObject(rigSo, "previewBody", previewBody);
        SetObject(rigSo, "formStatus", formStatus);
        SetObject(rigSo, "numberInput", numberInput);
        SetObject(rigSo, "nameInput", nameInput);
        SetObject(rigSo, "usageInput", usageInput);
        SetObject(rigSo, "dateInput", dateInput);
        SetObject(rigSo, "chooseAgainButton", chooseAgain);
        SetObject(rigSo, "createLabelButton", createLabel);
        SetObject(rigSo, "backButton", backButton);
        SetObject(rigSo, "keyboardManager", keyboardManager);
        rigSo.FindProperty("sceneVersion").intValue = RigVersion;
        rigSo.ApplyModifiedPropertiesWithoutUndo();

        SyrupEtiketWorkflow workflow = FindInScene<SyrupEtiketWorkflow>(scene);
        if (workflow != null)
        {
            SerializedObject workflowSo = new SerializedObject(workflow);
            SetObject(workflowSo, "panelRig", rig);
            workflowSo.ApplyModifiedPropertiesWithoutUndo();
        }

        choicePanel.SetActive(false);
        formPanel.SetActive(false);
        successPanel.SetActive(false);
        keyboardRoot.SetActive(false);
        root.SetActive(false);

        CalibrateLiquidVisuals(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SyrupSceneBuilder] Step 7 world-space UI dan visual cairan selesai dibangun.");
    }

    private static void CalibrateLiquidVisuals(Scene scene)
    {
        MortarWaterVisual mortarVisual = FindInScene<MortarWaterVisual>(scene);
        if (mortarVisual != null)
        {
            Transform visualRoot = mortarVisual.transform.Find("[VIS] Mortar Liquid Container");
            if (visualRoot == null)
            {
                GameObject visualRootObject = new GameObject("[VIS] Mortar Liquid Container");
                visualRootObject.transform.SetParent(mortarVisual.transform, false);
                visualRoot = visualRootObject.transform;
            }

            Transform liquid = visualRoot.Find("LiquidVisual");
            if (liquid == null)
            {
                GameObject liquidObject = new GameObject("LiquidVisual", typeof(MeshFilter), typeof(MeshRenderer));
                liquidObject.transform.SetParent(visualRoot, false);
                liquid = liquidObject.transform;
            }

            SerializedObject visualSo = new SerializedObject(mortarVisual);
            SetObject(visualSo, "waterVisual", liquid);
            SetObject(visualSo, "waterRenderer", liquid.GetComponent<MeshRenderer>());
            visualSo.FindProperty("visualMaxWaterMl").floatValue = 200f;
            visualSo.FindProperty("emptyLocalPosition").vector3Value = new Vector3(0f, 0f, 0.0001f);
            visualSo.FindProperty("fullLocalPosition").vector3Value = new Vector3(0f, 0f, 0.00125f);
            visualSo.FindProperty("emptyLocalRadius").vector2Value = new Vector2(0.00135f, 0.00135f);
            visualSo.FindProperty("fullLocalRadius").vector2Value = new Vector2(0.00242f, 0.00242f);
            visualSo.FindProperty("emptyLocalDepth").floatValue = 0.00008f;
            visualSo.FindProperty("fullLocalDepth").floatValue = 0.0012f;
            visualSo.FindProperty("firstWaterColor").colorValue = new Color(0.48f, 0.78f, 1f, 0.38f);
            visualSo.FindProperty("mixedColor").colorValue = new Color(0.56f, 0.80f, 1f, 0.42f);
            visualSo.FindProperty("highlightColor").colorValue = new Color(0.92f, 0.98f, 1f, 0.42f);
            visualSo.ApplyModifiedPropertiesWithoutUndo();
        }

        GameObject bottle = FindGameObject(scene, "bottle");
        LiquidContainer bottleContainer = bottle != null ? bottle.GetComponent<LiquidContainer>() : null;
        if (bottleContainer != null)
        {
            SerializedObject bottleSo = new SerializedObject(bottleContainer);
            bottleSo.FindProperty("bottomLocalY").floatValue = 0.00125f;
            bottleSo.FindProperty("fullHeightLocal").floatValue = 0.0172f;
            bottleSo.FindProperty("diameterXLocal").floatValue = 0.0096f;
            bottleSo.FindProperty("diameterZLocal").floatValue = 0.0096f;
            bottleSo.FindProperty("diameterMultiplier").floatValue = 0.98f;
            bottleSo.FindProperty("rimPaddingPercent").floatValue = 0.02f;
            bottleSo.FindProperty("meshAngularSegments").intValue = 72;
            bottleSo.FindProperty("meshHeightSegments").intValue = 4;
            bottleSo.FindProperty("volumeSolveSamples").intValue = 1024;
            bottleSo.FindProperty("capSurfaceAtLowestRim").boolValue = false;
            bottleSo.FindProperty("autoSpillWhenPastRim").boolValue = false;
            bottleSo.FindProperty("spillOverflowWhenFull").boolValue = false;
            bottleSo.ApplyModifiedPropertiesWithoutUndo();

            if (bottle.GetComponent<BottleMixtureSuspension>() == null)
                bottle.AddComponent<BottleMixtureSuspension>();
        }

        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static GameObject CreateKeyboard(Scene scene, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KeyboardSample/Prefabs/Keyboard.prefab");
        GameObject keyboard;

        if (prefab != null)
            keyboard = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        else
            keyboard = new GameObject("VR Keyboard Missing Prefab");

        keyboard.name = "VR Keyboard Etiket";
        keyboard.transform.SetParent(parent, false);
        keyboard.transform.localPosition = new Vector3(0f, -455f, -28f);
        keyboard.transform.localRotation = Quaternion.Euler(24f, 0f, 0f);
        keyboard.transform.localScale = Vector3.one * 860f;
        return keyboard;
    }

    private static Button CreateEtiketChoice(
        string name,
        Transform parent,
        Vector2 position,
        Color cardColor,
        Color textColor,
        string header,
        string category)
    {
        Image border = CreateImage(name, parent, position, new Vector2(330f, 300f), Color.black);
        Button button = border.gameObject.AddComponent<Button>();
        button.targetGraphic = border;

        Image card = CreateImage("Card", border.transform, Vector2.zero, new Vector2(314f, 284f), cardColor);
        CreateText("Header", card.transform, header, 27f, FontStyles.Bold, new Vector2(0f, 104f), new Vector2(285f, 46f), textColor);
        CreateImage("Divider", card.transform, new Vector2(0f, 62f), new Vector2(276f, 4f), Color.black);
        TMP_Text body = CreateText(
            "Isi",
            card.transform,
            $"No: ____      Tgl: ____\n\nNama: ______________\n\nUntuk: _____________\n\n<b>{category}</b>",
            18f,
            FontStyles.Normal,
            new Vector2(0f, -52f),
            new Vector2(282f, 205f),
            textColor);
        body.alignment = TextAlignmentOptions.MidlineLeft;
        body.margin = new Vector4(12f, 4f, 12f, 4f);
        return button;
    }

    private static TMP_InputField CreateInput(string name, Transform parent, string placeholderText, Vector2 position, Vector2 size)
    {
        RectTransform root = CreateRect(name, parent, position, size);
        Image background = root.gameObject.AddComponent<Image>();
        background.color = new Color(0.94f, 0.96f, 0.98f, 1f);

        TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Standard;

        RectTransform viewport = CreateRect("Text Area", root, Vector2.zero, size - new Vector2(22f, 12f));
        TMP_Text placeholder = CreateText("Placeholder", viewport, placeholderText, 21f, FontStyles.Italic, Vector2.zero, viewport.sizeDelta, new Color(0.35f, 0.4f, 0.46f, 0.8f));
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        TMP_Text text = CreateText("Text", viewport, string.Empty, 22f, FontStyles.Normal, Vector2.zero, viewport.sizeDelta, new Color(0.03f, 0.045f, 0.06f, 1f));
        text.alignment = TextAlignmentOptions.MidlineLeft;

        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    private static GameObject CreatePanel(string name, Transform parent)
    {
        return CreateRect(name, parent, Vector2.zero, Vector2.zero, stretch: true).gameObject;
    }

    private static Image CreateImage(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        RectTransform rect = CreateRect(name, parent, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size, Color background, Color foreground)
    {
        Image image = CreateImage(name, parent, position, size, background);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        CreateText("Text", image.transform, label, 23f, FontStyles.Bold, Vector2.zero, size - new Vector2(12f, 10f), foreground);
        return button;
    }

    private static TMP_Text CreateText(string name, Transform parent, string value, float fontSize, FontStyles style, Vector2 position, Vector2 size, Color color)
    {
        RectTransform rect = CreateRect(name, parent, position, size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size, bool stretch = false)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        if (stretch)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        else
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        return rect;
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T result = root.GetComponentInChildren<T>(true);
            if (result != null)
                return result;
        }

        return null;
    }

    private static GameObject FindGameObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
            {
                if (candidate.name == objectName)
                    return candidate.gameObject;
            }
        }

        return null;
    }
}
