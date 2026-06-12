using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using TMPro;
using Valve.VR;
using UnityEngine.UI;
using PupilLabs;
using UnityEngine.SceneManagement;

/// <summary>
/// Sustained-attention style digit stream: brief stimulus, blank ISI, response window = stimulus + ISI.
/// Sequence is pseudorandom with configurable constraints (no immediate repeats, no long monotonic runs,
/// roughly even target density over time). Trial table: task_trials.csv.
/// Session streams (gaze_timeseries, gaze_events, head_timeseries, controller_timeseries) follow the same column order as Sc1LivingRoom where applicable;
/// the first column is <c>time_since_task_start_ms</c> (milliseconds since session logging began at StartTask, including any pre-task delay). There is no video in this scene.
/// Gaze state 1 = hit collider tagged Book; spherecast matches Sc1 (radius 0.05).
/// </summary>
public class Sc2aLectureHall : LevelScript
{
    [SerializeField] TextMeshPro text = null;
    public Camera camera;

    [Space]
    [Header("Trial counts (defaults: 810 = 162 targets + 8×81 non-targets)")]
    [Min(1)] public int totalTrials = 810;
    [Min(1)] public int targetDigit = 3;
    [Min(0)] public int targetTrialCount = 162;
    [Tooltip("Each non-target digit (1–9 except target) appears this many times. Required: targetTrialCount + trialsPerNonTargetDigit × 8 = totalTrials when using digits 1–9.")]
    [Min(0)] public int trialsPerNonTargetDigit = 81;

    [Space]
    [Header("Timing (ms). Response window should equal stimulus + blank after removal.")]
    [Min(1)] public int stimulusDurationMs = 100;
    [Min(0)] public int postStimulusBlankMs = 1100;
    [Tooltip("Full trial window from stimulus onset to next stimulus onset (first response only). Default 1200 = 100 + 1100.")]
    [Min(1)] public int responseWindowMs = 1200;

    [Space]
    [Header("Block timing")]
    [Min(0f)] public float preTaskDelaySeconds = 3f;
    [Min(0f)] public float postBlockDelayBeforeNextSceneSeconds = 2f;

    [Space]
    [Header("Pseudorandom sequence")]
    [Tooltip("-1 = random seed each run; otherwise fixed for reproducibility.")]
    public int sequenceRandomSeed = -1;
    [Min(100)] public int maxSequenceShuffleAttempts = 25000;
    [Min(2)] public int minMonotonicRunLength = 4;
    [Tooltip("Must divide totalTrials and targetTrialCount for strict per-stratum target balancing.")]
    [Min(1)] public int targetStrataCount = 27;
    [Min(0)] public int targetStrataCountTolerance = 1;

    [Space]
    [Header("VR Trigger")]
    public SteamVR_Input_Sources handType;
    public SteamVR_Action_Boolean grabPinchAction = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("GrabPinch");
    public GameObject Pointer;

    [Space]
    [Header("Eye Tracker")]
    public RecordingController recorder;
    public Text statusText;
    [Tooltip("Optional. If assigned, last gaze packet supplies Pupil timestamp at stimulus onset (same idea as Sc1LivingRoom).")]
    public GazeController gazeController;
    /// <summary>HMD / Pupil calibration frame (same role as Sc1LivingRoom). Required for gaze_timeseries and head_timeseries rows.</summary>
    public Transform gazeOriginCamera;
    [Tooltip("Optional. World reference on the book for head distance/angle columns. If unset, uses first GameObject tagged Book.")]
    [SerializeField] Transform bookHeadMetricsAnchor;

    [Space]
    [Header("Session logging (streams under Behavioural; no video — time column is task-relative)")]
    [Tooltip("Seconds between gaze/head/controller CSV rows (same as Sc1LivingRoom period).")]
    public float period = 0.1f;
    [Tooltip("Gaze sample is valid for ROI hit testing only at or above this confidence.")]
    [Range(0f, 1f)]
    public float gazeConfidenceThreshold = 0.6f;
    [Min(0.5f)]
    public float csvFlushIntervalSeconds = 2f;
    [Range(0f, 1f)]
    public float triggerPressThreshold = 0.5f;

