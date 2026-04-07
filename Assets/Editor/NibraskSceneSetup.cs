using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEditor;
using TMPro;
using Nibrask.Core;
using Nibrask.Data;
using Nibrask.AR;
using Nibrask.Navigation;
using Nibrask.UI;
using Nibrask.Feedback;

/// <summary>
/// Editor utility that automates the complete Nibrāsk scene setup.
/// Creates all ScriptableObject data, builds the scene hierarchy,
/// wires Inspector references, and creates placeholder prefabs/materials.
/// 
/// Usage: Menu → Nibrask → Setup Entire Scene
/// </summary>
public class NibraskSceneSetup : EditorWindow
{
    // ────────────────────────────────────────────────────────────────
    // Menu Entry Point
    // ────────────────────────────────────────────────────────────────

    [MenuItem("Nibrask/Setup Entire Scene")]
    public static void SetupScene()
    {
        if (!EditorUtility.DisplayDialog(
            "Nibrāsk Scene Setup",
            "This will:\n" +
            "• Create ScriptableObject data assets\n" +
            "• Clean template objects from the scene\n" +
            "• Create all Manager GameObjects\n" +
            "• Create UI Canvases and elements\n" +
            "• Create placeholder materials & arrow prefab\n" +
            "• Wire all Inspector references\n\n" +
            "Continue?",
            "Yes, Setup Everything", "Cancel"))
        {
            return;
        }

        Debug.Log("═══════════════════════════════════════════════════");
        Debug.Log("  Nibrāsk Scene Setup — Starting...");
        Debug.Log("═══════════════════════════════════════════════════");

        EnsureDirectories();

        // Phase 1: Materials (needed before prefabs)
        var arrowMat = CreateArrowMaterial();
        var pathMat = CreatePathLineMaterial();
        var glassMat = CreateGlassPanelMaterial();
        var uiBgMat = CreateUIBackgroundMaterial();

        // Phase 2: Arrow prefab
        var arrowPrefab = CreateArrowPrefab(arrowMat);

        // Phase 3: ScriptableObject data
        var destinations = CreateDestinationAssets();
        var terminalMap = CreateTerminalMapAsset(destinations);

        // Phase 4: Clean template objects
        CleanTemplateScene();

        // Phase 5: Find existing AR objects
        var xrOrigin = FindOrLogError<Unity.XR.CoreUtils.XROrigin>("XR Origin");
        if (xrOrigin == null)
        {
            // Fallback: try to find by name
            var xrOriginGO = GameObject.Find("XR Origin (AR Rig)");
            if (xrOriginGO != null)
                xrOrigin = xrOriginGO.GetComponent<Unity.XR.CoreUtils.XROrigin>();
        }
        var arSession = Object.FindAnyObjectByType<ARSession>();
        var mainCamera = Camera.main;

        // Phase 6: Add ARAnchorManager — find AR managers on XR Origin or its children
        ARPlaneManager planeManager = null;
        ARRaycastManager raycastManager = null;
        ARAnchorManager anchorManager = null;

        if (xrOrigin != null)
        {
            planeManager = xrOrigin.GetComponentInChildren<ARPlaneManager>();
            if (planeManager == null) planeManager = Object.FindAnyObjectByType<ARPlaneManager>();

            raycastManager = xrOrigin.GetComponentInChildren<ARRaycastManager>();
            if (raycastManager == null) raycastManager = Object.FindAnyObjectByType<ARRaycastManager>();

            anchorManager = xrOrigin.GetComponentInChildren<ARAnchorManager>();
            if (anchorManager == null)
            {
                anchorManager = xrOrigin.gameObject.AddComponent<ARAnchorManager>();
                Debug.Log("  ✓ Added ARAnchorManager to XR Origin");
            }
        }
        else
        {
            Debug.LogWarning("  ⚠ XR Origin not found — AR references will not be wired. Add them manually.");
        }

        // Phase 7: Create Managers
        var managersRoot = CreateEmptyGO("--- Managers ---", null);
        var appStateMgr = CreateManagerGO<AppStateManager>("AppStateManager", managersRoot.transform);
        var arEnvMgr = CreateManagerGO<AREnvironmentManager>("AREnvironmentManager", managersRoot.transform);
        var navMgr = CreateManagerGO<NavigationManager>("NavigationManager", managersRoot.transform);
        var waypointGraph = CreateManagerGO<WaypointGraph>("WaypointGraph", managersRoot.transform);
        var audioFb = CreateManagerGO<AudioFeedbackManager>("AudioFeedbackManager", managersRoot.transform);
        var hapticFb = CreateManagerGO<HapticFeedbackManager>("HapticFeedbackManager", managersRoot.transform);

        // Phase 8: Create Navigation objects
        var navRoot = CreateEmptyGO("--- Navigation ---", null);
        var pathRendererGO = CreateEmptyGO("PathRenderer", navRoot.transform);
        var pathRenderer = pathRendererGO.AddComponent<PathRenderer>();
        var lineRenderer = pathRendererGO.GetComponent<LineRenderer>(); // auto-added by RequireComponent

        var arrowContainerGO = CreateEmptyGO("ArrowContainer", navRoot.transform);
        var arrowGen = arrowContainerGO.AddComponent<ArrowGenerator>();

        var distTracker = navMgr.gameObject.AddComponent<DistanceTracker>();

        // Phase 9: Create UI
        var screenCanvas = CreateScreenSpaceCanvas();
        var onboardingUI = CreateOnboardingUI(screenCanvas.transform);
        var feedbackPanel = CreateFeedbackPanel(screenCanvas.transform);

        var worldCanvas = CreateWorldSpaceCanvas(mainCamera);
        var destMenu = CreateDestinationMenu(worldCanvas.transform);
        var gateInfo = CreateGateInfoPanel(worldCanvas.transform);
        var arrivalPanel = CreateArrivalPanel(worldCanvas.transform);

        // Phase 10: Wire all references
        Debug.Log("  Wiring Inspector references...");

        // AppStateManager
        SetSerializedField(appStateMgr, "terminalMapData", terminalMap);

        // AREnvironmentManager
        SetSerializedField(arEnvMgr, "planeManager", planeManager);
        SetSerializedField(arEnvMgr, "raycastManager", raycastManager);
        SetSerializedField(arEnvMgr, "anchorManager", anchorManager);

        // NavigationManager
        SetSerializedField(navMgr, "waypointGraph", waypointGraph);
        SetSerializedField(navMgr, "pathRenderer", pathRenderer);
        SetSerializedField(navMgr, "arrowGenerator", arrowGen);
        SetSerializedField(navMgr, "distanceTracker", distTracker);

        // DistanceTracker
        if (mainCamera != null)
        {
            SetSerializedField(distTracker, "userTransform", mainCamera.transform);
        }

        // PathRenderer
        SetSerializedField(pathRenderer, "pathMaterial", pathMat);
        if (lineRenderer != null)
        {
            lineRenderer.material = pathMat;
        }

        // ArrowGenerator
        SetSerializedField(arrowGen, "arrowPrefab", arrowPrefab);

        // OnboardingUI wiring
        WireOnboardingUI(onboardingUI.GetComponent<Nibrask.UI.OnboardingUI>(), onboardingUI);

        // FeedbackPanel wiring
        WireFeedbackPanel(feedbackPanel.GetComponent<Nibrask.UI.FeedbackPanel>(), feedbackPanel);

        // DestinationSelectionMenu wiring
        WireDestinationMenu(destMenu.GetComponent<DestinationSelectionMenu>(), destMenu);

        // GateInfoPanel wiring
        WireGateInfoPanel(gateInfo.GetComponent<Nibrask.UI.GateInfoPanel>(), gateInfo);

        // ArrivalPanel wiring
        WireArrivalPanel(arrivalPanel.GetComponent<Nibrask.UI.ArrivalPanel>(), arrivalPanel);

        // Phase 11: Set initial states
        SetInitialStates(onboardingUI, feedbackPanel, destMenu, gateInfo, arrivalPanel);

        // Done
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("═══════════════════════════════════════════════════");
        Debug.Log("  Nibrāsk Scene Setup — COMPLETE ✓");
        Debug.Log("  Save the scene (Ctrl+S) and test!");
        Debug.Log("═══════════════════════════════════════════════════");

        EditorUtility.DisplayDialog("Setup Complete",
            "Nibrāsk scene has been configured!\n\n" +
            "• 8 destination assets created in Assets/Data/\n" +
            "• Terminal map with 13 waypoints created\n" +
            "• All managers and UI wired up\n" +
            "• Arrow prefab created with primitives\n\n" +
            "Remember to save the scene (Ctrl+S).",
            "OK");
    }

