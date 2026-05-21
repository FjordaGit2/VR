using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Valve.VR;
using UnityEngine.UI;
using PupilLabs;
using UnityEngine.SceneManagement;

/// <summary>
/// Sc3a street task: lamp cue → brief gap → car on left or right road → ITI.
/// Responses (touchpad + gaze) accepted only during the car window.
/// Session CSV streams match Sc1/Sc2 layout (task-relative time column); ROI tags are Left/Right road.
/// </summary>
public class SC3aStreet : LevelScript
{
    [SerializeField] Transform[] SpawnPoses = null;
    [SerializeField] GameObject[] SpawnPrefabs = null;

    [Space]
    [Header("Lamp cue")]
    [Tooltip("Assign the street lamp GameObject; toggled on during lamp-on phase each trial.")]
    public GameObject lamp;

    [Space]
    [Header("Trial counts")]
    [Min(1)] public int totalTrials = 300;
    [Tooltip("Trials per road (left or right). Each car prefab is counterbalanced within that side (e.g. 150 trials, 4 cars → 38/38/37/37 per side).")]
    [Min(1)] public int trialsPerRoadSide = 150;

    [Space]
    [Header("Timing (ms)")]
    [Tooltip("Delay after Start before the first lamp (ms).")]
    [Min(0)] public int preTaskDelayMs = 1000;
    [Min(1)] public int lampOnDurationMs = 1000;
    [Min(0)] public int lampOffGapMs = 200;
    [Min(1)] public int carShowDurationMs = 1000;
    [Min(0)] public int interTrialIntervalMs = 1000;
    [Min(0)] public int postBlockDelayBeforeNextSceneMs = 2000;

    [Space]
    [Header("Car movement")]
    [Tooltip("Forward speed while the car is visible (world units per second).")]
    [Min(0f)] public float carSpeed = 50f;

    [Space]
    [Header("Sequence")]
    [Tooltip("-1 = random seed each run; otherwise fixed for reproducibility.")]
    public int sequenceRandomSeed = -1;
    [Min(1)] public int maxSequenceShuffleAttempts = 5000;
    [Tooltip("Avoid the same road side on consecutive trials when possible.")]
    public bool avoidConsecutiveSameRoadSide = true;
    [Tooltip("When shuffling car order within each road side, avoid the same prefab on consecutive trials in that side's list when possible.")]
    public bool avoidConsecutiveSameCarWithinRoadSide = true;

    [Space]
    [Header("VR Touchpad")]
    public GameObject Pointer;
    public SteamVR_Input_Sources handType;
    public SteamVR_Action_Vector2 touchPadAction = SteamVR_Input.GetAction<SteamVR_Action_Vector2>("TouchpadLeftRight");
    public SteamVR_Action_Boolean touchPadClick = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("TouchpadClick");

    [Space]
    [Header("Eye Tracker")]
    public RecordingController recorder;
    public Text statusText;
    public Camera camera;
    public GazeVisualizer gazeVisualizer;
    public GazeController gazeController;
    public Transform gazeOriginCamera;
    [Tooltip("Optional. World reference for head distance/angle columns (e.g. road midpoint).")]
    [SerializeField] Transform roadHeadMetricsAnchor;

    [Space]
    [Header("Session logging (streams under Behavioural; time column is task-relative)")]
    public float period = 0.1f;
    [Range(0f, 1f)] public float gazeConfidenceThreshold = 0.6f;
    [Min(0.5f)] public float csvFlushIntervalSeconds = 2f;
    [Range(0f, 1f)] public float triggerPressThreshold = 0.5f;

    const int StateRoadTarget = 1;
    const int StateNotRoadTarget = 0;
    const int StateInvalid = -1;

    static SteamVR_Action_Pose VrPose => SteamVR_Actions.default_Pose;

    List<int> _trialRoadSides;
    List<int> _trialCarIndices;
    int _loggedSequenceSeed;

    int _trialIndex;
    int _currentRoadSide;
    int _currentCarIndex;
    float _carOnsetUnityTime;
    bool _responseWindowActive;
    bool _trialResponded;
    bool? _trialPressedLeft;
    float _trialRtMs;
    string _trialLookedGaze;

    StreamWriter _timeseriesWriter;
    StreamWriter _eventsWriter;
    StreamWriter _headWriter;
    StreamWriter _controllerWriter;
    float _sessionLogStartUnityTime;
    float _nextSessionLogTime;
    float _lastCsvFlushTime;
    bool _csvSessionLogging;
    bool _hasPreviousObservedState;
    int _previousObservedState;
    int _lookedRoadTargetCount;
    int _lookedElseCount;
    bool _headMotionPrimed;
    Vector3 _headPrevWorldPos;
    Quaternion _headPrevWorldRot;
    Vector3 _headLastVelWorld;
    float _headLastLinSpeed;
    float _headLastAngSpeed;
    int _headKinTickCount;
    string _trialCsvPath;
    bool _trialCsvHeaderWritten;
    string _gazeSessionId;
    string _gazeCsvParticipantGroup;
    string _gazeCsvParticipantId;

    GazeData _lastGaze;

    void Awake()
    {
        if (Pointer != null)
            Pointer.SetActive(true);
        if (lamp != null)
            lamp.SetActive(false);
        if (camera != null)
            camera.clearFlags = CameraClearFlags.Skybox;

        if (recorder != null)
        {
            recorder.customPath = LevelScript.GetBehaviouralPath(LevelScript.DataFolderSc3aStreet);
            if (recorder.requestCtrl != null)
                _ = recorder.requestCtrl.IsConnected;
        }

        ValidateAndBuildSequences();
    }