    StreamWriter timeseriesWriter;
    StreamWriter eventsWriter;
    StreamWriter headWriter;
    StreamWriter controllerWriter;
    float _sessionLogStartUnityTime;
    float _nextSessionLogTime;
    float _lastCsvFlushTime;
    bool _csvSessionLogging;
    bool _hasPreviousObservedState;
    int _previousObservedState;
    int lookedbookcount;
    int lookedelsecount;
    bool _headMotionPrimed;
    Vector3 _headPrevWorldPos;
    Quaternion _headPrevWorldRot;
    Vector3 _headLastVelWorld;
    float _headLastLinSpeed;
    float _headLastAngSpeed;
    int _headKinTickCount;
    int _summaryHits;
    int _summaryMisses;
    int _summaryFalseAlarms;
    int _summaryCorrectRejections;

    const int StateBook = 1;
    const int StateNotBook = 0;
    const int StateInvalid = -1;

    static SteamVR_Action_Pose VrPose => SteamVR_Actions.default_Pose;

    List<int> _trialDigits;
    int _loggedSequenceSeed;

    GazeData _lastGaze;
    bool _trialWindowActive;
    /// <summary>Stimulus onset in Unity game time (<see cref="Time.time"/>), same clock as Sc1LivingRoom CSV column unity_time_ms.</summary>
    float _trialOnsetUnityTime;
    bool _trialResponded;
    float _trialRtMs;
    string _trialCsvPath;
    bool _trialCsvHeaderWritten;
    void Awake()
    {
        if (camera != null)
            camera.clearFlags = CameraClearFlags.Skybox;
        if (Pointer != null)
            Pointer.SetActive(true);

        if (recorder != null)
        {
            recorder.customPath = LevelScript.GetEyeTrackingPath(LevelScript.DataFolderSc2LectureHall);
            if (recorder.requestCtrl != null)
                _ = recorder.requestCtrl.IsConnected;
        }

        ValidateAndBuildSequence();
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

        if (!_trialWindowActive || grabPinchAction == null)
            return;

        float now = Time.time;
        float window = responseWindowMs * 0.001f;
        if (now - _trialOnsetUnityTime >= window)
            return;

        if (!_trialResponded && grabPinchAction.GetStateDown(handType))
        {
            _trialResponded = true;
            _trialRtMs = Mathf.Clamp((now - _trialOnsetUnityTime) * 1000f, 0f, responseWindowMs);
        }
    }

    public new void StartTask()
    {
        if (_csvSessionLogging)
            return;
        base.StartTask();
        StartCoroutine(ClearData("sc2_data"));

        lookedbookcount = 0;
        lookedelsecount = 0;
        _hasPreviousObservedState = false;
        _previousObservedState = StateInvalid;
        _headMotionPrimed = false;
        _headKinTickCount = 0;
        _headLastVelWorld = Vector3.zero;
        _headLastLinSpeed = 0f;
        _headLastAngSpeed = 0f;
        _summaryHits = _summaryMisses = _summaryFalseAlarms = _summaryCorrectRejections = 0;

        _sessionLogStartUnityTime = Time.time;
        _nextSessionLogTime = Time.time;
        _lastCsvFlushTime = Time.time;

        OpenSessionCsvWriters();
        if (gazeOriginCamera != null)
            WriteSceneReferenceJsonFiles(LevelScript.GetBehaviouralPath(LevelScript.DataFolderSc2LectureHall));
        _csvSessionLogging = true;

        StartCoroutine(RunTaskCoroutine());
    }

