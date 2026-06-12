using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Experimenter-only debug overlay on the PC monitor (not rendered inside the VR headset).
/// Participant VR input is never disabled. Mouse clicks use manual hit-testing so scene
/// EventSystems / VRInputModule stay untouched. Keys: N = skip, F = 1→5, B = 5→1.
/// </summary>
public class StudyFlowDebugSkip : MonoBehaviour
{
    static StudyFlowDebugSkip _instance;

    const int UiLayer = 5;

    struct ClickTarget
    {
        public RectTransform Rect;
        public UnityAction Action;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded += OnAnySceneLoaded;
        EnsureInstance();
        OnAnySceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!StudySceneFlow.EnableDebugSkipButton)
            return;

        EnsureInstance();
        if (_instance == null)
            return;

        _instance._advancing = false;
        _instance.ApplyRenderMode();
        _instance.RefreshVisibility();
        _instance.RefreshStatus();
    }

    static void EnsureInstance()
    {
        if (!StudySceneFlow.EnableDebugSkipButton)
            return;

        if (_instance != null)
            return;

        if (SceneManager.GetActiveScene().name == StudySceneFlow.SceneEnd)
            return;

        var go = new GameObject("StudyFlowDebugSkip");
        _instance = go.AddComponent<StudyFlowDebugSkip>();
    }

    Camera _monitorCamera;
    GameObject _canvasRoot;
    Canvas _canvas;
    Text _statusText;
    bool _advancing;
    readonly List<ClickTarget> _clickTargets = new List<ClickTarget>();

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        BuildUi();
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    void BuildUi()
    {
        var camGo = new GameObject("ExperimenterDebugCamera");
        camGo.transform.SetParent(transform, false);
        _monitorCamera = camGo.AddComponent<Camera>();
        _monitorCamera.clearFlags = CameraClearFlags.Depth;
        _monitorCamera.cullingMask = 1 << UiLayer;
        _monitorCamera.orthographic = true;
        _monitorCamera.nearClipPlane = 0.3f;
        _monitorCamera.farClipPlane = 5f;
        _monitorCamera.depth = 100f;
        _monitorCamera.stereoTargetEye = StereoTargetEyeMask.None;
        _monitorCamera.targetDisplay = 0;
        _monitorCamera.useOcclusionCulling = false;

        _canvasRoot = new GameObject("StudyFlowDebugCanvas");
        _canvasRoot.transform.SetParent(transform, false);
        SetLayerRecursive(_canvasRoot, UiLayer);

        _canvas = _canvasRoot.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32767;

        var scaler = _canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var panel = CreatePanel(_canvasRoot.transform, new Vector2(12, -12), new Vector2(0, 1), new Vector2(0, 1), new Vector2(560, 168));

        _statusText = CreateText(panel.transform, "Status", 14, TextAnchor.UpperLeft,
            new Vector2(10, -10), new Vector2(520, 56));

        RegisterButton(CreateButton(panel.transform, "Skip - next scene (N)", new Vector2(10, -72), new Vector2(250, 36)), OnSkipNext);
        RegisterButton(CreateButton(panel.transform, "Start 1 to 5 here (F)", new Vector2(268, -72), new Vector2(130, 36)), ActivateForwardOrder);
        RegisterButton(CreateButton(panel.transform, "Start 5 to 1 here (B)", new Vector2(406, -72), new Vector2(140, 36)), ActivateReverseOrder);

        CreateText(panel.transform, "Hint", 12, TextAnchor.UpperLeft,
            new Vector2(10, -118), new Vector2(540, 40)).text =
            "Experimenter monitor only — not visible in headset";

        RefreshStatus();
        ApplyRenderMode();
        RefreshVisibility();
    }

    /// <summary>
    /// VR session: render to PC monitor only (StereoTargetEye.None — not in headset).
    /// Flatscreen / editor: Screen Space Overlay on the Game view.
    /// </summary>
    void ApplyRenderMode()
    {
        if (_canvas == null)
            return;

        bool vrHeadsetActive = UnityEngine.XR.XRSettings.isDeviceActive;
        if (vrHeadsetActive)
        {
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = _monitorCamera;
            _canvas.planeDistance = 1f;
            if (_monitorCamera != null)
                _monitorCamera.enabled = _canvasRoot != null && _canvasRoot.activeSelf;
        }
        else
        {
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.worldCamera = null;
            if (_monitorCamera != null)
                _monitorCamera.enabled = false;
        }
    }

    void RegisterButton(RectTransform rect, UnityAction action)
    {
        _clickTargets.Add(new ClickTarget { Rect = rect, Action = action });
    }

    void RefreshVisibility()
    {
        if (_canvasRoot == null)
            return;

        bool show = StudySceneFlow.ShouldShowPcDebugOverlay()
            && SceneManager.GetActiveScene().name != StudySceneFlow.SceneEnd;

        _canvasRoot.SetActive(show);
        ApplyRenderMode();
    }

    void RefreshStatus()
    {
        if (_statusText == null)
            return;

        string scene = SceneManager.GetActiveScene().name;
        if (scene == StudySceneFlow.SceneId)
            _statusText.text = "ID — log in to start study flow.\n" + StudySceneFlow.GetDebugStatusLine();
        else
            _statusText.text = StudySceneFlow.GetDebugStatusLine();
    }

    void Update()
    {
        if (!StudySceneFlow.ShouldShowPcDebugOverlay())
            return;

        if (_canvasRoot == null || !_canvasRoot.activeInHierarchy)
            return;

        if (Input.GetKeyDown(KeyCode.N))
            OnSkipNext();
        if (Input.GetKeyDown(KeyCode.F))
            ActivateForwardOrder();
        if (Input.GetKeyDown(KeyCode.B))
            ActivateReverseOrder();

        if (Input.GetMouseButtonDown(0))
            TryHandleMouseClick();
    }

    void TryHandleMouseClick()
    {
        Vector2 mouse = Input.mousePosition;
        Camera cam = _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceCamera
            ? _monitorCamera
            : null;

        for (int i = _clickTargets.Count - 1; i >= 0; i--)
        {
            ClickTarget target = _clickTargets[i];
            if (target.Rect == null)
                continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(target.Rect, mouse, cam))
            {
                target.Action();
                return;
            }
        }
    }

    void ActivateForwardOrder()
    {
        StudySceneFlow.BeginSequenceAtCurrentScene(StudySceneFlow.StudySceneOrder.Forward_1_to_5);
        RefreshStatus();
    }

    void ActivateReverseOrder()
    {
        StudySceneFlow.BeginSequenceAtCurrentScene(StudySceneFlow.StudySceneOrder.Reverse_5_to_1);
        RefreshStatus();
    }

    void OnSkipNext()
    {
        if (_advancing)
            return;

        _advancing = true;
        Debug.Log("StudyFlowDebugSkip: " + StudySceneFlow.GetDebugStatusLine());
        LevelScript.NextScene();
        StartCoroutine(UnlockIfSceneDidNotChange());
    }

    System.Collections.IEnumerator UnlockIfSceneDidNotChange()
    {
        string before = SceneManager.GetActiveScene().name;
        yield return null;
        yield return null;
        if (SceneManager.GetActiveScene().name == before)
            _advancing = false;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        Transform t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursive(t.GetChild(i).gameObject, layer);
    }

    static Font _cachedUiFont;

    static Font UiFont()
    {
        if (_cachedUiFont != null)
            return _cachedUiFont;

        _cachedUiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_cachedUiFont == null)
            _cachedUiFont = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Helvetica" }, 14);
        return _cachedUiFont;
    }

    static GameObject CreatePanel(Transform parent, Vector2 anchoredPos, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        var go = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = anchorMin;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.88f);
        img.raycastTarget = false;
        return go;
    }

    static Text CreateText(Transform parent, string name, int fontSize, TextAnchor anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var text = go.AddComponent<Text>();
        text.font = UiFont();
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static RectTransform CreateButton(Transform parent, string label, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.22f, 0.45f, 0.75f, 1f);
        img.raycastTarget = false;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var text = textGo.AddComponent<Text>();
        text.font = UiFont();
        text.fontSize = 12;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;
        text.raycastTarget = false;

        return rt;
    }
}