    void OnEnable()
    {
        if (gazeController != null)
            gazeController.OnReceive3dGaze += OnGaze;
    }

    void OnDisable()
    {
        if (gazeController != null)
            gazeController.OnReceive3dGaze -= OnGaze;
    }

    void OnGaze(GazeData data)
    {
        _lastGaze = data;
    }

    void OnDestroy()
    {
        _csvSessionLogging = false;
        CloseSessionCsvWriters();
        if (recorder != null)
            recorder.StopRecording();
    }

    void Update()
    {
        if (btnIsClicked && !isStarted)
        {
            StartTask();
            if (recorder != null)
                recorder.StartRecording();
            if (Pointer != null)
                Pointer.SetActive(false);
        }

        if (_csvSessionLogging && gazeOriginCamera != null && Time.timeScale > 0f)
        {
            if (Time.time >= _nextSessionLogTime)
            {
                _nextSessionLogTime += period;
                float sinceStart = Time.time - _sessionLogStartUnityTime;
                GazeData gd = _lastGaze;
                bool havePupilTs = gd != null;
                double pupilTs = havePupilTs ? gd.PupilTimestamp : 0;
                ProcessGazeTick(sinceStart, havePupilTs, pupilTs);
                ProcessHeadMotionRow(sinceStart, havePupilTs, pupilTs);
                ProcessControllerHandRow(sinceStart, havePupilTs, pupilTs, SteamVR_Input_Sources.RightHand, "right");
                MaybePeriodicFlushCsv();
            }
        }

        if (!_responseWindowActive || touchPadAction == null || touchPadClick == null)
            return;

        float windowSec = carShowDurationMs * 0.001f;
        float now = Time.time;
        if (now - _carOnsetUnityTime >= windowSec)
            return;

        if (_trialResponded)
            return;

        Vector2 touchpadValue = touchPadAction.GetAxis(handType);
        bool touchpadClicked = touchPadClick.GetStateDown(handType);
        if (!touchpadClicked)
            return;

        if (touchpadValue.x < 0)
            RegisterTouchpadResponse(true);
        else if (touchpadValue.x > 0)
            RegisterTouchpadResponse(false);
    }

    void RegisterTouchpadResponse(bool pressedLeft)
    {
        _trialResponded = true;
        _trialPressedLeft = pressedLeft;
        float windowMs = carShowDurationMs;
        _trialRtMs = Mathf.Clamp((Time.time - _carOnsetUnityTime) * 1000f, 0f, windowMs);
        _trialLookedGaze = SampleGazeRoadLabel();
    }

    new public void StartTask()
    {
        base.StartTask();
        StartCoroutine(ClearData("sc3a_data"));

        _lookedRoadTargetCount = 0;
        _lookedElseCount = 0;
        _hasPreviousObservedState = false;
        _previousObservedState = StateInvalid;
        _headMotionPrimed = false;
        _headKinTickCount = 0;
        _headLastVelWorld = Vector3.zero;
        _headLastLinSpeed = 0f;
        _headLastAngSpeed = 0f;
        _trialIndex = 0;

        _sessionLogStartUnityTime = Time.time;
        _nextSessionLogTime = Time.time;
        _lastCsvFlushTime = Time.time;

        OpenSessionCsvWriters();
        if (gazeOriginCamera != null)
        {
            string dir = recorder != null
                ? recorder.customPath
                : LevelScript.GetBehaviouralPath(LevelScript.DataFolderSc3aStreet);
            WriteSceneReferenceJsonFiles(dir);
        }

        _csvSessionLogging = true;
        StartCoroutine(RunTaskCoroutine());
    }

    void ValidateAndBuildSequences()
    {
        int prefabCount = SpawnPrefabs != null ? SpawnPrefabs.Length : 0;
        int poseCount = SpawnPoses != null ? SpawnPoses.Length : 0;

        if (prefabCount < 1 || poseCount < 2)
            Debug.LogError("SC3aStreet: assign SpawnPrefabs (cars) and SpawnPoses (left/right).");

        int expectedTotalFromSides = trialsPerRoadSide * 2;
        if (expectedTotalFromSides != totalTrials)
        {
            Debug.LogWarning(
                $"SC3aStreet: totalTrials ({totalTrials}) != trialsPerRoadSide×2 ({expectedTotalFromSides}). Adjust Inspector counts.");
        }

        int seed = sequenceRandomSeed >= 0
            ? sequenceRandomSeed
            : UnityEngine.Random.Range(int.MinValue / 4, int.MaxValue / 4);
        _loggedSequenceSeed = seed;
        var rng = new System.Random(seed);

        _trialRoadSides = BuildBalancedMultiset(0, trialsPerRoadSide, 1, trialsPerRoadSide);
        Shuffle(_trialRoadSides, rng);
        if (avoidConsecutiveSameRoadSide)
            TryRemoveConsecutiveDuplicates(_trialRoadSides, rng);

        _trialCarIndices = BuildTrialCarIndicesMergedWithRoadSides(prefabCount, rng);
    }