    void ValidateAndBuildSequence()
    {
        int nonTargetKinds = 9 - 1;
        int expectedTotal = targetTrialCount + trialsPerNonTargetDigit * nonTargetKinds;
        if (expectedTotal != totalTrials)
        {
            Debug.LogWarning(
                $"Sc2aLectureHall: totalTrials ({totalTrials}) != targetTrialCount ({targetTrialCount}) + trialsPerNonTargetDigit×{nonTargetKinds} ({trialsPerNonTargetDigit * nonTargetKinds}). " +
                $"Expected sum {expectedTotal}. Sequence build may be wrong; fix Inspector counts.");
        }

        if (totalTrials <= 0 || targetTrialCount < 0 || trialsPerNonTargetDigit < 0)
        {
            Debug.LogError("Sc2aLectureHall: invalid trial counts.");
            _trialDigits = new List<int>();
            return;
        }

        if (totalTrials % targetStrataCount != 0 || targetTrialCount % targetStrataCount != 0)
        {
            Debug.LogWarning(
                $"Sc2aLectureHall: targetStrataCount ({targetStrataCount}) should divide totalTrials ({totalTrials}) and targetTrialCount ({targetTrialCount}) for even target spacing. Stratum check relaxed.");
        }

        int seed = sequenceRandomSeed >= 0 ? sequenceRandomSeed : UnityEngine.Random.Range(int.MinValue / 4, int.MaxValue / 4);
        _loggedSequenceSeed = seed;
        _trialDigits = BuildConstrainedSequence(seed, out bool ok);
        if (!ok)
            Debug.LogError("Sc2aLectureHall: could not satisfy sequence constraints; using last attempt. Consider more shuffle attempts or looser strata.");
    }

    List<int> BuildConstrainedSequence(int seed, out bool success)
    {
        var rng = new System.Random(seed);
        var multiset = new List<int>(totalTrials);
        for (int i = 0; i < targetTrialCount; i++)
            multiset.Add(targetDigit);
        for (int d = 1; d <= 9; d++)
        {
            if (d == targetDigit)
                continue;
            for (int i = 0; i < trialsPerNonTargetDigit; i++)
                multiset.Add(d);
        }

        if (multiset.Count != totalTrials)
        {
            success = false;
            return multiset;
        }

        for (int attempt = 0; attempt < maxSequenceShuffleAttempts; attempt++)
        {
            Shuffle(multiset, rng);
            if (PassesConstraints(multiset))
            {
                success = true;
                return multiset;
            }
        }

        for (int repair = 0; repair < 80000; repair++)
        {
            int i = rng.Next(multiset.Count);
            int j = rng.Next(multiset.Count);
            if (i == j)
                continue;
            (multiset[i], multiset[j]) = (multiset[j], multiset[i]);
            if (PassesConstraints(multiset))
            {
                success = true;
                return multiset;
            }
        }

        success = false;
        return multiset;
    }

    bool PassesConstraints(List<int> seq)
    {
        if (seq.Count < 2)
            return true;
        for (int i = 0; i < seq.Count - 1; i++)
        {
            if (seq[i] == seq[i + 1])
                return false;
        }

        if (minMonotonicRunLength >= 3)
        {
            for (int i = 0; i + minMonotonicRunLength - 1 < seq.Count; i++)
            {
                bool asc = true, desc = true;
                for (int k = 1; k < minMonotonicRunLength; k++)
                {
                    if (seq[i + k] != seq[i + k - 1] + 1)
                        asc = false;
                    if (seq[i + k] != seq[i + k - 1] - 1)
                        desc = false;
                }
                if (asc || desc)
                    return false;
            }
        }

        if (totalTrials % targetStrataCount == 0 && targetTrialCount % targetStrataCount == 0)
        {
            int stratumSize = totalTrials / targetStrataCount;
            int expectedTargets = targetTrialCount / targetStrataCount;
            for (int s = 0; s < targetStrataCount; s++)
            {
                int start = s * stratumSize;
                int c = 0;
                for (int i = 0; i < stratumSize; i++)
                {
                    if (seq[start + i] == targetDigit)
                        c++;
                }
                if (Mathf.Abs(c - expectedTargets) > targetStrataCountTolerance)
                    return false;
            }
        }

        return true;
    }

