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
/// Sc3b street task: same structure as Sc3a; correct touchpad response is the opposite road side from the car.
/// </summary>
public class SC3bStreet : LevelScript
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
    int _lookedCorrectCount;
    int _lookedWrongCount;
    int _trialGazeSampleCount;
    int _summaryHitsTouchpad;
    int _summaryMissesTouchpad;
    int _summaryFalseAlarmsTouchpad;
    int _summaryCorrectRejectionsTouchpad;
    int _summaryHitsBoth;
    int _summaryMissesBoth;
    int _summaryFalseAlarmsBoth;
    int _summaryCorrectRejectionsBoth;
    bool _headMotionPrimed;
    Vector3 _headPrevWorldPos;
    Quaternion _headPrevWorldRot;
    Vector3 _headLastVelWorld;
    float _headLastLinSpeed;
    float _headLastAngSpeed;
    int _headKinTickCount;
    string _trialCsvPath;
    bool _trialCsvHeaderWritten;
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
            recorder.customPath = LevelScript.GetEyeTrackingPath(LevelScript.DataFolderSc3bStreet);
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
        if (ConsumeStartButtonForTask())
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
                ProcessControllerHandRow(sinceStart, havePupilTs, pupilTs, SteamVR_Input_Sources.RightHand);
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
        if (_csvSessionLogging)
            return;
        base.StartTask();
        StartCoroutine(ClearData("sc3b_data"));

        _lookedRoadTargetCount = 0;
        _lookedElseCount = 0;
        _lookedCorrectCount = 0;
        _lookedWrongCount = 0;
        _trialGazeSampleCount = 0;
        _summaryHitsTouchpad = 0;
        _summaryMissesTouchpad = 0;
        _summaryFalseAlarmsTouchpad = 0;
        _summaryCorrectRejectionsTouchpad = 0;
        _summaryHitsBoth = 0;
        _summaryMissesBoth = 0;
        _summaryFalseAlarmsBoth = 0;
        _summaryCorrectRejectionsBoth = 0;
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
            string dir = LevelScript.GetBehaviouralPath(LevelScript.DataFolderSc3bStreet);
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
            Debug.LogError("SC3bStreet: assign SpawnPrefabs (cars) and SpawnPoses (left/right).");

        int expectedTotalFromSides = trialsPerRoadSide * 2;
        if (expectedTotalFromSides != totalTrials)
        {
            Debug.LogWarning(
                $"SC3bStreet: totalTrials ({totalTrials}) != trialsPerRoadSide×2 ({expectedTotalFromSides}). Adjust Inspector counts.");
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
                    Debug.LogError("SC3bStreet: ran out of left-road car assignments.");
                    break;
                }

                merged.Add(leftCars[leftUsed++]);
            }
            else
            {
                if (rightUsed >= rightCars.Count)
                {
                    Debug.LogError("SC3bStreet: ran out of right-road car assignments.");
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
            Debug.LogError("SC3bStreet: invalid trial sequences; aborting.");
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
            _trialGazeSampleCount = 0;

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
            int lookedAccuracyCode = ComputeLookedAccuracyCode(_currentRoadSide, looked);
            int touchpadAccuracyCode = ComputeTouchpadAccuracyCode(
                _trialResponded, _currentRoadSide, _trialPressedLeft == true);
            int responseFlag = _trialResponded ? 1 : 0;
            int bothAccuracyCode = ComputeBothAccuracyCode(
                responseFlag, touchpadAccuracyCode, lookedAccuracyCode);
            int touchpadOutcomeCode = StudyTaskTrialsLog.ComputeRespondGoOutcomeCode(
                responseFlag, touchpadAccuracyCode);
            int bothOutcomeCode = StudyTaskTrialsLog.ComputeRespondGoOutcomeCode(
                responseFlag, bothAccuracyCode);
            StudyTaskTrialsLog.IncrementOutcomeSummary(
                touchpadOutcomeCode,
                ref _summaryHitsTouchpad,
                ref _summaryMissesTouchpad,
                ref _summaryFalseAlarmsTouchpad,
                ref _summaryCorrectRejectionsTouchpad);
            StudyTaskTrialsLog.IncrementOutcomeSummary(
                bothOutcomeCode,
                ref _summaryHitsBoth,
                ref _summaryMissesBoth,
                ref _summaryFalseAlarmsBoth,
                ref _summaryCorrectRejectionsBoth);
            string rtCell = _trialResponded
                ? _trialRtMs.ToString("0.###", CultureInfo.InvariantCulture)
                : "NaN";
            float sinceTaskStartSec = _carOnsetUnityTime - _sessionLogStartUnityTime;

            CommitTrialGazeToOutcomeBuckets(bothAccuracyCode);

            AppendTrialRow(
                sinceTaskStartSec,
                _trialIndex + 1,
                unityOnset,
                pupilOnset,
                carShown,
                arrowPressed,
                looked,
                lookedAccuracyCode,
                touchpadAccuracyCode,
                bothAccuracyCode,
                responseFlag,
                touchpadOutcomeCode,
                bothOutcomeCode,
                rtCell);

            yield return WaitMs(interTrialIntervalMs);
        }

        _csvSessionLogging = false;
        CloseSessionCsvWriters();
        WriteSc3bSummaryCsv();

        if (recorder != null)
            recorder.StopRecording();
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

    /// <summary>Sc3b: correct touchpad is opposite side from car (left car → right press).</summary>
    static bool IsOppositeTouchpadResponse(int carRoadSide, bool pressedLeft)
    {
        return (carRoadSide == 0) != pressedLeft;
    }

    /// <summary>Sc3b gaze accuracy: 0 wrong, 1 correct, 2 else, 3 no gaze.</summary>
    static int ComputeLookedAccuracyCode(int carRoadSide, string lookedLabel)
    {
        if (string.IsNullOrEmpty(lookedLabel))
            return 3;
        if (lookedLabel == "Else")
            return 2;
        bool lookedLeft = lookedLabel == "Left";
        bool opposite = (carRoadSide == 0) != lookedLeft;
        return opposite ? 1 : 0;
    }

    static int ComputeTouchpadAccuracyCode(bool responded, int carRoadSide, bool pressedLeft)
    {
        if (!responded)
            return 2;
        return IsOppositeTouchpadResponse(carRoadSide, pressedLeft) ? 1 : 0;
    }

    static int ComputeBothAccuracyCode(int responseFlag, int touchpadAccuracyCode, int lookedAccuracyCode)
    {
        if (responseFlag == 0)
            return 2;
        return touchpadAccuracyCode == 1 && lookedAccuracyCode == 1 ? 1 : 0;
    }

    void AppendTrialRow(
        float sinceTaskStartSec,
        int trialIndexOneBased,
        double unityCarOnset,
        double pupilCarOnset,
        string carShown,
        string arrowPressed,
        string looked,
        int lookedAccuracyCode,
        int touchpadAccuracyCode,
        int bothAccuracyCode,
        int responseFlag,
        int touchpadOutcomeCode,
        int bothOutcomeCode,
        string reactionTimeCell)
    {
        string dir = LevelScript.GetBehaviouralPath(LevelScript.DataFolderSc3bStreet);
        Directory.CreateDirectory(dir);
        _trialCsvPath = Path.Combine(dir, "task_trials.csv");

        if (!_trialCsvHeaderWritten)
        {
            if (!File.Exists(_trialCsvPath))
            {
                string header =
                    "time_since_task_start_ms," +
                    "sequence_seed," +
                    "trial_index," +
                    "unity_time_ms_car_onset," +
                    "pupil_timestamp_ms_at_car_onset," +
                    "car_shown," +
                    "arrow_pressed," +
                    "looked," +
                    "looked_accuracy_0_wrong_1_correct_2_else_3_no_gaze," +
                    "touchpad_accuracy_1_correct_0_wrong_2_no_response," +
                    "accuracy_both_1_correct_0_wrong_2_no_response," +
                    "target," +
                    "response," +
                    StudyTaskTrialsLog.OutcomeColumnName + "," +
                    "commission_touchpad_0_no_1_yes," +
                    "omission_touchpad_0_no_1_yes," +
                    StudyTaskTrialsLog.OutcomeBothColumnName + "," +
                    "commission_both_0_no_1_yes," +
                    "omission_both_0_no_1_yes," +
                    "reaction_time_ms," +
                    "created_at\n";
                File.WriteAllText(_trialCsvPath, header, new UTF8Encoding(false));
            }

            _trialCsvHeaderWritten = true;
        }

        string row =
            StudyCsvTime.FormatSecondsAsMs(sinceTaskStartSec) + "," +
            _loggedSequenceSeed.ToString(CultureInfo.InvariantCulture) + "," +
            trialIndexOneBased.ToString(CultureInfo.InvariantCulture) + "," +
            StudyCsvTime.FormatSecondsAsMs(unityCarOnset) + "," +
            StudyCsvTime.FormatOptionalTimestampCellMs(pupilCarOnset) + "," +
            CsvEscape(carShown) + "," +
            CsvEscape(arrowPressed) + "," +
            CsvEscape(looked) + "," +
            lookedAccuracyCode.ToString(CultureInfo.InvariantCulture) + "," +
            touchpadAccuracyCode.ToString(CultureInfo.InvariantCulture) + "," +
            bothAccuracyCode.ToString(CultureInfo.InvariantCulture) + "," +
            StudyTaskTrialsLog.Sc3TargetAlwaysRespond.ToString(CultureInfo.InvariantCulture) + "," +
            responseFlag.ToString(CultureInfo.InvariantCulture) + "," +
            touchpadOutcomeCode.ToString(CultureInfo.InvariantCulture) + "," +
            StudyTaskTrialsLog.CommissionFromOutcome(touchpadOutcomeCode).ToString(CultureInfo.InvariantCulture) + "," +
            StudyTaskTrialsLog.OmissionFromOutcome(touchpadOutcomeCode).ToString(CultureInfo.InvariantCulture) + "," +
            bothOutcomeCode.ToString(CultureInfo.InvariantCulture) + "," +
            StudyTaskTrialsLog.CommissionFromOutcome(bothOutcomeCode).ToString(CultureInfo.InvariantCulture) + "," +
            StudyTaskTrialsLog.OmissionFromOutcome(bothOutcomeCode).ToString(CultureInfo.InvariantCulture) + "," +
            reactionTimeCell + "," +
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\n";

        File.AppendAllText(_trialCsvPath, row, new UTF8Encoding(false));
    }

    void WriteSc3bSummaryCsv()
    {
        try
        {
            string dir = LevelScript.GetBehaviouralPath(LevelScript.DataFolderSc3bStreet);
            if (string.IsNullOrWhiteSpace(dir))
                return;
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "sc3b_summary.csv");
            string summary =
                "looked_road_target_count,looked_road_target_time_ms,looked_else_count,looked_else_time_ms," +
                "looked_correct_count,looked_correct_time_ms,looked_wrong_count,looked_wrong_time_ms," +
                "hits_touchpad,misses_touchpad,false_alarms_touchpad,correct_rejections_touchpad,commission_errors_touchpad,omission_errors_touchpad," +
                "hits_both,misses_both,false_alarms_both,correct_rejections_both,commission_errors_both,omission_errors_both," +
                "created_at\n" +
                $"{_lookedRoadTargetCount},{StudyCsvTime.GazeSampleCountToMs(_lookedRoadTargetCount, period)}," +
                $"{_lookedElseCount},{StudyCsvTime.GazeSampleCountToMs(_lookedElseCount, period)}," +
                $"{_lookedCorrectCount},{StudyCsvTime.GazeSampleCountToMs(_lookedCorrectCount, period)}," +
                $"{_lookedWrongCount},{StudyCsvTime.GazeSampleCountToMs(_lookedWrongCount, period)}," +
                $"{_summaryHitsTouchpad},{_summaryMissesTouchpad},{_summaryFalseAlarmsTouchpad},{_summaryCorrectRejectionsTouchpad}," +
                $"{_summaryFalseAlarmsTouchpad},{_summaryMissesTouchpad}," +
                $"{_summaryHitsBoth},{_summaryMissesBoth},{_summaryFalseAlarmsBoth},{_summaryCorrectRejectionsBoth}," +
                $"{_summaryFalseAlarmsBoth},{_summaryMissesBoth}," +
                $"{DateTime.Now:O}\n";
            File.WriteAllText(path, summary, new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SC3bStreet: failed to save sc3b_summary.csv ({e.Message})");
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
            if (_responseWindowActive)
                _trialGazeSampleCount++;
        }
    }

    void CommitTrialGazeToOutcomeBuckets(int bothAccuracyCode)
    {
        if (bothAccuracyCode == 1)
            _lookedCorrectCount += _trialGazeSampleCount;
        else if (bothAccuracyCode == 0)
            _lookedWrongCount += _trialGazeSampleCount;
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

        _headWriter.WriteLine(string.Join(",",
            StudyCsvTime.FormatSecondsAsMs(sinceStart),
            StudyCsvTime.FormatOptionalPupilTimestampMs(havePupilTs, pupilTs),
            StudyCsvTime.FormatSecondsAsMs(Time.time),
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

    void ProcessControllerHandRow(float sinceStart, bool havePupilTs, double pupilTs, SteamVR_Input_Sources hand)
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

        var buttons = ControllerTimeseriesLog.Capture(hand, triggerPressThreshold);
        _controllerWriter.WriteLine(string.Join(",",
            StudyCsvTime.FormatSecondsAsMs(sinceStart),
            StudyCsvTime.FormatOptionalPupilTimestampMs(havePupilTs, pupilTs),
            StudyCsvTime.FormatSecondsAsMs(Time.time),
            F(p.x), F(p.y), F(p.z),
            F(euler.x), F(euler.y), F(euler.z),
            F(vLin),
            F(vAng),
            ControllerTimeseriesLog.FormatButtonColumns(buttons)));
    }

    void OpenSessionCsvWriters()
    {
        CloseSessionCsvWriters();
        string dir = LevelScript.GetBehaviouralPath(LevelScript.DataFolderSc3bStreet);
        Directory.CreateDirectory(dir);

        _timeseriesWriter = new StreamWriter(Path.Combine(dir, "gaze_timeseries.csv"), false, new UTF8Encoding(false)) { AutoFlush = false };
        _eventsWriter = new StreamWriter(Path.Combine(dir, "gaze_events.csv"), false, new UTF8Encoding(false)) { AutoFlush = false };
        _headWriter = new StreamWriter(Path.Combine(dir, "head_timeseries.csv"), false, new UTF8Encoding(false)) { AutoFlush = false };
        _controllerWriter = new StreamWriter(Path.Combine(dir, "controller_timeseries.csv"), false, new UTF8Encoding(false)) { AutoFlush = false };

        _timeseriesWriter.WriteLine(StudyCsvTime.TaskSessionTimeColumnsHeader + ",state,confidence,valid,hit_name,hit_tag,invalid_reason,gaze_hmd_local_x,gaze_hmd_local_y,gaze_hmd_local_z,yaw_deg,pitch_deg,equirect_u,equirect_v,gaze_world_x,gaze_world_y,gaze_world_z");
        _eventsWriter.WriteLine(StudyCsvTime.TaskSessionTimeColumnsHeader + ",event,confidence,valid,hit_name,hit_tag,invalid_reason,gaze_hmd_local_x,gaze_hmd_local_y,gaze_hmd_local_z,yaw_deg,pitch_deg,equirect_u,equirect_v,gaze_world_x,gaze_world_y,gaze_world_z");
        _headWriter.WriteLine(StudyCsvTime.TaskSessionTimeColumnsHeader + ",position_x,position_y,position_z,rotation_x,rotation_y,rotation_z,forward_x,forward_y,forward_z,up_x,up_y,up_z,vel_x,vel_y,vel_z,linear_speed,accel_lin_vec_mag,accel_linear,accel_angular,angular_speed,head_distance_to_road,head_angle_to_road");
        _controllerWriter.WriteLine(StudyCsvTime.TaskSessionTimeColumnsHeader + "," + ControllerTimeseriesLog.PoseColumnsHeader + "," + ControllerTimeseriesLog.ButtonColumnsHeader);

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
        string cCol = gazePacket ? F(conf) : "";
        _timeseriesWriter.WriteLine(string.Join(",",
            StudyCsvTime.FormatSecondsAsMs(sinceStart),
            StudyCsvTime.FormatOptionalPupilTimestampMs(havePupilTs, pupilTs),
            StudyCsvTime.FormatSecondsAsMs(Time.time),
            state.ToString(CultureInfo.InvariantCulture),
            cCol,
            valid.ToString(CultureInfo.InvariantCulture),
            CsvEscape(hitName),
            CsvEscape(hitTag),
            CsvEscape(invalidReason),
            glx, gly, glz, gyaw, gpit, gu, gv,
            gwx, gwy, gwz));
    }

    void WriteEventRow(float sinceStart, bool havePupilTs, double pupilTs, float conf, int valid, string hitName, string hitTag, string invalidReason, string ev, string glx, string gly, string glz, string gyaw, string gpit, string gu, string gv, string gwx, string gwy, string gwz)
    {
        if (_eventsWriter == null)
            return;
        _eventsWriter.WriteLine(string.Join(",",
            StudyCsvTime.FormatSecondsAsMs(sinceStart),
            StudyCsvTime.FormatOptionalPupilTimestampMs(havePupilTs, pupilTs),
            StudyCsvTime.FormatSecondsAsMs(Time.time),
            CsvEscape(ev),
            F(conf),
            valid.ToString(CultureInfo.InvariantCulture),
            CsvEscape(hitName),
            CsvEscape(hitTag),
            CsvEscape(invalidReason),
            glx, gly, glz, gyaw, gpit, gu, gv,
            gwx, gwy, gwz));
    }

    void WriteSceneReferenceJsonFiles(string sessionDir)
    {
        WriteSceneReferenceJsonOnce(sessionDir, () =>
        {
            var scene = SceneManager.GetActiveScene();
            var sb = new StringBuilder(1024);
            sb.Append('{');
            sb.Append("\"schema\":\"sc3b_scene_rois v1\",");
            sb.Append("\"unity_scene_name\":").Append(JsonString(scene.name)).Append(',');
            sb.Append("\"unity_time_ms_at_snapshot\":").Append(StudyCsvTime.FormatSecondsAsMs(Time.time)).Append(',');
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
        });
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