    /// <summary>
    /// For each road side, build an evenly split multiset of car prefab indices, shuffle it,
    /// then assign cars trial-by-trial according to the shuffled road-side sequence.
    /// </summary>
    List<int> BuildTrialCarIndicesMergedWithRoadSides(int prefabCount, System.Random rng)
    {
        var merged = new List<int>(_trialRoadSides.Count);
        if (prefabCount < 1 || _trialRoadSides == null)
            return merged;

        var leftCars = BuildBalancedCarMultiset(prefabCount, trialsPerRoadSide);
        var rightCars = BuildBalancedCarMultiset(prefabCount, trialsPerRoadSide);
        Shuffle(leftCars, rng);
        Shuffle(rightCars, rng);
        if (avoidConsecutiveSameCarWithinRoadSide)
        {
            TryRemoveConsecutiveDuplicates(leftCars, rng);
            TryRemoveConsecutiveDuplicates(rightCars, rng);
        }

        int leftUsed = 0;
        int rightUsed = 0;
        for (int t = 0; t < _trialRoadSides.Count; t++)
        {
            if (_trialRoadSides[t] == 0)
            {
                if (leftUsed >= leftCars.Count)
                {
                    Debug.LogError("SC3aStreet: ran out of left-road car assignments.");
                    break;
                }

                merged.Add(leftCars[leftUsed++]);
            }
            else
            {
                if (rightUsed >= rightCars.Count)
                {
                    Debug.LogError("SC3aStreet: ran out of right-road car assignments.");
                    break;
                }

                merged.Add(rightCars[rightUsed++]);
            }
        }

        while (merged.Count < totalTrials)
            merged.Add(rng.Next(prefabCount));
        while (merged.Count > totalTrials)
            merged.RemoveAt(merged.Count - 1);

        return merged;
    }

    /// <summary>
    /// Even split of <paramref name="prefabCount"/> car indices across <paramref name="trialCount"/> trials
    /// (e.g. 150 trials, 4 cars → 38, 38, 37, 37).
    /// </summary>
    static List<int> BuildBalancedCarMultiset(int prefabCount, int trialCount)
    {
        var list = new List<int>(trialCount);
        if (prefabCount < 1 || trialCount < 1)
            return list;

        int baseCount = trialCount / prefabCount;
        int remainder = trialCount % prefabCount;
        for (int c = 0; c < prefabCount; c++)
        {
            int n = baseCount + (c < remainder ? 1 : 0);
            for (int i = 0; i < n; i++)
                list.Add(c);
        }

        return list;
    }

    static List<int> BuildBalancedMultiset(int a, int countA, int b, int countB)
    {
        var list = new List<int>(countA + countB);
        for (int i = 0; i < countA; i++)
            list.Add(a);
        for (int i = 0; i < countB; i++)
            list.Add(b);
        return list;
    }