    static void Shuffle(IList<int> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    IEnumerator RunTaskCoroutine()
    {
        if (preTaskDelaySeconds > 0f)
            yield return new WaitForSeconds(preTaskDelaySeconds);

        float stimSec = stimulusDurationMs * 0.001f;
        float windowSec = responseWindowMs * 0.001f;
        if (stimSec + postStimulusBlankMs * 0.001f > windowSec + 0.0001f)
            Debug.LogWarning("Sc2aLectureHall: stimulusDurationMs + postStimulusBlankMs exceeds responseWindowMs; window ends before blank ISI completes.");

        if (_trialDigits == null || _trialDigits.Count != totalTrials)
        {
            Debug.LogError("Sc2aLectureHall: invalid sequence; aborting task.");
            _csvSessionLogging = false;
            CloseSessionCsvWriters();
            yield break;
        }

        for (int trialIndex = 0; trialIndex < totalTrials; trialIndex++)
        {
            int digit = _trialDigits[trialIndex];
            int targetFlag = digit == targetDigit ? 1 : 0;

            _trialOnsetUnityTime = Time.time;
            double unityOnset = _trialOnsetUnityTime;
            double pupilOnset = double.NaN;
            if (_lastGaze != null)
                pupilOnset = _lastGaze.PupilTimestamp;

            _trialResponded = false;
            _trialRtMs = 0f;
            _trialWindowActive = true;

            if (text != null)
                text.text = digit.ToString();

            float windowEnd = _trialOnsetUnityTime + windowSec;
            while (Time.time < windowEnd)
            {
                if (Time.time - _trialOnsetUnityTime >= stimSec && text != null)
                    text.text = string.Empty;
                yield return null;
            }

            _trialWindowActive = false;
            if (text != null)
                text.text = string.Empty;

            int responseFlag = _trialResponded ? 1 : 0;
            int outcomeCode = StudyTaskTrialsLog.ComputeOutcomeCode(targetFlag, responseFlag);
            int accuracyCode = ComputeTrialAccuracyCode(targetFlag, responseFlag);
            string rtCell = _trialResponded ? _trialRtMs.ToString("0.###", CultureInfo.InvariantCulture) : "NaN";
            float sinceTaskStartSec = _trialOnsetUnityTime - _sessionLogStartUnityTime;

            AppendTrialRow(
                sinceTaskStartSec,
                trialIndex + 1,
                unityOnset,
                pupilOnset,
                digit,
                targetFlag,
                responseFlag,
                outcomeCode,
                accuracyCode,
                rtCell);

            switch (outcomeCode)
            {
                case 1: _summaryHits++; break;
                case 2: _summaryMisses++; break;
                case 3: _summaryFalseAlarms++; break;
                case 4: _summaryCorrectRejections++; break;
            }
        }

        _csvSessionLogging = false;
        CloseSessionCsvWriters();
        WriteSc2aSummaryCsv();

        if (recorder != null)
            recorder.StopRecording();
        if (postBlockDelayBeforeNextSceneSeconds > 0f)
            yield return new WaitForSeconds(postBlockDelayBeforeNextSceneSeconds);
        NextScene();
    }

    /// <summary>1 correct (hit or correct rejection), 0 wrong (false alarm), 2 no response (miss).</summary>
    static int ComputeTrialAccuracyCode(int targetFlag, int responseFlag)
    {
        if (responseFlag == 0 && targetFlag == 1)
            return 2;
        if (responseFlag == 0)
            return 1;
        return targetFlag == 1 ? 1 : 0;
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
            hitName = "";
            hitTag = "";
            invalidReason = "no_gaze";
        }
        else if (!confOk)
        {
            valid = 0;
            state = StateInvalid;
            hitName = "";
            hitTag = "";
            invalidReason = "low_confidence";
        }
        else
        {
            if (!TryBuildGazeRay(gd, gazeOriginCamera, out Vector3 origin, out Vector3 direction)
                || direction.sqrMagnitude < 1e-8f)
            {
                valid = 0;
                state = StateInvalid;
                hitName = "";
                hitTag = "";
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
                        state = col.CompareTag("Book") ? StateBook : StateNotBook;
                    }
                    else
                    {
                        state = StateNotBook;
                    }

                    invalidReason = "";
                }
                else
                {
                    valid = 1;
                    state = StateNotBook;
                    hitName = "no_hit";
                    invalidReason = "";
                }
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
            string ev = EventForBookTransition(_previousObservedState, state);
            if (ev != null)
                WriteEventRow(sinceStart, havePupilTs, pupilTs, conf, valid, hitName, hitTag, invalidReason, ev, glx, gly, glz, gyaw, gpit, gu, gv, gwx, gwy, gwz);
            _previousObservedState = state;
        }