    // ────────────────────────────────────────────────────────────────
    // Directory Setup
    // ────────────────────────────────────────────────────────────────

    static void EnsureDirectories()
    {
        string[] dirs = {
            "Assets/Data",
            "Assets/Materials/Navigation",
            "Assets/Materials/UI",
            "Assets/Prefabs/Navigation",
            "Assets/Prefabs/UI",
            "Assets/Audio"
        };

        foreach (var dir in dirs)
        {
            if (!AssetDatabase.IsValidFolder(dir))
            {
                var parts = dir.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }
        }
        Debug.Log("  ✓ Directories ensured");
    }

    // ────────────────────────────────────────────────────────────────
    // Materials
    // ────────────────────────────────────────────────────────────────

    static Material CreateArrowMaterial()
    {
        string path = "Assets/Materials/Navigation/ArrowMaterial.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        var mat = new Material(shader);
        mat.name = "ArrowMaterial";
        mat.SetColor("_BaseColor", new Color(0f, 0.9f, 0.3f, 0.85f));

        // Enable transparency
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_Blend", 0);
        mat.SetFloat("_AlphaClip", 0);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;

        // Emission
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0f, 0.9f, 0.3f) * 2f);

        AssetDatabase.CreateAsset(mat, path);
        Debug.Log("  ✓ Created ArrowMaterial");
        return mat;
    }

    static Material CreatePathLineMaterial()
    {
        string path = "Assets/Materials/Navigation/PathLineMaterial.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        var mat = new Material(shader);
        mat.name = "PathLineMaterial";
        mat.SetColor("_BaseColor", new Color(0f, 0.78f, 0.47f, 0.7f));

        mat.SetFloat("_Surface", 1);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;

        AssetDatabase.CreateAsset(mat, path);
        Debug.Log("  ✓ Created PathLineMaterial");
        return mat;
    }

    static Material CreateGlassPanelMaterial()
    {
        string path = "Assets/Materials/UI/GlassPanel.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        var mat = new Material(shader);
        mat.name = "GlassPanel";
        mat.SetColor("_BaseColor", new Color(0.1f, 0.12f, 0.18f, 0.75f));
        mat.SetFloat("_Surface", 1);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;

        AssetDatabase.CreateAsset(mat, path);
        Debug.Log("  ✓ Created GlassPanelMaterial");
        return mat;
    }

    static Material CreateUIBackgroundMaterial()
    {
        string path = "Assets/Materials/UI/UIBackground.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("UI/Default");
        if (shader == null) shader = Shader.Find("Standard");

        var mat = new Material(shader);
        mat.name = "UIBackground";
        mat.color = new Color(0.12f, 0.14f, 0.2f, 0.9f);

        AssetDatabase.CreateAsset(mat, path);
        Debug.Log("  ✓ Created UIBackgroundMaterial");
        return mat;
    }

    // ────────────────────────────────────────────────────────────────
    // Arrow Prefab (primitives)
    // ────────────────────────────────────────────────────────────────

    static GameObject CreateArrowPrefab(Material arrowMat)
    {
        string path = "Assets/Prefabs/Navigation/ARArrow.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var arrow = new GameObject("ARArrow");

        // Body (flat elongated cube)
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(arrow.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(0.25f, 0.04f, 0.6f);
        Object.DestroyImmediate(body.GetComponent<BoxCollider>());
        body.GetComponent<Renderer>().sharedMaterial = arrowMat;

        // Head (rotated cube as chevron)
        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        head.transform.SetParent(arrow.transform);
        head.transform.localPosition = new Vector3(0f, 0f, 0.45f);
        head.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        head.transform.localScale = new Vector3(0.3f, 0.04f, 0.3f);
        Object.DestroyImmediate(head.GetComponent<BoxCollider>());
        head.GetComponent<Renderer>().sharedMaterial = arrowMat;

        var prefab = PrefabUtility.SaveAsPrefabAsset(arrow, path);
        Object.DestroyImmediate(arrow);
        Debug.Log("  ✓ Created ARArrow prefab (primitives)");
        return prefab;
    }

    // ────────────────────────────────────────────────────────────────
    // ScriptableObject Assets
    // ────────────────────────────────────────────────────────────────

    static List<DestinationData> CreateDestinationAssets()
    {
        var list = new List<DestinationData>();

        list.Add(CreateDestination("Gate_A12", "Gate A12", DestinationType.Gate,
            new Vector3(10, 0, 0), 5, "SV-412", "14:30", "Saudia"));
        list.Add(CreateDestination("Gate_A14", "Gate A14", DestinationType.Gate,
            new Vector3(20, 0, 0), 7, "EK-203", "15:00", "Emirates"));
        list.Add(CreateDestination("Gate_B3", "Gate B3", DestinationType.Gate,
            new Vector3(10, 0, 15), 10, "QR-881", "16:15", "Qatar Airways"));
        list.Add(CreateDestination("Gate_B7", "Gate B7", DestinationType.Gate,
            new Vector3(20, 0, 15), 12, "BA-117", "17:45", "British Airways"));
        list.Add(CreateDestination("Restroom_A", "Restroom A", DestinationType.Restroom,
            new Vector3(5, 0, 7), 3));
        list.Add(CreateDestination("Restaurant_SkyLounge", "Sky Lounge Café", DestinationType.Restaurant,
            new Vector3(15, 0, 7), 8));
        list.Add(CreateDestination("Security_Checkpoint", "Security Checkpoint", DestinationType.SecurityCheckpoint,
            new Vector3(0, 0, 3), 1));
        list.Add(CreateDestination("Exit_Main", "Main Exit", DestinationType.Exit,
            new Vector3(-5, 0, 0), 0));

        Debug.Log($"  ✓ Created {list.Count} destination assets");
        return list;
    }

    static DestinationData CreateDestination(string fileName, string displayName, DestinationType type,
        Vector3 pos, int waypointIdx, string flight = "", string boarding = "", string airline = "")
    {
        string path = $"Assets/Data/{fileName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<DestinationData>(path);
        if (existing != null) return existing;

        var dest = ScriptableObject.CreateInstance<DestinationData>();
        dest.destinationName = displayName;
        dest.destinationType = type;
        dest.relativePosition = pos;
        dest.nearestWaypointIndex = waypointIdx;
        dest.flightNumber = flight;
        dest.boardingTime = boarding;
        dest.airlineName = airline;

        AssetDatabase.CreateAsset(dest, path);
        return dest;
    }

    static TerminalMapData CreateTerminalMapAsset(List<DestinationData> destinations)
    {
        string path = "Assets/Data/SampleTerminal.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TerminalMapData>(path);
        if (existing != null) return existing;

        var map = ScriptableObject.CreateInstance<TerminalMapData>();
        map.terminalName = "Terminal 1";
        map.scaleFactor = 1f;
        map.destinations = destinations;

        // Build waypoint graph
        // Layout:
        // Exit(0) -- Security(1) -- Entrance(2) -- Junction(3) -- ConcA_Start(4) -- GateA12(5) -- ConcA_Mid(6) -- GateA14(7)
        //                                              |
        //                                          Restaurant(8)
        //                                              |
        //                                         ConcB_Start(9) -- GateB3(10) -- ConcB_Mid(11) -- GateB7(12)

        map.waypoints = new List<WaypointData>
        {
            MakeWaypoint(0,  new Vector3(-5, 0, 0),    new[]{1},       false, "Exit"),
            MakeWaypoint(1,  new Vector3(0, 0, 3),     new[]{0,2,3},   false, "Security"),
            MakeWaypoint(2,  new Vector3(0, 0, 0),     new[]{1,3},     false, "Entrance"),
            MakeWaypoint(3,  new Vector3(5, 0, 7),     new[]{1,2,4,8}, false, "Junction"),
            MakeWaypoint(4,  new Vector3(5, 0, 0),     new[]{3,5},     false, "ConcA Start"),
            MakeWaypoint(5,  new Vector3(10, 0, 0),    new[]{4,6},     true,  "Gate A12"),
            MakeWaypoint(6,  new Vector3(15, 0, 0),    new[]{5,7},     false, "ConcA Mid"),
            MakeWaypoint(7,  new Vector3(20, 0, 0),    new[]{6},       true,  "Gate A14"),
            MakeWaypoint(8,  new Vector3(15, 0, 7),    new[]{3,9},     false, "Restaurant"),
            MakeWaypoint(9,  new Vector3(5, 0, 15),    new[]{8,10},    false, "ConcB Start"),
            MakeWaypoint(10, new Vector3(10, 0, 15),   new[]{9,11},    true,  "Gate B3"),
            MakeWaypoint(11, new Vector3(15, 0, 15),   new[]{10,12},   false, "ConcB Mid"),
            MakeWaypoint(12, new Vector3(20, 0, 15),   new[]{11},      true,  "Gate B7"),
        };

        AssetDatabase.CreateAsset(map, path);

        // Validate
        if (map.Validate(out string error))
        {
            Debug.Log("  ✓ Created SampleTerminal with 13 waypoints (validation passed)");
        }
        else
        {
            Debug.LogWarning($"  ⚠ Terminal map validation: {error}");
        }

        return map;
    }

    static WaypointData MakeWaypoint(int id, Vector3 pos, int[] connections, bool isDest, string label)
    {
        return new WaypointData
        {
            nodeId = id,
            relativePosition = pos,
            connectedNodeIds = new List<int>(connections),
            isDestinationNode = isDest,
            debugLabel = label
        };
    }

    // ────────────────────────────────────────────────────────────────
    // Clean Template Scene
    // ────────────────────────────────────────────────────────────────

    static void CleanTemplateScene()
    {
        string[] toDelete = {
            "Object Spawner",
            "Object Menu",
            "Object Menu Animator",
            "Create Button",
            "Delete Button",
            "Cancel Button",
            "Options Button",
            "Options Modal",
            "Debug Menu Toggle",
            "Debug Plane Toggle",
            "Greeting Prompt",
            "Hints Button",
            "Remove Objects Button",
        };

        int deleted = 0;
        foreach (var name in toDelete)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                Undo.DestroyObjectImmediate(go);
                deleted++;
            }
        }

        // Also remove the ARTemplateMenuManager and GoalManager components from any object
        var templateMenus = Object.FindObjectsByType<UnityEngine.XR.Templates.AR.ARTemplateMenuManager>(FindObjectsSortMode.None);
        foreach (var menu in templateMenus)
        {
            Undo.DestroyObjectImmediate(menu);
            deleted++;
        }

        var goalManagers = Object.FindObjectsByType<UnityEngine.XR.Templates.AR.GoalManager>(FindObjectsSortMode.None);
        foreach (var gm in goalManagers)
        {
            Undo.DestroyObjectImmediate(gm);
            deleted++;
        }

        Debug.Log($"  ✓ Cleaned template scene ({deleted} objects/components removed)");
    }

    // ────────────────────────────────────────────────────────────────
    // UI Creation — Screen Space
    // ────────────────────────────────────────────────────────────────

    static Canvas CreateScreenSpaceCanvas()
    {
        var existing = GameObject.Find("Canvas_ScreenSpace");
        if (existing != null) return existing.GetComponent<Canvas>();

        var canvasGO = new GameObject("Canvas_ScreenSpace");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        Debug.Log("  ✓ Created Canvas_ScreenSpace");
        return canvas;
    }

    static GameObject CreateOnboardingUI(Transform parent)
    {
        var onboardingGO = CreateEmptyGO("OnboardingUI", parent);
        var onboarding = onboardingGO.AddComponent<Nibrask.UI.OnboardingUI>();

        var canvasGroup = onboardingGO.AddComponent<CanvasGroup>();

        var rt = onboardingGO.AddComponent<RectTransform>();
        StretchFull(rt);

        // ── Welcome Panel ──
        var welcomePanel = CreatePanel("WelcomePanel", onboardingGO.transform,
            new Color(0.08f, 0.09f, 0.14f, 0.95f));
        StretchFull(welcomePanel.GetComponent<RectTransform>());

        var titleText = CreateTMP("TitleText", welcomePanel.transform,
            "Nibrāsk", 72, TextAlignmentOptions.Center, Color.white);
        SetRectAnchored(titleText.GetComponent<RectTransform>(),
            new Vector2(0, 0.55f), new Vector2(1, 0.75f));

        var subtitleText = CreateTMP("SubtitleText", welcomePanel.transform,
            "AR Navigation Assistant", 36, TextAlignmentOptions.Center, new Color(0.6f, 0.85f, 0.7f));
        SetRectAnchored(subtitleText.GetComponent<RectTransform>(),
            new Vector2(0, 0.45f), new Vector2(1, 0.55f));

        var instructionText = CreateTMP("InstructionText", welcomePanel.transform,
            "Navigate airport terminals\nwith augmented reality guidance", 24,
            TextAlignmentOptions.Center, new Color(0.7f, 0.7f, 0.75f));
        SetRectAnchored(instructionText.GetComponent<RectTransform>(),
            new Vector2(0.1f, 0.30f), new Vector2(0.9f, 0.45f));

        var startButton = CreateButton("StartButton", welcomePanel.transform,
            "Start Scanning", new Color(0f, 0.7f, 0.35f, 1f));
        SetRectAnchored(startButton.GetComponent<RectTransform>(),
            new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.22f));

        // ── Scanning Panel ──
        var scanningPanel = CreatePanel("ScanningPanel", onboardingGO.transform,
            new Color(0.08f, 0.09f, 0.14f, 0.85f));
        StretchFull(scanningPanel.GetComponent<RectTransform>());
        scanningPanel.SetActive(false);

        var scanInstruction = CreateTMP("ScanningInstruction", scanningPanel.transform,
            "Point your phone at the floor\nand move it slowly around", 28,
            TextAlignmentOptions.Center, Color.white);
        SetRectAnchored(scanInstruction.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.70f));

        var scanStatus = CreateTMP("ScanningStatus", scanningPanel.transform,
            "Scanning... 0%", 22, TextAlignmentOptions.Center, new Color(0.5f, 0.9f, 0.6f));
        SetRectAnchored(scanStatus.GetComponent<RectTransform>(),
            new Vector2(0.1f, 0.42f), new Vector2(0.9f, 0.52f));

        // Progress bar
        var progressBG = CreatePanel("ProgressBarBG", scanningPanel.transform,
            new Color(0.2f, 0.2f, 0.25f, 0.8f));
        SetRectAnchored(progressBG.GetComponent<RectTransform>(),
            new Vector2(0.15f, 0.35f), new Vector2(0.85f, 0.40f));

        var progressFill = CreatePanel("ProgressFill", progressBG.transform,
            new Color(0f, 0.8f, 0.4f, 1f));
        var fillRT = progressFill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(0f, 1f); // starts at 0 width
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        var fillImage = progressFill.GetComponent<Image>();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = 0f;

        Debug.Log("  ✓ Created OnboardingUI");
        return onboardingGO;
    }

    static GameObject CreateFeedbackPanel(Transform parent)
    {
        var feedbackGO = CreateEmptyGO("FeedbackPanel", parent);
        var feedback = feedbackGO.AddComponent<Nibrask.UI.FeedbackPanel>();

        var rt = feedbackGO.AddComponent<RectTransform>();
        StretchFull(rt);
        feedbackGO.SetActive(false);

        // ── Toast Container (top of screen) ──
        var toastContainer = CreateEmptyGO("ToastContainer", feedbackGO.transform);
        var toastRT = toastContainer.AddComponent<RectTransform>();
        toastRT.anchorMin = new Vector2(0.1f, 1f);
        toastRT.anchorMax = new Vector2(0.9f, 1f);
        toastRT.pivot = new Vector2(0.5f, 1f);
        toastRT.anchoredPosition = new Vector2(0, 120f); // starts off-screen
        toastRT.sizeDelta = new Vector2(0, 80f);

        var toastBG = toastContainer.AddComponent<Image>();
        toastBG.color = new Color(0f, 0.7f, 0.35f, 0.9f);

        var toastText = CreateTMP("ToastText", toastContainer.transform,
            "", 22, TextAlignmentOptions.Center, Color.white);
        StretchFull(toastText.GetComponent<RectTransform>(), new Vector2(15, 5));

        // ── Progress Bar ──
        var progressContainer = CreateEmptyGO("ProgressContainer", feedbackGO.transform);
        var progressContainerRT = progressContainer.AddComponent<RectTransform>();
        progressContainerRT.anchorMin = new Vector2(0.05f, 0.02f);
        progressContainerRT.anchorMax = new Vector2(0.95f, 0.04f);
        progressContainerRT.offsetMin = Vector2.zero;
        progressContainerRT.offsetMax = Vector2.zero;

        var progBG = progressContainer.AddComponent<Image>();
        progBG.color = new Color(0.2f, 0.2f, 0.25f, 0.6f);

        var progFill = CreatePanel("ProgressFill", progressContainer.transform,
            new Color(0f, 0.8f, 0.4f, 1f));
        var progFillRT = progFill.GetComponent<RectTransform>();
        progFillRT.anchorMin = Vector2.zero;
        progFillRT.anchorMax = new Vector2(0, 1);
        progFillRT.offsetMin = Vector2.zero;
        progFillRT.offsetMax = Vector2.zero;
        var progFillImg = progFill.GetComponent<Image>();
        progFillImg.type = Image.Type.Filled;
        progFillImg.fillMethod = Image.FillMethod.Horizontal;

        var progressText = CreateTMP("ProgressText", progressContainer.transform,
            "0/0", 14, TextAlignmentOptions.Center, Color.white);
        StretchFull(progressText.GetComponent<RectTransform>());

        // ── Off-Route Warning ──
        var offRouteWarning = CreatePanel("OffRouteWarning", feedbackGO.transform,
            new Color(0.9f, 0.2f, 0.1f, 0.85f));
        var offRouteRT = offRouteWarning.GetComponent<RectTransform>();
        offRouteRT.anchorMin = new Vector2(0, 0.92f);
        offRouteRT.anchorMax = new Vector2(1, 0.97f);
        offRouteRT.offsetMin = Vector2.zero;
        offRouteRT.offsetMax = Vector2.zero;
        offRouteWarning.SetActive(false);

        var offRouteText = CreateTMP("OffRouteText", offRouteWarning.transform,
            "Off route — recalculating...", 20, TextAlignmentOptions.Center, Color.white);
        StretchFull(offRouteText.GetComponent<RectTransform>());

        // Wire FeedbackPanel fields
        SetSerializedField(feedback, "toastContainer", toastRT);
        SetSerializedField(feedback, "toastBackground", toastBG);
        SetSerializedField(feedback, "toastText", toastText.GetComponent<TextMeshProUGUI>());
        SetSerializedField(feedback, "progressBarFill", progFillImg);
        SetSerializedField(feedback, "progressText", progressText.GetComponent<TextMeshProUGUI>());
        SetSerializedField(feedback, "offRouteWarning", offRouteWarning);
        SetSerializedField(feedback, "offRouteText", offRouteText.GetComponent<TextMeshProUGUI>());

        Debug.Log("  ✓ Created FeedbackPanel");
        return feedbackGO;
    }

    // ────────────────────────────────────────────────────────────────
    // UI Creation — World Space
    // ────────────────────────────────────────────────────────────────

    static Canvas CreateWorldSpaceCanvas(Camera cam)
    {
        var existing = GameObject.Find("Canvas_WorldSpace");
        if (existing != null) return existing.GetComponent<Canvas>();

        var canvasGO = new GameObject("Canvas_WorldSpace");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        if (cam != null)
            canvas.worldCamera = cam;

        var rt = canvasGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(800, 1000);
        rt.localScale = Vector3.one * 0.001f;
        rt.position = new Vector3(0, 1.2f, 2f);

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        Debug.Log("  ✓ Created Canvas_WorldSpace");
        return canvas;
    }

    static GameObject CreateDestinationMenu(Transform parent)
    {
        var menuGO = CreateEmptyGO("DestinationMenu", parent);
        var menu = menuGO.AddComponent<DestinationSelectionMenu>();
        var cg = menuGO.AddComponent<CanvasGroup>();
        menuGO.AddComponent<Billboard>();

        var menuRT = menuGO.AddComponent<RectTransform>();
        menuRT.sizeDelta = new Vector2(400, 600);
        menuRT.anchoredPosition = Vector2.zero;

        // Background panel
        var bg = menuGO.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.12f, 0.18f, 0.9f);

        // Header
        var header = CreateTMP("Header", menuGO.transform,
            "Select Destination", 28, TextAlignmentOptions.Center, Color.white);
        var headerRT = header.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0, 0.90f);
        headerRT.anchorMax = new Vector2(1, 1);
        headerRT.offsetMin = new Vector2(10, 0);
        headerRT.offsetMax = new Vector2(-10, -5);

        // Category labels
        var catGates = CreateTMP("CategoryGates", menuGO.transform,
            "🛫 Gates", 20, TextAlignmentOptions.Left, new Color(0.5f, 0.9f, 0.6f));
        var catGatesRT = catGates.GetComponent<RectTransform>();
        catGatesRT.anchorMin = new Vector2(0, 0.82f);
        catGatesRT.anchorMax = new Vector2(1, 0.90f);
        catGatesRT.offsetMin = new Vector2(15, 0);
        catGatesRT.offsetMax = new Vector2(-10, 0);

        var catServices = CreateTMP("CategoryServices", menuGO.transform,
            "📍 Services", 20, TextAlignmentOptions.Left, new Color(0.5f, 0.8f, 0.95f));
        var catServicesRT = catServices.GetComponent<RectTransform>();
        catServicesRT.anchorMin = new Vector2(0, 0.35f);
        catServicesRT.anchorMax = new Vector2(1, 0.43f);
        catServicesRT.offsetMin = new Vector2(15, 0);
        catServicesRT.offsetMax = new Vector2(-10, 0);

        // Button container with layout group
        var btnContainer = CreateEmptyGO("ButtonContainer", menuGO.transform);
        var btnContainerRT = btnContainer.AddComponent<RectTransform>();
        btnContainerRT.anchorMin = new Vector2(0.05f, 0.05f);
        btnContainerRT.anchorMax = new Vector2(0.95f, 0.82f);
        btnContainerRT.offsetMin = Vector2.zero;
        btnContainerRT.offsetMax = Vector2.zero;

        var vlg = btnContainer.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.padding = new RectOffset(5, 5, 5, 5);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        var csf = btnContainer.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Wire
        SetSerializedField(menu, "buttonContainer", btnContainer.transform);
        SetSerializedField(menu, "headerText", header.GetComponent<TextMeshProUGUI>());
        SetSerializedField(menu, "categoryLabelGates", catGates.GetComponent<TextMeshProUGUI>());
        SetSerializedField(menu, "categoryLabelServices", catServices.GetComponent<TextMeshProUGUI>());
        SetSerializedField(menu, "canvasGroup", cg);

        menuGO.SetActive(false);
        Debug.Log("  ✓ Created DestinationMenu (World Space)");
        return menuGO;
    }

    static GameObject CreateGateInfoPanel(Transform parent)
    {
        var panelGO = CreateEmptyGO("GateInfoPanel", parent);
        var panel = panelGO.AddComponent<Nibrask.UI.GateInfoPanel>();
        var cg = panelGO.AddComponent<CanvasGroup>();

        var panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.sizeDelta = new Vector2(350, 250);
        panelRT.anchoredPosition = new Vector2(250, -100);

        // Panel root (background)
        var panelRoot = CreatePanel("PanelRoot", panelGO.transform,
            new Color(0.1f, 0.12f, 0.18f, 0.88f));
        StretchFull(panelRoot.GetComponent<RectTransform>(), new Vector2(0, 0));

        // Destination name
        var destName = CreateTMP("DestinationName", panelRoot.transform,
            "Gate A12", 26, TextAlignmentOptions.TopLeft, Color.white);
        SetRectAnchored(destName.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.95f));

        // Flight info
        var flightInfo = CreateTMP("FlightInfo", panelRoot.transform,
            "✈️ SV-412\n🕐 Boarding: 14:30", 18, TextAlignmentOptions.TopLeft,
            new Color(0.7f, 0.8f, 0.9f));
        SetRectAnchored(flightInfo.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.50f), new Vector2(0.95f, 0.78f));

        // Distance
        var distance = CreateTMP("Distance", panelRoot.transform,
            "📏 -- m", 20, TextAlignmentOptions.Left, new Color(0.9f, 0.95f, 1f));
        SetRectAnchored(distance.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.30f), new Vector2(0.50f, 0.48f));

        // Walking time
        var walkTime = CreateTMP("WalkingTime", panelRoot.transform,
            "🚶 --:--", 20, TextAlignmentOptions.Left, new Color(0.9f, 0.95f, 1f));
        SetRectAnchored(walkTime.GetComponent<RectTransform>(),
            new Vector2(0.50f, 0.30f), new Vector2(0.95f, 0.48f));

        // Status
        var status = CreateTMP("Status", panelRoot.transform,
            "Following route", 16, TextAlignmentOptions.Center, new Color(0f, 0.9f, 0.4f));
        SetRectAnchored(status.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.25f));

        // Wire
        SetSerializedField(panel, "destinationNameText", destName.GetComponent<TextMeshProUGUI>());
        SetSerializedField(panel, "flightInfoText", flightInfo.GetComponent<TextMeshProUGUI>());
        SetSerializedField(panel, "distanceText", distance.GetComponent<TextMeshProUGUI>());
        SetSerializedField(panel, "walkingTimeText", walkTime.GetComponent<TextMeshProUGUI>());
        SetSerializedField(panel, "statusText", status.GetComponent<TextMeshProUGUI>());
        SetSerializedField(panel, "canvasGroup", cg);
        SetSerializedField(panel, "panelRoot", panelRoot);

        panelGO.SetActive(false);
        Debug.Log("  ✓ Created GateInfoPanel (World Space)");
        return panelGO;
    }

    static GameObject CreateArrivalPanel(Transform parent)
    {
        var arrivalGO = CreateEmptyGO("ArrivalPanel", parent);
        var arrival = arrivalGO.AddComponent<Nibrask.UI.ArrivalPanel>();
        var cg = arrivalGO.AddComponent<CanvasGroup>();
        arrivalGO.AddComponent<Billboard>();

        var arrivalRT = arrivalGO.AddComponent<RectTransform>();
        arrivalRT.sizeDelta = new Vector2(400, 350);
        arrivalRT.anchoredPosition = new Vector2(0, 200);

        // Background
        var bg = arrivalGO.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.12f, 0.1f, 0.92f);

        // Title
        var title = CreateTMP("ArrivalTitle", arrivalGO.transform,
            "🎉  You have arrived!", 30, TextAlignmentOptions.Center, Color.white);
        SetRectAnchored(title.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.95f));

        // Message
        var message = CreateTMP("ArrivalMessage", arrivalGO.transform,
            "Welcome to", 22, TextAlignmentOptions.Center, new Color(0.7f, 0.8f, 0.75f));
        SetRectAnchored(message.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.65f), new Vector2(0.95f, 0.78f));

        // Destination info
        var destInfo = CreateTMP("DestinationInfo", arrivalGO.transform,
            "Gate A12\nFlight: SV-412", 24, TextAlignmentOptions.Center,
            new Color(0.5f, 1f, 0.65f));
        SetRectAnchored(destInfo.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.65f));

        // Checkmark icon (green circle primitive)
        var checkmark = CreatePanel("CheckmarkIcon", arrivalGO.transform,
            new Color(0f, 0.85f, 0.35f, 1f));
        var checkRT = checkmark.GetComponent<RectTransform>();
        checkRT.anchorMin = new Vector2(0.5f, 0.42f);
        checkRT.anchorMax = new Vector2(0.5f, 0.42f);
        checkRT.sizeDelta = new Vector2(60, 60);
        checkRT.anchoredPosition = new Vector2(0, -10);
        // Add a checkmark "✓" text inside
        var checkText = CreateTMP("CheckText", checkmark.transform,
            "✓", 36, TextAlignmentOptions.Center, Color.white);
        StretchFull(checkText.GetComponent<RectTransform>());

        // Navigate again button
        var navAgainBtn = CreateButton("NavigateAgainButton", arrivalGO.transform,
            "Navigate to another destination", new Color(0.1f, 0.5f, 0.85f, 1f));
        SetRectAnchored(navAgainBtn.GetComponent<RectTransform>(),
            new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.18f));

        // Wire
        SetSerializedField(arrival, "arrivalTitleText", title.GetComponent<TextMeshProUGUI>());
        SetSerializedField(arrival, "arrivalMessageText", message.GetComponent<TextMeshProUGUI>());
        SetSerializedField(arrival, "destinationInfoText", destInfo.GetComponent<TextMeshProUGUI>());
        SetSerializedField(arrival, "checkmarkIcon", checkmark.GetComponent<Image>());
        SetSerializedField(arrival, "navigateAgainButton", navAgainBtn.GetComponent<Button>());
        SetSerializedField(arrival, "canvasGroup", cg);

        arrivalGO.SetActive(false);
        Debug.Log("  ✓ Created ArrivalPanel (World Space)");
        return arrivalGO;
    }

    // ────────────────────────────────────────────────────────────────
    // Wire remaining references
    // ────────────────────────────────────────────────────────────────

    static void WireOnboardingUI(Nibrask.UI.OnboardingUI comp, GameObject root)
    {
        var welcomePanel = root.transform.Find("WelcomePanel")?.gameObject;
        var scanningPanel = root.transform.Find("ScanningPanel")?.gameObject;

        SetSerializedField(comp, "welcomePanel", welcomePanel);
        SetSerializedField(comp, "scanningPanel", scanningPanel);

        if (welcomePanel != null)
        {
            SetSerializedField(comp, "titleText", FindChildTMP(welcomePanel, "TitleText"));
            SetSerializedField(comp, "subtitleText", FindChildTMP(welcomePanel, "SubtitleText"));
            SetSerializedField(comp, "instructionText", FindChildTMP(welcomePanel, "InstructionText"));
            var btn = welcomePanel.transform.Find("StartButton");
            if (btn != null) SetSerializedField(comp, "startButton", btn.GetComponent<Button>());
        }

        if (scanningPanel != null)
        {
            SetSerializedField(comp, "scanningStatusText", FindChildTMP(scanningPanel, "ScanningStatus"));
            SetSerializedField(comp, "scanningInstructionText", FindChildTMP(scanningPanel, "ScanningInstruction"));

            var progressBG = scanningPanel.transform.Find("ProgressBarBG");
            if (progressBG != null)
            {
                var fill = progressBG.Find("ProgressFill");
                if (fill != null) SetSerializedField(comp, "scanningProgressFill", fill.GetComponent<Image>());
            }
        }

        SetSerializedField(comp, "canvasGroup", root.GetComponent<CanvasGroup>());
    }

    static void WireFeedbackPanel(Nibrask.UI.FeedbackPanel comp, GameObject root)
    {
        // Already wired inline during creation
    }

    static void WireDestinationMenu(DestinationSelectionMenu comp, GameObject root)
    {
        // Already wired inline during creation
    }

    static void WireGateInfoPanel(Nibrask.UI.GateInfoPanel comp, GameObject root)
    {
        // Already wired inline during creation
    }

    static void WireArrivalPanel(Nibrask.UI.ArrivalPanel comp, GameObject root)
    {
        // Already wired inline during creation
    }

    // ────────────────────────────────────────────────────────────────
    // Initial States
    // ────────────────────────────────────────────────────────────────

    static void SetInitialStates(GameObject onboarding, GameObject feedback,
        GameObject destMenu, GameObject gateInfo, GameObject arrival)
    {
        // Onboarding starts active (welcome visible)
        onboarding.SetActive(true);

        // Everything else starts inactive
        feedback.SetActive(false);
        destMenu.SetActive(false);
        gateInfo.SetActive(false);
        arrival.SetActive(false);

        Debug.Log("  ✓ Set initial active states");
    }

    // ────────────────────────────────────────────────────────────────
    // Helper Utilities
    // ────────────────────────────────────────────────────────────────

    static GameObject CreateEmptyGO(string name, Transform parent)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        return go;
    }

    static T CreateManagerGO<T>(string name, Transform parent) where T : MonoBehaviour
    {
        var go = CreateEmptyGO(name, parent);
        return go.AddComponent<T>();
    }

    static T FindOrLogError<T>(string objectName) where T : Object
    {
        var obj = Object.FindAnyObjectByType<T>();
        if (obj == null)
        {
            Debug.LogError($"  ✗ Could not find {typeof(T).Name} ({objectName}) in scene!");
        }
        return obj;
    }

    static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    static GameObject CreateTMP(string name, Transform parent, string text,
        float fontSize, TextAlignmentOptions alignment, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.enableAutoSizing = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return go;
    }

    static GameObject CreateButton(string name, Transform parent, string label, Color bgColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();

        var img = go.AddComponent<Image>();
        img.color = bgColor;

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        btn.colors = colors;
        btn.targetGraphic = img;

        var textGO = CreateTMP("Label", go.transform, label, 20,
            TextAlignmentOptions.Center, Color.white);
        StretchFull(textGO.GetComponent<RectTransform>(), new Vector2(10, 5));

        // Add layout element for buttons in layout groups
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 55;

        return go;
    }

    static void StretchFull(RectTransform rt, Vector2 padding = default)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = padding;
        rt.offsetMax = -padding;
    }

    static void SetRectAnchored(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI FindChildTMP(GameObject parent, string childName)
    {
        var child = parent.transform.Find(childName);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    /// <summary>
    /// Sets a serialized private field on a MonoBehaviour using SerializedObject.
    /// Works with [SerializeField] private fields.
    /// </summary>
    static void SetSerializedField(Object target, string fieldName, Object value)
    {
        if (target == null) return;

        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            // Try with m_ prefix convention
            prop = so.FindProperty("m_" + char.ToUpper(fieldName[0]) + fieldName.Substring(1));
            if (prop != null)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning($"  ⚠ Could not find field '{fieldName}' on {target.GetType().Name}");
            }
        }
    }
}