    static void Shuffle(IList<int> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    static bool HasConsecutiveDuplicates(IList<int> list)
    {
        for (int i = 1; i < list.Count; i++)
        {
            if (list[i] == list[i - 1])
                return true;
        }

        return false;
    }

    static void TryRemoveConsecutiveDuplicates(IList<int> list, System.Random rng)
    {
        for (int attempt = 0; attempt < 5000 && HasConsecutiveDuplicates(list); attempt++)
            Shuffle(list, rng);
    }

    IEnumerator RunTaskCoroutine()
    {
        if (preTaskDelayMs > 0)
            yield return WaitMs(preTaskDelayMs);

        if (_trialRoadSides == null || _trialCarIndices == null
            || _trialRoadSides.Count < totalTrials || _trialCarIndices.Count < totalTrials)
        {
            Debug.LogError("SC3aStreet: invalid trial sequences; aborting.");
            _csvSessionLogging = false;
            CloseSessionCsvWriters();
            yield break;
        }

        for (_trialIndex = 0; _trialIndex < totalTrials; _trialIndex++)
        {
            _currentRoadSide = _trialRoadSides[_trialIndex];
            _currentCarIndex = _trialCarIndices[_trialIndex];

            SetLampActive(true);
            yield return WaitMs(lampOnDurationMs);
            SetLampActive(false);
            yield return WaitMs(lampOffGapMs);

            _responseWindowActive = true;
            _trialResponded = false;
            _trialPressedLeft = null;
            _trialRtMs = 0f;
            _trialLookedGaze = "";

            _carOnsetUnityTime = Time.time;
            double unityOnset = _carOnsetUnityTime;
            double pupilOnset = double.NaN;
            if (_lastGaze != null)
                pupilOnset = _lastGaze.PupilTimestamp;

            SpawnCar(_currentRoadSide, _currentCarIndex);

            yield return WaitMs(carShowDurationMs);

            _responseWindowActive = false;

            string carShown = _currentRoadSide == 0 ? "Left" : "Right";
            string arrowPressed = _trialResponded
                ? (_trialPressedLeft == true ? "Left" : "Right")
                : "";
            string looked = string.IsNullOrEmpty(_trialLookedGaze)
                ? SampleGazeRoadLabel()
                : _trialLookedGaze;
            string lookedAccuracy = ComputeLookedAccuracy(_currentRoadSide, looked);
            bool touchpadCorrect = _trialResponded
                && IsSameSideTouchpadResponse(_currentRoadSide, _trialPressedLeft == true);
            bool gazeCorrect = lookedAccuracy == "Correct";
            string accuracy;
            if (!_trialResponded)
                accuracy = "NoResponse";
            else if (touchpadCorrect && gazeCorrect)
                accuracy = "Correct";
            else
                accuracy = "Wrong";
            string rtCell = _trialResponded
                ? _trialRtMs.ToString("0.###", CultureInfo.InvariantCulture)
                : "NaN";

            AppendTrialRow(
                _trialIndex + 1,
                unityOnset,
                pupilOnset,
                carShown,
                _currentCarIndex,
                arrowPressed,
                accuracy,
                rtCell,
                looked,
                lookedAccuracy);

            yield return WaitMs(interTrialIntervalMs);
        }

        _csvSessionLogging = false;
        CloseSessionCsvWriters();
        WriteSc3aSummaryCsv();

        if (recorder != null)
            recorder.StopRecording();
        StartCoroutine(SetLevel(SceneType.Sc3aQuestionnaire));
        if (postBlockDelayBeforeNextSceneMs > 0)
            yield return WaitMs(postBlockDelayBeforeNextSceneMs);
        NextScene();
    }

    void SpawnCar(int roadSideIndex, int carPrefabIndex)
    {
        if (SpawnPrefabs == null || SpawnPoses == null)
            return;
        if (carPrefabIndex < 0 || carPrefabIndex >= SpawnPrefabs.Length)
            carPrefabIndex = 0;
        if (roadSideIndex < 0 || roadSideIndex >= SpawnPoses.Length)
            roadSideIndex = 0;

        float showSec = carShowDurationMs * 0.001f;
        Instantiate(SpawnPrefabs[carPrefabIndex], SpawnPoses[roadSideIndex])
            .AddComponent<AutoCar>()
            .Set(showSec, carSpeed);
    }

    void SetLampActive(bool on)
    {
        if (lamp != null)
            lamp.SetActive(on);
    }

    static IEnumerator WaitMs(int ms)
    {
        if (ms <= 0)
            yield break;
        yield return new WaitForSeconds(ms * 0.001f);
    }

    string SampleGazeRoadLabel()
    {
        if (gazeOriginCamera == null || _lastGaze == null)
            return "";

        if (!TryBuildGazeRay(_lastGaze, gazeOriginCamera, out Vector3 origin, out Vector3 direction)
            || direction.sqrMagnitude < 1e-8f)
            return "";

        if (Physics.SphereCast(origin, 0.05f, direction, out RaycastHit hit, Mathf.Infinity))
        {
            if (hit.collider.CompareTag("Left"))
                return "Left";
            if (hit.collider.CompareTag("Right"))
                return "Right";
            return "Else";
        }

        return "";
    }

    /// <summary>Sc3a: correct touchpad is same side as car (left car → left press).</summary>
    static bool IsSameSideTouchpadResponse(int carRoadSide, bool pressedLeft)
    {
        return (carRoadSide == 0) == pressedLeft;
    }

    /// <summary>Sc3a: gaze on same road collider (Left/Right tag) as car side.</summary>
    static string ComputeLookedAccuracy(int carRoadSide, string lookedLabel)
    {
        if (string.IsNullOrEmpty(lookedLabel))
            return "NoGaze";
        if (lookedLabel == "Else")
            return "Else";
        bool lookedLeft = lookedLabel == "Left";
        bool sameSide = (carRoadSide == 0) == lookedLeft;
        return sameSide ? "Correct" : "Wrong";
    }

    void AppendTrialRow(
        int trialIndexOneBased,
        double unityCarOnset,
        double pupilCarOnset,
        string carShown,
        int carPrefabIndex,
        string arrowPressed,
        string accuracy,
        string reactionTimeCell,
        string looked,
        string lookedAccuracy)
    {
        string dir = recorder != null
            ? recorder.customPath
            : LevelScript.GetBehaviouralPath(LevelScript.DataFolderSc3aStreet);
        Directory.CreateDirectory(dir);
        _trialCsvPath = Path.Combine(dir, "task_trials.csv");

        if (!_trialCsvHeaderWritten)
        {
            if (!File.Exists(_trialCsvPath))
            {
                string header =
                    "sequence_seed," +
                    "trial_index," +
                    "unity_time_s_car_onset," +
                    "pupil_timestamp_s_at_car_onset," +
                    "car_shown," +
                    "car_prefab_index," +
                    "arrow_pressed," +
                    "accuracy," +
                    "reaction_time_ms," +
                    "looked," +
                    "looked_accuracy," +
                    "created_at\n";
                File.WriteAllText(_trialCsvPath, header, new UTF8Encoding(false));
            }

            _trialCsvHeaderWritten = true;
        }

        string pupilCell = double.IsNaN(pupilCarOnset)
            ? "NaN"
            : pupilCarOnset.ToString(CultureInfo.InvariantCulture);

        string row =
            _loggedSequenceSeed.ToString(CultureInfo.InvariantCulture) + "," +
            trialIndexOneBased.ToString(CultureInfo.InvariantCulture) + "," +
            unityCarOnset.ToString(CultureInfo.InvariantCulture) + "," +
            pupilCell + "," +
            CsvEscape(carShown) + "," +
            carPrefabIndex.ToString(CultureInfo.InvariantCulture) + "," +
            CsvEscape(arrowPressed) + "," +
            CsvEscape(accuracy) + "," +
            reactionTimeCell + "," +
            CsvEscape(looked) + "," +
            CsvEscape(lookedAccuracy) + "," +
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\n";

        File.AppendAllText(_trialCsvPath, row, new UTF8Encoding(false));
    }

    void WriteSc3aSummaryCsv()
    {
        try
        {
            string dir = recorder != null
                ? recorder.customPath
                : LevelScript.GetBehaviouralPath(LevelScript.DataFolderSc3aStreet);
            if (string.IsNullOrWhiteSpace(dir))
                return;
            Directory.CreateDirectory(dir);
            float roadDwell = _lookedRoadTargetCount * period;
            float elseDwell = _lookedElseCount * period;
            string path = Path.Combine(dir, "sc3a_summary.csv");
            string summary =
                "looked_road_target_count,looked_road_target_time_s,looked_else_count,looked_else_time_s,created_at\n" +
                $"{_lookedRoadTargetCount},{roadDwell.ToString("0.0", CultureInfo.InvariantCulture)}," +
                $"{_lookedElseCount},{elseDwell.ToString("0.0", CultureInfo.InvariantCulture)}," +
                $"{DateTime.Now:O}\n";
            File.WriteAllText(path, summary, new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SC3aStreet: failed to save sc3a_summary.csv ({e.Message})");
        }
    }

    void ProcessGazeTick(float sinceStart, bool havePupilTs, double pupilTs)
    {
        float conf = 0f;
        int valid;
        int state;
        string hitName = "";
        string hitTag = "";
        string invalidReason = "";
        Vector3 worldGazeUnit = default;
        bool haveWorldGazeDir = false;

        GazeData gd = _lastGaze;
        bool gazePacket = gd != null;
        if (gazePacket)
            conf = gd.Confidence;

        bool confOk = gazePacket
            && (gazeConfidenceThreshold <= 0f || gd.Confidence >= gazeConfidenceThreshold);

        if (!gazePacket)
        {
            valid = 0;
            state = StateInvalid;
            invalidReason = "no_gaze";
        }
        else if (!confOk)
        {
            valid = 0;
            state = StateInvalid;
            invalidReason = "low_confidence";
        }
        else if (!TryBuildGazeRay(gd, gazeOriginCamera, out Vector3 origin, out Vector3 direction)
                 || direction.sqrMagnitude < 1e-8f)
        {
            valid = 0;
            state = StateInvalid;
            invalidReason = "ray_fail";
        }
        else
        {
            worldGazeUnit = direction.normalized;
            haveWorldGazeDir = true;
            if (Physics.SphereCast(origin, 0.05f, direction, out RaycastHit hit, Mathf.Infinity))
            {
                valid = 1;
                var col = hit.collider;
                if (col != null)
                {
                    hitName = col.gameObject.name;
                    hitTag = col.tag;
                    state = IsRoadTargetTag(col.tag) ? StateRoadTarget : StateNotRoadTarget;
                }
                else
                {
                    state = StateNotRoadTarget;
                }
            }
            else
            {
                valid = 1;
                state = StateNotRoadTarget;
                hitName = "no_hit";
            }
        }

        string glx = "", gly = "", glz = "", gyaw = "", gpit = "", gu = "", gv = "";
        if (haveWorldGazeDir && gazeOriginCamera != null)
            VrGazeEquirectMetrics.TryFormatCsvFields(gazeOriginCamera, worldGazeUnit, out glx, out gly, out glz, out gyaw, out gpit, out gu, out gv);

        string gwx = "", gwy = "", gwz = "";
        if (haveWorldGazeDir)
        {
            gwx = F(worldGazeUnit.x);
            gwy = F(worldGazeUnit.y);
            gwz = F(worldGazeUnit.z);
        }

        WriteTimeseriesRow(sinceStart, gazePacket, havePupilTs, pupilTs, conf, valid, state, hitName, hitTag, invalidReason, glx, gly, glz, gyaw, gpit, gu, gv, gwx, gwy, gwz);

        if (!_hasPreviousObservedState)
        {
            _hasPreviousObservedState = true;
            _previousObservedState = state;
        }
        else if (state != _previousObservedState)
        {
            string ev = EventForRoadTransition(_previousObservedState, state);
            if (ev != null)
                WriteEventRow(sinceStart, havePupilTs, pupilTs, conf, valid, hitName, hitTag, invalidReason, ev, glx, gly, glz, gyaw, gpit, gu, gv, gwx, gwy, gwz);
            _previousObservedState = state;
        }

        if (valid == 1)
        {
            if (state == StateRoadTarget)
                _lookedRoadTargetCount++;
            else
                _lookedElseCount++;
        }
    }

    static bool IsRoadTargetTag(string tag)
    {
        return tag == "Left" || tag == "Right";
    }

    void ProcessHeadMotionRow(float sinceStart, bool havePupilTs, double pupilTs)
    {
        if (_headWriter == null || gazeOriginCamera == null)
            return;

        Transform cam = gazeOriginCamera;
        Vector3 p = cam.position;
        Quaternion r = cam.rotation;
        Vector3 euler = r.eulerAngles;
        Vector3 fwd = cam.forward;
        Vector3 up = cam.up;

        Vector3 velWorld = _headMotionPrimed ? (p - _headPrevWorldPos) / period : Vector3.zero;
        float linearSpeed = velWorld.magnitude;
        float angDeg = _headMotionPrimed ? Quaternion.Angle(_headPrevWorldRot, r) : 0f;
        float angularSpeed = _headMotionPrimed ? Mathf.Deg2Rad * angDeg / period : 0f;

        float accelLinVecMag = 0f;
        float accelLinear = 0f;
        float accelAngular = 0f;
        if (_headMotionPrimed && _headKinTickCount >= 2)
        {
            accelLinVecMag = (velWorld - _headLastVelWorld).magnitude / period;
            accelLinear = (linearSpeed - _headLastLinSpeed) / period;
            accelAngular = (angularSpeed - _headLastAngSpeed) / period;
        }

        string distCol = "";
        string angCol = "";
        if (TryGetRoadWorldPoint(out Vector3 roadPos))
        {
            distCol = F(Vector3.Distance(p, roadPos));
            Vector3 toRoad = roadPos - p;
            if (toRoad.sqrMagnitude > 1e-10f)
                angCol = F(Vector3.Angle(cam.forward, toRoad.normalized));
        }

        string pCol = havePupilTs ? F(pupilTs) : "";
        _headWriter.WriteLine(string.Join(",",
            F(sinceStart),
            pCol,
            F(Time.time),
            F(p.x), F(p.y), F(p.z),
            F(euler.x), F(euler.y), F(euler.z),
            F(fwd.x), F(fwd.y), F(fwd.z),
            F(up.x), F(up.y), F(up.z),
            F(velWorld.x), F(velWorld.y), F(velWorld.z),
            F(linearSpeed),
            F(accelLinVecMag),
            F(accelLinear),
            F(accelAngular),
            F(angularSpeed),
            distCol,
            angCol));

        if (_headMotionPrimed)
        {
            _headLastVelWorld = velWorld;
            _headLastLinSpeed = linearSpeed;
            _headLastAngSpeed = angularSpeed;
        }

        _headPrevWorldPos = p;
        _headPrevWorldRot = r;
        _headMotionPrimed = true;
        _headKinTickCount++;
    }

    void ProcessControllerHandRow(float sinceStart, bool havePupilTs, double pupilTs, SteamVR_Input_Sources hand, string handLabel)
    {
        if (_controllerWriter == null)
            return;

        Vector3 p = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        if (SteamVR.initializedState == SteamVR.InitializedStates.InitializeSuccess && VrPose != null
            && VrPose.GetPoseIsValid(hand))
        {
            Vector3 lp = VrPose.GetLocalPosition(hand);
            Quaternion lr = VrPose.GetLocalRotation(hand);
            Transform rig = GetTrackingRigOrigin();
            if (rig != null)
            {
                p = rig.TransformPoint(lp);
                rot = rig.rotation * lr;
            }
            else
            {
                p = lp;
                rot = lr;
            }
        }

        Vector3 euler = rot.eulerAngles;
        float vLin = 0f;
        float vAng = 0f;
        if (SteamVR.initializedState == SteamVR.InitializedStates.InitializeSuccess && VrPose != null
            && VrPose.GetPoseIsValid(hand))
        {
            Transform rig = GetTrackingRigOrigin();
            Vector3 lv = VrPose.GetVelocity(hand);
            Vector3 av = VrPose.GetAngularVelocity(hand);
            if (rig != null)
            {
                lv = rig.TransformDirection(lv);
                av = rig.TransformDirection(av);
            }

            vLin = lv.magnitude;
            vAng = av.magnitude;
        }

        int triggerPressed = 0;
        int gripPressed = 0;
        if (SteamVR.initializedState == SteamVR.InitializedStates.InitializeSuccess)
        {
            var squeeze = SteamVR_Actions.default_Squeeze;
            if (squeeze != null && squeeze.activeBinding)
                triggerPressed = squeeze.GetAxis(hand) >= triggerPressThreshold ? 1 : 0;
            var grip = SteamVR_Actions.default_GrabGrip;
            if (grip != null && grip.activeBinding)
                gripPressed = grip.GetState(hand) ? 1 : 0;
        }

        string buttonEvents = BuildControllerButtonEvents(hand);
        string pCol = havePupilTs ? F(pupilTs) : "";
        _controllerWriter.WriteLine(string.Join(",",
            F(sinceStart),
            pCol,
            F(Time.time),
            CsvEscape(handLabel),
            F(p.x), F(p.y), F(p.z),
            F(euler.x), F(euler.y), F(euler.z),
            F(vLin),
            F(vAng),
            triggerPressed.ToString(CultureInfo.InvariantCulture),
            gripPressed.ToString(CultureInfo.InvariantCulture),
            CsvEscape(buttonEvents)));
    }

    void OpenSessionCsvWriters()
    {
        CloseSessionCsvWriters();
        string dir = recorder != null
            ? recorder.customPath
            : LevelScript.GetBehaviouralPath(LevelScript.DataFolderSc3aStreet);
        Directory.CreateDirectory(dir);

        _gazeSessionId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
            + "_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture).Substring(0, 8);
        _gazeCsvParticipantGroup = CsvEscape(UserGroup ?? "");
        _gazeCsvParticipantId = CsvEscape(UserName ?? "");

        _timeseriesWriter = new StreamWriter(Path.Combine(dir, "gaze_timeseries.csv"), false, new UTF8Encoding(false)) { AutoFlush = false };
        _eventsWriter = new StreamWriter(Path.Combine(dir, "gaze_events.csv"), false, new UTF8Encoding(false)) { AutoFlush = false };
        _headWriter = new StreamWriter(Path.Combine(dir, "head_timeseries.csv"), false, new UTF8Encoding(false)) { AutoFlush = false };
        _controllerWriter = new StreamWriter(Path.Combine(dir, "controller_timeseries.csv"), false, new UTF8Encoding(false)) { AutoFlush = false };

        _timeseriesWriter.WriteLine("time_since_task_start_s,pupil_timestamp_s,unity_time_s,state,confidence,valid,hit_name,hit_tag,invalid_reason,gaze_hmd_local_x,gaze_hmd_local_y,gaze_hmd_local_z,yaw_deg,pitch_deg,equirect_u,equirect_v,gaze_world_x,gaze_world_y,gaze_world_z,participant_group,participant_id,session_id");
        _eventsWriter.WriteLine("time_since_task_start_s,pupil_timestamp_s,unity_time_s,event,confidence,valid,hit_name,hit_tag,invalid_reason,gaze_hmd_local_x,gaze_hmd_local_y,gaze_hmd_local_z,yaw_deg,pitch_deg,equirect_u,equirect_v,gaze_world_x,gaze_world_y,gaze_world_z,participant_group,participant_id,session_id");
        _headWriter.WriteLine("time_since_task_start_s,pupil_timestamp_s,unity_time_s,position_x,position_y,position_z,rotation_x,rotation_y,rotation_z,forward_x,forward_y,forward_z,up_x,up_y,up_z,vel_x,vel_y,vel_z,linear_speed,accel_lin_vec_mag,accel_linear,accel_angular,angular_speed,head_distance_to_road,head_angle_to_road");
        _controllerWriter.WriteLine("time_since_task_start_s,pupil_timestamp_s,unity_time_s,hand,position_x,position_y,position_z,rotation_x,rotation_y,rotation_z,velocity_linear,velocity_angular,trigger_pressed,grip_pressed,button_events");

        _lastCsvFlushTime = Time.time;
        _timeseriesWriter.Flush();
        _eventsWriter.Flush();
        _headWriter.Flush();
        _controllerWriter.Flush();
    }

    void CloseSessionCsvWriters()
    {
        if (_timeseriesWriter != null)
        {
            _timeseriesWriter.Flush();
            _timeseriesWriter.Dispose();
            _timeseriesWriter = null;
        }

        if (_eventsWriter != null)
        {
            _eventsWriter.Flush();
            _eventsWriter.Dispose();
            _eventsWriter = null;
        }

        if (_headWriter != null)
        {
            _headWriter.Flush();
            _headWriter.Dispose();
            _headWriter = null;
        }

        if (_controllerWriter != null)
        {
            _controllerWriter.Flush();
            _controllerWriter.Dispose();
            _controllerWriter = null;
        }
    }

    void MaybePeriodicFlushCsv()
    {
        if (_timeseriesWriter == null && _eventsWriter == null && _headWriter == null && _controllerWriter == null)
            return;
        if (Time.time - _lastCsvFlushTime < csvFlushIntervalSeconds)
            return;
        _lastCsvFlushTime = Time.time;
        _timeseriesWriter?.Flush();
        _eventsWriter?.Flush();
        _headWriter?.Flush();
        _controllerWriter?.Flush();
    }

    void WriteTimeseriesRow(float sinceStart, bool gazePacket, bool havePupilTs, double pupilTs, float conf, int valid, int state, string hitName, string hitTag, string invalidReason, string glx, string gly, string glz, string gyaw, string gpit, string gu, string gv, string gwx, string gwy, string gwz)
    {
        if (_timeseriesWriter == null)
            return;
        string pCol = havePupilTs ? F(pupilTs) : "";
        string cCol = gazePacket ? F(conf) : "";
        _timeseriesWriter.WriteLine(string.Join(",",
            F(sinceStart),
            pCol,
            F(Time.time),
            state.ToString(CultureInfo.InvariantCulture),
            cCol,
            valid.ToString(CultureInfo.InvariantCulture),
            CsvEscape(hitName),
            CsvEscape(hitTag),
            CsvEscape(invalidReason),
            glx, gly, glz, gyaw, gpit, gu, gv,
            gwx, gwy, gwz,
            _gazeCsvParticipantGroup,
            _gazeCsvParticipantId,
            _gazeSessionId));
    }

    void WriteEventRow(float sinceStart, bool havePupilTs, double pupilTs, float conf, int valid, string hitName, string hitTag, string invalidReason, string ev, string glx, string gly, string glz, string gyaw, string gpit, string gu, string gv, string gwx, string gwy, string gwz)
    {
        if (_eventsWriter == null)
            return;
        string pCol = havePupilTs ? F(pupilTs) : "";
        _eventsWriter.WriteLine(string.Join(",",
            F(sinceStart),
            pCol,
            F(Time.time),
            CsvEscape(ev),
            F(conf),
            valid.ToString(CultureInfo.InvariantCulture),
            CsvEscape(hitName),
            CsvEscape(hitTag),
            CsvEscape(invalidReason),
            glx, gly, glz, gyaw, gpit, gu, gv,
            gwx, gwy, gwz,
            _gazeCsvParticipantGroup,
            _gazeCsvParticipantId,
            _gazeSessionId));
    }

    void WriteSceneReferenceJsonFiles(string sessionDir)
    {
        try
        {
            var scene = SceneManager.GetActiveScene();
            var sb = new StringBuilder(1024);
            sb.Append('{');
            sb.Append("\"schema\":\"sc3a_scene_rois v1\",");
            sb.Append("\"unity_scene_name\":").Append(JsonString(scene.name)).Append(',');
            sb.Append("\"unity_time_at_snapshot\":").Append(F(Time.time)).Append(',');
            sb.Append("\"equirect_heatmap_columns\":").Append(JsonString("gaze_hmd_local_x,y,z; yaw_deg; pitch_deg; equirect_u,v; see VrGazeEquirectMetrics")).Append(',');
            AppendTransformBlock(sb, "road_anchor", GetRoadTransformForSnapshot());
            sb.Append(',');
            AppendTransformBlock(sb, "hmd_origin_at_session_start", gazeOriginCamera);
            sb.Append(',');
            AppendTransformBlock(sb, "tracking_rig_origin", GetTrackingRigOrigin());
            sb.Append('}');
            File.WriteAllText(Path.Combine(sessionDir, "scene_rois.json"), sb.ToString(), new UTF8Encoding(false));

            const string roiRoadTemplate = "{\"roi_name\":\"Road\",\"type\":\"polygon\",\"points\":[],\"note\":\"Left/Right road colliders tagged in scene; fill panorama ROI offline if needed.\"}";
            File.WriteAllText(Path.Combine(sessionDir, "roi_road.json"), roiRoadTemplate, new UTF8Encoding(false));
            LevelScript.WritePanoReferenceJson(sessionDir, scene.name);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SC3aStreet: failed to write scene ROI JSON ({e.Message})");
        }
    }

    Transform GetRoadTransformForSnapshot()
    {
        if (roadHeadMetricsAnchor != null)
            return roadHeadMetricsAnchor;
        var left = GameObject.FindGameObjectWithTag("Left");
        if (left != null)
            return left.transform;
        return SpawnPoses != null && SpawnPoses.Length > 0 ? SpawnPoses[0] : null;
    }

    bool TryGetRoadWorldPoint(out Vector3 roadPos)
    {
        var t = GetRoadTransformForSnapshot();
        if (t != null)
        {
            roadPos = t.position;
            return true;
        }

        roadPos = default;
        return false;
    }

    static string EventForRoadTransition(int prev, int curr)
    {
        if (prev == curr)
            return null;
        bool prevRoad = prev == StateRoadTarget;
        bool currRoad = curr == StateRoadTarget;
        if (currRoad && !prevRoad)
            return "enter_road";
        if (!currRoad && prevRoad)
            return "exit_road";
        return null;
    }

    static bool TryBuildGazeRay(GazeData gazeData, Transform pupilLocalToWorld, out Vector3 origin, out Vector3 direction)
    {
        if (gazeData.MappingContext == GazeData.GazeMappingContext.Binocular
            && gazeData.IsEyeDataAvailable(0) && gazeData.IsEyeDataAvailable(1))
        {
            Vector3 localEyeMid = (gazeData.EyeCenter0 + gazeData.EyeCenter1) * 0.5f;
            origin = pupilLocalToWorld.TransformPoint(localEyeMid);
            direction = pupilLocalToWorld.TransformDirection((gazeData.GazeNormal0 + gazeData.GazeNormal1).normalized);
            return true;
        }

        if (gazeData.IsEyeDataAvailable(0))
        {
            origin = pupilLocalToWorld.TransformPoint(gazeData.EyeCenter0);
            direction = pupilLocalToWorld.TransformDirection(gazeData.GazeNormal0.normalized);
            return true;
        }

        if (gazeData.IsEyeDataAvailable(1))
        {
            origin = pupilLocalToWorld.TransformPoint(gazeData.EyeCenter1);
            direction = pupilLocalToWorld.TransformDirection(gazeData.GazeNormal1.normalized);
            return true;
        }

        origin = pupilLocalToWorld.position;
        direction = pupilLocalToWorld.TransformDirection(gazeData.GazeDirection);
        return true;
    }

    static string BuildControllerButtonEvents(SteamVR_Input_Sources hand)
    {
        if (SteamVR.initializedState != SteamVR.InitializedStates.InitializeSuccess)
            return "";

        var sb = new StringBuilder(64);
        AppendBoolEdges(sb, SteamVR_Actions.default_GrabPinch, hand, "grab_pinch");
        AppendBoolEdges(sb, SteamVR_Actions.default_GrabGrip, hand, "grab_grip");
        AppendBoolEdges(sb, SteamVR_Actions.default_InteractUI, hand, "interact_ui");
        AppendBoolEdges(sb, SteamVR_Actions.default_Teleport, hand, "teleport");
        AppendBoolEdges(sb, SteamVR_Actions.default_TouchpadClick, hand, "touchpad_click");
        AppendBoolEdges(sb, SteamVR_Actions.default_SnapTurnLeft, hand, "snap_turn_left");
        AppendBoolEdges(sb, SteamVR_Actions.default_SnapTurnRight, hand, "snap_turn_right");
        if (sb.Length > 0 && sb[sb.Length - 1] == ';')
            sb.Length -= 1;
        return sb.ToString();
    }

    static void AppendBoolEdges(StringBuilder sb, SteamVR_Action_Boolean action, SteamVR_Input_Sources hand, string id)
    {
        if (action == null || !action.activeBinding)
            return;
        if (action.GetStateDown(hand))
            sb.Append(id).Append("_down;");
        if (action.GetStateUp(hand))
            sb.Append(id).Append("_up;");
    }

    static Transform GetTrackingRigOrigin()
    {
        if (SteamVR.initializedState != SteamVR.InitializedStates.InitializeSuccess)
            return null;
        var top = SteamVR_Render.Top();
        return top != null ? top.origin : null;
    }

    static void AppendTransformBlock(StringBuilder sb, string key, Transform t)
    {
        sb.Append('"').Append(key).Append("\":");
        if (t == null)
        {
            sb.Append("null");
            return;
        }

        Vector3 p = t.position;
        Quaternion q = t.rotation;
        Vector3 ls = t.localScale;
        sb.Append('{');
        sb.Append("\"position\":[").Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z)).Append("],");
        sb.Append("\"rotation\":[").Append(F(q.x)).Append(',').Append(F(q.y)).Append(',').Append(F(q.z)).Append(',').Append(F(q.w)).Append("],");
        sb.Append("\"local_scale\":[").Append(F(ls.x)).Append(',').Append(F(ls.y)).Append(',').Append(F(ls.z)).Append("],");
        sb.Append("\"name\":").Append(JsonString(t.name));
        sb.Append('}');
    }

    static string JsonString(string s)
    {
        if (s == null)
            return "\"\"";
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    static string CsvEscape(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    static string F(double v) => v.ToString("G17", CultureInfo.InvariantCulture);
    static string F(float v) => v.ToString("G9", CultureInfo.InvariantCulture);
}