        if (valid == 1)
        {
            if (state == StateBook)
                lookedbookcount++;
            else
                lookedelsecount++;
        }
    }

    void ProcessHeadMotionRow(float sinceStart, bool havePupilTs, double pupilTs)
    {
        if (headWriter == null || gazeOriginCamera == null)
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
        if (TryGetBookWorldPoint(out Vector3 bookPos))
        {
            distCol = F(Vector3.Distance(p, bookPos));
            Vector3 toBook = bookPos - p;
            if (toBook.sqrMagnitude > 1e-10f)
                angCol = F(Vector3.Angle(cam.forward, toBook.normalized));
        }

        headWriter.WriteLine(string.Join(",",
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
        if (controllerWriter == null)
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

        controllerWriter.WriteLine(string.Join(",",
            StudyCsvTime.FormatSecondsAsMs(sinceStart),
            StudyCsvTime.FormatOptionalPupilTimestampMs(havePupilTs, pupilTs),
            StudyCsvTime.FormatSecondsAsMs(Time.time),
            F(p.x), F(p.y), F(p.z),
            F(euler.x), F(euler.y), F(euler.z),
            F(vLin),
            F(vAng),
            ControllerTimeseriesLog.FormatButtonColumns(buttons)));
    }

    static Transform GetTrackingRigOrigin()
    {
        if (SteamVR.initializedState != SteamVR.InitializedStates.InitializeSuccess)
            return null;
        var top = SteamVR_Render.Top();
        return top != null ? top.origin : null;
    }

    Transform GetBookTransformForSnapshot()
    {
        if (bookHeadMetricsAnchor != null)
            return bookHeadMetricsAnchor;
        var go = GameObject.FindGameObjectWithTag("Book");
        return go != null ? go.transform : null;
    }

    bool TryGetBookWorldPoint(out Vector3 bookPos)
    {
        var t = GetBookTransformForSnapshot();
        if (t != null)
        {
            bookPos = t.position;
            return true;
        }

        bookPos = default;
        return false;
    }

    void WriteSceneReferenceJsonFiles(string sessionDir)
    {
        WriteSceneReferenceJsonOnce(sessionDir, () =>
        {
            var scene = SceneManager.GetActiveScene();
            var sb = new StringBuilder(1024);
            sb.Append('{');
            sb.Append("\"schema\":\"sc2a_scene_rois v1\",");
            sb.Append("\"unity_scene_name\":").Append(JsonString(scene.name)).Append(',');
            sb.Append("\"unity_time_ms_at_snapshot\":").Append(StudyCsvTime.FormatSecondsAsMs(Time.time)).Append(',');
            sb.Append("\"equirect_heatmap_columns\":").Append(JsonString("gaze_hmd_local_x,y,z (HMD +Z forward +Y up); yaw_deg; pitch_deg; equirect_u,v zenith-top pano; see VrGazeEquirectMetrics")).Append(',');
            AppendTransformBlock(sb, "book", GetBookTransformForSnapshot());
            sb.Append(',');
            AppendTransformBlock(sb, "hmd_origin_at_session_start", gazeOriginCamera);
            sb.Append(',');
            Transform rig = GetTrackingRigOrigin();
            AppendTransformBlock(sb, "tracking_rig_origin", rig);
            sb.Append('}');
            File.WriteAllText(Path.Combine(sessionDir, "scene_rois.json"), sb.ToString(), new UTF8Encoding(false));

            const string roiBookTemplate = "{\"roi_name\":\"Book\",\"type\":\"polygon\",\"points\":[],\"note\":\"Normalized panorama UV polygon; fill after calibration or external tooling.\"}";
            File.WriteAllText(Path.Combine(sessionDir, "roi_book.json"), roiBookTemplate, new UTF8Encoding(false));
            LevelScript.WritePanoReferenceJson(sessionDir, scene.name);
        });
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

    /// <summary>Book ROI: valid↔valid and Book↔invalid (same enter/exit pattern as Sc1 gaze events).</summary>
    static string EventForBookTransition(int prev, int curr)
    {
        if (prev == curr)
            return null;
        bool prevBook = prev == StateBook;
        bool currBook = curr == StateBook;
        if (currBook && !prevBook)
            return "enter_book";
        if (!currBook && prevBook)
            return "exit_book";
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

    void WriteTimeseriesRow(float sinceStart, bool gazePacket, bool havePupilTs, double pupilTs, float conf, int valid, int state, string hitName, string hitTag, string invalidReason, string glx, string gly, string glz, string gyaw, string gpit, string gu, string gv, string gwx, string gwy, string gwz)
    {
        if (timeseriesWriter == null)
            return;
        string cCol = gazePacket ? F(conf) : "";
        timeseriesWriter.WriteLine(string.Join(",",
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
        if (eventsWriter == null)
            return;
        eventsWriter.WriteLine(string.Join(",",
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

    void MaybePeriodicFlushCsv()
    {
        if (timeseriesWriter == null && eventsWriter == null && headWriter == null && controllerWriter == null)
            return;
        if (Time.time - _lastCsvFlushTime < csvFlushIntervalSeconds)
            return;
        _lastCsvFlushTime = Time.time;
        timeseriesWriter?.Flush();
        eventsWriter?.Flush();
        headWriter?.Flush();
        controllerWriter?.Flush();
    }

    void OpenSessionCsvWriters()
    {
        CloseSessionCsvWriters();
        string dir = LevelScript.GetBehaviouralPath(LevelScript.DataFolderSc2LectureHall);
        Directory.CreateDirectory(dir);

        timeseriesWriter = new StreamWriter(Path.Combine(dir, "gaze_timeseries.csv"), false, new UTF8Encoding(false)) { AutoFlush = false };
        eventsWriter = new StreamWriter(Path.Combine(dir, "gaze_events.csv"), false, new UTF8Encoding(false)) { AutoFlush = false };
        headWriter = new StreamWriter(Path.Combine(dir, "head_timeseries.csv"), false, new UTF8Encoding(false)) { AutoFlush = false };
        controllerWriter = new StreamWriter(Path.Combine(dir, "controller_timeseries.csv"), false, new UTF8Encoding(false)) { AutoFlush = false };

        timeseriesWriter.WriteLine(StudyCsvTime.TaskSessionTimeColumnsHeader + ",state,confidence,valid,hit_name,hit_tag,invalid_reason,gaze_hmd_local_x,gaze_hmd_local_y,gaze_hmd_local_z,yaw_deg,pitch_deg,equirect_u,equirect_v,gaze_world_x,gaze_world_y,gaze_world_z");
        eventsWriter.WriteLine(StudyCsvTime.TaskSessionTimeColumnsHeader + ",event,confidence,valid,hit_name,hit_tag,invalid_reason,gaze_hmd_local_x,gaze_hmd_local_y,gaze_hmd_local_z,yaw_deg,pitch_deg,equirect_u,equirect_v,gaze_world_x,gaze_world_y,gaze_world_z");
        headWriter.WriteLine(StudyCsvTime.TaskSessionTimeColumnsHeader + ",position_x,position_y,position_z,rotation_x,rotation_y,rotation_z,forward_x,forward_y,forward_z,up_x,up_y,up_z,vel_x,vel_y,vel_z,linear_speed,accel_lin_vec_mag,accel_linear,accel_angular,angular_speed,head_distance_to_book,head_angle_to_book");
        controllerWriter.WriteLine(StudyCsvTime.TaskSessionTimeColumnsHeader + "," + ControllerTimeseriesLog.PoseColumnsHeader + "," + ControllerTimeseriesLog.ButtonColumnsHeader);

        _lastCsvFlushTime = Time.time;
        timeseriesWriter.Flush();
        eventsWriter.Flush();
        headWriter.Flush();
        controllerWriter.Flush();
    }

    void CloseSessionCsvWriters()
    {
        if (timeseriesWriter != null)
        {
            timeseriesWriter.Flush();
            timeseriesWriter.Dispose();
            timeseriesWriter = null;
        }

        if (eventsWriter != null)
        {
            eventsWriter.Flush();
            eventsWriter.Dispose();
            eventsWriter = null;
        }

        if (headWriter != null)
        {
            headWriter.Flush();
            headWriter.Dispose();
            headWriter = null;
        }

        if (controllerWriter != null)
        {
            controllerWriter.Flush();
            controllerWriter.Dispose();
            controllerWriter = null;
        }
    }

    void WriteSc2aSummaryCsv()
    {
        try
        {
            string dir = LevelScript.GetBehaviouralPath(LevelScript.DataFolderSc2LectureHall);
            if (string.IsNullOrWhiteSpace(dir))
                return;
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "sc2a_summary.csv");
            string summary =
                "lookedbookcount,lookedbooktime_ms,lookedelsecount,lookedelsetime_ms,hits,misses,false_alarms,correct_rejections,commission_errors,omission_errors,created_at\n" +
                $"{lookedbookcount},{StudyCsvTime.GazeSampleCountToMs(lookedbookcount, period)}," +
                $"{lookedelsecount},{StudyCsvTime.GazeSampleCountToMs(lookedelsecount, period)}," +
                $"{_summaryHits},{_summaryMisses},{_summaryFalseAlarms},{_summaryCorrectRejections}," +
                $"{_summaryFalseAlarms},{_summaryMisses}," +
                $"{DateTime.Now:O}\n";
            File.WriteAllText(path, summary, new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Sc2aLectureHall: failed to save sc2a_summary.csv ({e.Message})");
        }
    }

    void AppendTrialRow(float sinceTaskStartSec, int trialIndexOneBased, double unityStimulusTime, double pupilStimulusTime, int digit, int targetFlag, int responseFlag, int outcomeCode, int accuracyCode, string reactionTimeCell)
    {
        string dir = LevelScript.GetBehaviouralPath(LevelScript.DataFolderSc2LectureHall);
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
                    "unity_time_ms_stimulus_onset," +
                    "pupil_timestamp_ms_at_stimulus_onset," +
                    "digit," +
                    "target," +
                    "response," +
                    StudyTaskTrialsLog.OutcomeColumnName + "," +
                    "commission_0_no_1_yes," +
                    "omission_0_no_1_yes," +
                    "accuracy_1_correct_0_wrong_2_no_response," +
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
            StudyCsvTime.FormatSecondsAsMs(unityStimulusTime) + "," +
            StudyCsvTime.FormatOptionalTimestampCellMs(pupilStimulusTime) + "," +
            digit.ToString(CultureInfo.InvariantCulture) + "," +
            targetFlag.ToString(CultureInfo.InvariantCulture) + "," +
            responseFlag.ToString(CultureInfo.InvariantCulture) + "," +
            outcomeCode.ToString(CultureInfo.InvariantCulture) + "," +
            StudyTaskTrialsLog.CommissionFromOutcome(outcomeCode).ToString(CultureInfo.InvariantCulture) + "," +
            StudyTaskTrialsLog.OmissionFromOutcome(outcomeCode).ToString(CultureInfo.InvariantCulture) + "," +
            accuracyCode.ToString(CultureInfo.InvariantCulture) + "," +
            reactionTimeCell + "," +
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\n";

        File.AppendAllText(_trialCsvPath, row, new UTF8Encoding(false));
    }

}
